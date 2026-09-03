# PCSpawnCrv Curve Simplification and Layer Routing Execution

## 对应计划

- Plan: `Project_Plan/260903_PLAN_pcspawncrv-curve-simplification.md`
- Execution date: 2026-09-03

## 关联产物

- Tests: `Project_Test/260903_TEST_pcspawncrv-curve-simplification/`
- Commit / PR: not created in this execution

## 执行结果 / 实际落地范围

- Replaced the single `Curves-PNL::Main Frame` destination with deterministic source-layer mapping:
  `01_CW Panels::Surfaces-PNL::<suffix>` becomes
  `02_CW Extrusions::Curves-PNL::<suffix>`.
- Passed the source panel's `LayerFullPath` into extrusion planning and stored the resolved path on
  every curve plan.
- Changed live spawning to resolve/cache output layers per planned path, allowing one selected batch
  to contain multiple panel types.
- Changed curve sync discovery from one exact legacy layer to the complete `Curves-PNL` subtree and
  regenerated expected plans using each panel's source layer.
- Added a RhinoCommon simplification stage after edge joining. It uses `Curve.Simplify(All)` followed
  by degree-3 tolerance-bounded `Curve.Fit`, retaining the fitted result only when its NURBS control
  point count is lower.
- Updated all affected regression fixtures for the explicit source-layer planning contract.

## 与计划的偏差

- The planned native curve-reduction assertion could not execute in the standalone CLI host on this
  machine because RhinoCore initialization failed with `COMException 0x80004005 (E_FAIL)`. The test
  remains implemented behind `--rhino-geometry`; default Debug/Release runs record it as skipped.
- No migration command was added for pre-existing curves on `Curves-PNL::Main Frame`; as planned,
  existing geometry is not moved by this change.

## 施工中发现并修复的问题

- Curve sync originally filtered for exact equality with the legacy `Main Frame` path. Without
  widening discovery, newly spawned type-layer curves would have disappeared from `PCSyncCrv`.
- Spawn originally resolved one output layer for the entire batch. It now caches one Rhino layer
  index per planned path so mixed panel types route correctly.
- The simplification helper defensively duplicates the source if RhinoCommon ever returns the same
  curve instance from `Simplify`, preventing the returned curve from being disposed with the joined
  source.

## 测试记录

Detailed commands and outputs are recorded in
`Project_Test/260903_TEST_pcspawncrv-curve-simplification/RESULTS.md`.

- Debug plug-in build: exit `0`, 0 warnings, 0 errors.
- Release plug-in build: exit `0`, 0 warnings, 0 errors.
- Focused Debug smoke: exit `0`; layer routing assertions passed; native case skipped.
- Focused Release smoke: exit `0`; layer routing assertions passed; native case skipped.
- Five affected historical regression smokes: all exit `0`.
- `git diff --check`: exit `0` (line-ending warnings only).

## 验收判据对齐

- Exact `WT-04` mapping: passed.
- Nested suffix preservation: passed.
- Unsupported/root-only source layer rejection: passed.
- Per-curve plan layer propagation: passed.
- Mixed-type implementation path: covered by per-path layer caching and compilation; a live mixed
  Rhino document was not exercised in this execution.
- Control-point reduction within tolerance: implementation and opt-in assertion are present; live
  native execution remains outstanding because the standalone RhinoCore host did not initialize.
- Existing extrusion topology/assignment/sync regressions: passed.

## 回退验证

No document mutation or installation was performed during construction. Reverting the four
production changes restores legacy joined-edge geometry and the single `Main Frame` destination;
the change introduces no persistent schema migration.

## 当前遗留项

- Run the opt-in native geometry assertion, or perform a live `PCSpawnCrv` check on a curved panel,
  in a Rhino host where native initialization is available. Confirm visible control-point reduction
  and destination-layer placement for at least `WT-04`.

## 结论

The requested `PCSpawnCrv` behavior is implemented and all available application/build regressions
pass. The only unclosed verification item is direct native/live Rhino execution of the curve
simplification deviation check; it is explicitly retained and documented rather than treated as a
passing standalone test.

## Deployment verification (2026-09-03)

After Rhino was closed, `PanelCladdingEditor` 1.0.69 was rebuilt and installed registry-only at
`C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.69`. The required installer
`-Mode Validate` gate passed. The installed RHP SHA-256 exactly matched the fresh bundle
(`28cb5e0adf66af5300a6b3fd885fd216771c91044fc53cf2dfd3922307de9e01`), and direct assembly
metadata inspection confirmed GUID `7C1A4D3B-5E29-4F68-9A72-1D8C6B0F4E35`.
