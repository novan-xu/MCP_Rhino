# Panel Cladding Update Command Plan

## 背景

The current panel-cladding command set separates spawn and geometry-authoritative sync workflows.
There is no single panel-authoritative command that treats the attributes saved by `PCEditor` as the
complete specification for all dependent STEP surfaces and extrusion curves. Users therefore cannot
repair missing dependencies or remove obsolete CIDs in one batch operation.

## 目标

- Add a Rhino command named `PCUpdate`.
- Accept one or more selected panel Breps, including preselection, using the same batch interaction
  pattern as the existing commands.
- Treat each selected panel's saved attributes as authoritative for both cladding surfaces and
  extrusion curves.
- Update the geometry, layer, name, and canonical metadata of dependencies whose expected CID exists.
- Create every expected dependency whose CID is missing.
- Delete managed dependencies whose PID belongs to a selected panel but whose nonblank canonical
  `CW_1.02_CID` is no longer expected, including duplicate legacy copies.
- Restrict all discovery, replacement, creation, and deletion to Breps under `04_STEP Surfaces` and
  curves under `02_CW Extrusions`.
- Leave objects outside those roots, objects without a nonblank canonical CID, and objects owned by
  unselected panel PIDs untouched.
- Apply the full batch as one Rhino Undo operation.

## 架构归属

- `UI/` owns the thin `PCUpdate` Rhino command and batch selection UX.
- `Application/Interfaces` owns the live update contract.
- `Domain/Models/PanelCladding` owns the update result DTO.
- `Infrastructure/Rhino/Live/PanelCladding` owns live document discovery, geometry replacement,
  creation, deletion, layer mutation, and Undo behavior.
- Existing spawn planning and geometry partition services remain the authoritative generators and
  are reused rather than reimplemented.
- Packaging owns command registration and deployment validation.

## 关键设计

1. Preflight every selected panel before document mutation by preparing its complete surface and
   curve geometry from current panel attributes.
2. Fail the batch before mutation when a panel cannot be prepared, when selected panels share a PID,
   or when expected CIDs collide.
3. Discover candidate dependencies only when all of the following are true:
   - geometry/root pair is managed (`Brep` under `04_STEP Surfaces`, `Curve` under
     `02_CW Extrusions`);
   - canonical PID equals a selected panel PID; and
   - canonical CID exists and is nonblank.
4. For each expected CID, retain at most one matching managed object, replace its geometry in place,
   and rewrite owned attributes/layer; create it if no match exists.
5. Delete every discovered managed object not retained, covering obsolete CIDs and duplicate copies.
6. Wrap the entire batch in one Undo record and automatically undo partial mutation if a write fails.
7. Report created, updated, and deleted object IDs and per-family counts.

## 涉及文件

- `src/PanelCladdingEditor/UI/PanelCladdingUpdateCommand.cs`
- `src/PanelCladdingEditor/Application/Interfaces/IPanelCladdingServices.cs`
- `src/PanelCladdingEditor/Domain/PanelCladdingUpdateModels.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingUpdateService.cs`
- Shared access points in `LivePanelCladdingSpawnService.cs`
- `Packaging/PanelCladdingEditor/README.md`
- `Packaging/PanelCladdingEditor/Install-PanelCladdingEditor.ps1`
- Command registration/identity regression fixtures
- `Project_Test/260903_TEST_panel-cladding-update-command/`
- `Project_Exet/260903_EXET_panel-cladding-update-command.md`

## 使用方式

After confirming panel data in `PCEditor`, preselect or select one or more panel Breps and run
`PCUpdate`. The command reconciles both managed dependency families in one operation and prints
created, updated, and deleted counts.

## 验收标准

- `PCUpdate` is compiled, has a unique non-empty command GUID, and is present in the exact package
  command list.
- The command accepts a batch of panel Breps and invokes one combined update service call.
- Application planning produces deterministic create/update/delete reconciliation for expected,
  missing, duplicated, and obsolete CIDs.
- Live mutation is constrained by geometry class, managed layer root, selected PID, and nonblank CID.
- Matching object IDs are retained through Rhino `Replace`; missing objects are created; obsolete and
  duplicate managed objects are deleted.
- Unmanaged/scope-ineligible objects are excluded by construction.
- Debug/Release plug-in builds, focused smokes, package registration validation, and affected panel
  regressions pass.

## 风险与回退方案

- This command is intentionally destructive inside managed roots. One Undo record provides user
  recovery, and full preflight prevents predictable partial batches.
- Duplicate PIDs among selected panels are rejected to prevent ambiguous ownership.
- Replacing generated objects could affect downstream references to their geometry, but preserving
  the retained Rhino object ID minimizes that risk.
- Reverting the command, interface/model, service, packaging entry, and tests removes the capability
  without changing stored panel attributes.

## 后续扩展方向

- Add an optional preview/report-only command if users need to audit changes before mutation.
- Add a live Rhino fixture that exercises create, replace, duplicate cleanup, stale deletion, and Undo
  against curved production panels.
