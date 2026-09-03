# PCSpawnCrv Curve Simplification and Layer Routing Plan

## 背景

`PCSpawnCrv` currently joins duplicated panel-cell edges and writes every result to
`02_CW Extrusions::Curves-PNL::Main Frame`. Joined edge curves can retain unnecessary NURBS
control points, and the single hard-coded output layer loses the source panel-type organization.

## 目标

- Simplify every curve created by `PCSpawnCrv` within the active document's model tolerance.
- Map a source panel layer such as `01_CW Panels::Surfaces-PNL::WT-04` to
  `02_CW Extrusions::Curves-PNL::WT-04`.
- Preserve any nested suffix below `Surfaces-PNL`, so mixed panel types in one command are routed
  independently.
- Keep curve metadata, CID generation, extrusion assignments, and one-command Undo behavior intact.

## 架构归属

- Layer-path derivation belongs to the panel-cladding application planning service because it is a
  deterministic business rule without RhinoCommon dependencies.
- Geometry simplification and Rhino layer creation remain in the live Rhino infrastructure layer.
- Regression coverage belongs in `Project_Test/260903_TEST_pcspawncrv-curve-simplification/`.

## 关键设计

1. Define canonical source and destination roots and derive the destination by replacing the
   `01_CW Panels::Surfaces-PNL` prefix with `02_CW Extrusions::Curves-PNL`.
2. Reject panels that have no child layer below the canonical source root, rather than silently
   placing curves in an unrelated fallback layer.
3. Carry the derived path on each `PanelCladdingExtrusionCurvePlan` and resolve layers per curve
   plan so a multi-panel batch can target multiple type layers.
4. After joining source edges, run RhinoCommon curve simplification and tolerance-bounded fitting;
   accept a fitted curve only when it lowers the NURBS control-point count.
5. Let curve sync discover the entire `Curves-PNL` subtree and use the panel's source layer when it
   regenerates expected curve plans.

## 涉及文件

- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingExtrusionPlanningService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingGeometryPartitionService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSpawnService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSurfaceSyncRepository.cs`
- Existing extrusion-planning smoke fixtures whose method contract changes
- `Project_Test/260903_TEST_pcspawncrv-curve-simplification/`
- `Project_Exet/260903_EXET_pcspawncrv-curve-simplification.md`

## 使用方式

Select one or more panel Breps organized below `01_CW Panels::Surfaces-PNL::<type>` and run
`PCSpawnCrv`. Each generated curve is simplified and placed below the corresponding
`02_CW Extrusions::Curves-PNL::<type>` path.

## 验收标准

- `WT-04` maps exactly to `02_CW Extrusions::Curves-PNL::WT-04`.
- Nested suffixes are preserved and unrelated/root-only panel layers fail closed.
- Every curve in a plan receives the derived destination layer.
- A high-control-point test curve is reduced without exceeding the supplied fit tolerance.
- Existing panel-cladding regression smokes and Debug/Release project builds pass.

## 风险与回退方案

- Curve fitting could alter geometry; it is bounded by model tolerance and retained only when it
  reduces control points. Reverting the simplification helper restores exact joined-edge output.
- Stricter layer validation can reject legacy panels outside the canonical subtree. Reverting the
  layer resolver and per-plan layer selection restores the former `Main Frame` fallback.
- Existing curves on the legacy layer remain untouched; this change affects new spawns and sync
  discovery only.

## 后续扩展方向

- Add a live Rhino fixture measuring maximum geometric deviation on representative curved panels.
- Consider a migration command for legacy curves on `Curves-PNL::Main Frame` if required.
