# PCUpdate Command Undo Fix Execution

## 对应计划

- Plan: `Project_Plan/260903_PLAN_pcupdate-command-undo.md`
- Execution date: 2026-09-03

## 关联产物

- Tests: `Project_Test/260903_TEST_pcupdate-command-undo/`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.71/`
- Commit / PR: not created in this execution

## 执行结果 / 实际落地范围

- Changed `LivePanelCladdingUpdateService.Apply` to read the active Rhino Undo serial before
  requesting a new record.
- Reused Rhino's ambient command-owned record when `_PCUpdate` runs as a normal command.
- Preserved explicit `BeginUndoRecord` behavior for service calls made without an ambient record.
- Guarded `EndUndoRecord` and automatic `RhinoDoc.Undo()` rollback so the service only manipulates
  records it owns.
- Bumped the standalone plug-in package from `1.0.70` to `1.0.71` and built the corrected bundle.
- Added a focused regression and excluded that standalone source precisely from the MCP server's
  intentional `Project_Test/**/*.cs` compile glob.

## 与计划的偏差

The optional native RhinoCore probe could not initialize a second in-process Rhino runtime in this
execution environment. Static/contract, build, package, registration, and assembly-identity gates
completed as planned. No production activation was attempted.

## 施工中发现并修复的问题

- The original `PCUpdate` smoke asserted only that `BeginUndoRecord` existed. That assertion could
  not distinguish a valid no-ambient service path from an invalid nested command path. The new
  regression asserts the ambient-record read and ownership guards around begin/end/rollback.
- A concurrent pair of Release test runs competed for the same intermediate
  `PanelCladdingEditor.dll` and produced one `CS2012` file-lock failure. Rerunning the affected smoke
  serially passed; final recorded results use the serial pass.
- Solution builds can leave the test-host `.dll` as the last output in the shared PanelCladdingEditor
  directory. A final direct Release rebuild restored the `.rhp` output before its hash was compared
  with the packaged RHP.

## 测试记录

Full commands and outcomes are recorded in
`Project_Test/260903_TEST_pcupdate-command-undo/RESULTS.md`.

- Focused Debug and Release Undo ownership smokes: passed.
- PanelCladdingEditor Debug and Release builds: passed, 0 warnings, 0 errors.
- MCP_Rhino solution Debug and Release builds: passed, 0 warnings, 0 errors.
- Existing PCUpdate Debug/Release and PC command registration regressions: passed.
- Package `1.0.71` build: passed; 28 files.
- Isolated registry-only install/validate/repair regression: passed.
- Direct compiled RHP identity and cross-product uniqueness gate: passed.
- Direct-build and packaged RHP SHA-256:
  `4ABC46259803D3C8F3EED0F80EACADE1E3EEB08207FB439B468ECB36491161BA`.
- Native RhinoCore probe: unavailable because startup returned `COMException (0x80004005)` after
  native DLL discovery was corrected.

## 验收判据对齐

- Ambient command Undo serial is reused without nested `BeginUndoRecord`: passed by source-contract
  regression and production compilation.
- Only service-owned records are ended or automatically undone: passed.
- Real no-record failure still returns `PANEL_CLADDING_UPDATE_UNDO_UNAVAILABLE`: preserved by the
  unchanged zero-serial guard.
- One normal Rhino command Undo entry: implemented by retaining Rhino's command-owned record;
  native installed confirmation remains pending.
- Corrected package and plug-in identity: passed.

## 回退验证

No live Rhino document or production installation was changed. Reverting the ambient-record branch,
focused regression/exclusion, README note, and package version restores the previous source state.
The prior installed RHP remains reachable and untouched.

## 当前遗留项

- Install `PanelCladdingEditor` `1.0.71` through a host-persistent registry context while Rhino is
  closed, run `_PCUpdate` on a fixture, and verify one-step Undo.
- The other standalone `PC*` commands contain similar unconditional `BeginUndoRecord` patterns, but
  they were outside this reported `PCUpdate` fix and were not changed.

## 结论

The reported `PANEL_CLADDING_UPDATE_UNDO_UNAVAILABLE` defect is fixed in source and packaged as
`PanelCladdingEditor` `1.0.71`. `PCUpdate` no longer attempts a nested Undo record during its normal
Rhino command lifecycle, while non-command service calls retain explicit Undo ownership. Production
activation and live-document verification remain intentionally separate host-side steps.
