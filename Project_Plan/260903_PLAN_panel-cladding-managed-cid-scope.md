# Panel Cladding Managed CID Scope Plan

## 背景

`PCSpawnSrf` currently searches the entire Rhino document for an existing canonical CID before it
creates a surface. This makes detail-modeling objects on unrelated layers block managed STEP
surface creation. `PCSyncSrf` also retains legacy material-root discovery, so geometry outside the
current managed output roots can participate in synchronization.

## 目标

- Restrict surface CID conflicts to Breps on `04_STEP Surfaces` or its descendants.
- Restrict curve CID conflicts to curves on `02_CW Extrusions` or its descendants.
- Let the same CID exist freely on all other layer trees for detail modeling and tracking.
- Make `PCSyncSrf` discover spawned surfaces only under `04_STEP Surfaces`.
- Make `PCSyncCrv` discover extrusion curves only under `02_CW Extrusions`.
- Preserve duplicate protection among objects managed by the panel-cladding commands.

## 架构归属

- Managed-layer ownership predicates belong to the application layer as deterministic path policy.
- Rhino object type/layer inspection and CID conflict filtering remain in the live infrastructure
  adapters.
- Regression coverage belongs to `Project_Test/260903_TEST_panel-cladding-managed-cid-scope/`.

## 关键设计

1. Add canonical managed-root predicates that accept a root itself or any descendant using a
   segment-aware `::` boundary.
2. During spawn preflight, filter `FindByUserString` results by requested scope, geometry type, and
   the corresponding managed root before raising `PANEL_CLADDING_CID_ALREADY_EXISTS`.
3. Reuse the same predicates in sync discovery so duplicate CIDs elsewhere never become candidates
   or create ambiguous ownership.
4. Keep in-batch duplicate CID detection unchanged because all objects in a spawn batch are managed
   output.

## 涉及文件

- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSpawnPlanningService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSpawnService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSurfaceSyncRepository.cs`
- `Project_Test/260903_TEST_panel-cladding-managed-cid-scope/`
- `Project_Exet/260903_EXET_panel-cladding-managed-cid-scope.md`

## 使用方式

Detailed objects may carry the same `CW_1.02_CID` as a managed panel surface or extrusion curve as
long as they remain outside the two managed roots. `PCSpawnSrf`, `PCSpawnCrv`, `PCSyncSrf`, and
`PCSyncCrv` ignore those external copies.

## 验收标准

- `04_STEP Surfaces` and descendants are recognized as managed surface layers.
- `02_CW Extrusions` and descendants are recognized as managed extrusion layers.
- Similar prefixes, legacy STEP roots, panel source layers, and unrelated detail layers are rejected.
- Spawn CID preflight uses scope, geometry class, and managed-layer ownership.
- Sync discovery uses the same managed-root predicates.
- Debug/Release plug-in builds and affected regression smokes pass.

## 风险与回退方案

- Legacy surfaces under `03_Material Surfaces (STEP)` will no longer be sync candidates. This is an
  intentional tightening requested for current managed output; reverting the sync predicate restores
  legacy discovery.
- Mis-layered managed geometry will no longer protect its CID. Moving it into the canonical managed
  root restores collision protection without changing metadata.
- Reverting the three production files restores document-wide CID conflicts and previous discovery.

## 后续扩展方向

- Add a dedicated migration/audit command if legacy STEP surfaces need to be moved into the current
  managed root.
