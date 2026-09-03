# PCUpdate Locked Managed Objects Execution

## 对应计划

- Plan: `Project_Plan/260903_PLAN_pcupdate-locked-managed-objects.md`
- Execution date: 2026-09-03

## 关联产物

- Tests: `Project_Test/260903_TEST_pcupdate-locked-managed-objects/`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.72/`
- Commit / PR: not created in this execution

## 执行结果 / 实际落地范围

- Replaced the mode-sensitive typed Brep/Curve update calls with RhinoCommon's
  `Replace(Guid, GeometryBase, ignoreModes: true)` overload.
- Changed stale/duplicate dependency removal to
  `Delete(RhinoObject, quiet: true, ignoreModes: true)`.
- Captured the retained object's original `ObjectAttributes.Mode` and reapplied it to authoritative
  rebuilt attributes, preserving object-level locked/hidden state.
- Did not unlock/show objects and did not alter managed-layer lock or visibility properties.
- Kept discovery restricted to existing managed roots, Brep/Curve kinds, selected PIDs, canonical
  nonblank CIDs, and non-reference objects.
- Bumped the standalone plug-in package to `1.0.72` and built the corrected bundle.
- Added a focused regression and one precise standalone-test exclusion in the MCP server project.

## 与计划的偏差

No implementation scope changed. A native locked/hidden Rhino fixture was not executed from the
agent context; production activation remains subject to the repository's independent host-registry
attestation rule.

## 施工中发现并修复的问题

- Existing discovery already included locked and hidden objects, so changing enumeration was not
  required. The defect was confined to RhinoCommon mutation overload selection.
- The generic `GeometryBase` replacement overload provides the required `ignoreModes` flag for both
  managed Breps and Curves; the typed Guid overloads used previously do not expose it.
- Deletion also needed an explicit mode-bypassing overload or the next locked/hidden stale object
  would fail after retained-object replacement was corrected.
- Rebuilt attributes are authoritative but start in normal object mode. Copying the retained
  object's old mode prevents an update from silently unlocking or unhiding it.

## 测试记录

Full commands and outputs are recorded in
`Project_Test/260903_TEST_pcupdate-locked-managed-objects/RESULTS.md`.

- Focused Debug/Release API and source-contract regressions: passed.
- Existing PCUpdate Debug/Release regression: passed.
- Existing PCUpdate Undo Debug/Release regression: passed.
- Existing PC command registration Release regression: passed.
- PanelCladdingEditor Debug/Release builds: passed, 0 warnings, 0 errors.
- Solution Debug/Release builds: passed, 0 warnings, 0 errors.
- Package `1.0.72` build: passed; 28 files.
- Isolated registry install/validate/repair regression: passed.
- Release assembly GUID/cross-product uniqueness gate: passed.
- Direct/package RHP SHA-256:
  `B1C731FF3596CA8263F334C2E0C9D9D809273775AD487B18E792C89951A08805`.
- `git diff --check`: passed with existing line-ending notices only.

## 验收判据对齐

- Locked/hidden retained dependencies use mode-bypassing replacement: passed.
- Locked/hidden stale/duplicate dependencies use mode-bypassing deletion: passed.
- Retained object-level mode is preserved: passed.
- No unlock/show or layer-state workaround: passed.
- PID/CID/kind/root/reference boundaries and one-command Undo behavior remain: passed by existing
  regressions and source construction.
- Package/identity/registration gates: passed.
- Native installed fixture: pending host-side verification.

## 回退验证

No live document or production plug-in installation was changed. Reverting the mode-aware
replace/delete calls, retained-mode copy, test/exclusion, README note, and version restores the
prior implementation. The currently installed prior version remains untouched and reachable.

## 当前遗留项

- Install `PanelCladdingEditor` `1.0.72` from ordinary host PowerShell while Rhino is closed.
- In Rhino, verify `_PCUpdate` with retained and stale/duplicate dependencies spanning object-locked,
  object-hidden, locked-layer, and hidden-layer states, then verify one-step Undo and preserved modes.

## 结论

`PCUpdate` now treats lock and visibility modes as presentation/protection state rather than a
reconciliation blocker. It updates retained managed geometry and deletes stale/duplicate managed
geometry through Rhino's explicit mode-bypass APIs while preserving retained object mode and
leaving layer state unchanged. The corrected build is packaged as version `1.0.72` for host-side
installation and live verification.
