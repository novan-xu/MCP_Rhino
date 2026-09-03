# Panel Cladding Curve Display Colors Execution

## 对应计划

- Plan: `Project_Plan/260903_PLAN_pc-curve-display-colors.md`
- Execution date: 2026-09-03

## 关联产物

- Tests: `Project_Test/260903_TEST_pc-curve-display-colors/`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.73/`
- Commit / PR: not created in this execution

## 执行结果 / 实际落地范围

- Added one authoritative curve-color resolver to extrusion planning:
  - main frame: Blue (`RGB 0,0,255`);
  - horizontal intermediate: Purple (`RGB 128,0,128`);
  - vertical intermediate: DarkGreen (`RGB 0,100,0`).
- Stored the resolved RGB on every extrusion curve plan, with `Kind = Frame` taking precedence over
  horizontal/vertical axis.
- Changed newly baked `PCSpawnCrv` curves and both retained/new `PCUpdate` curves to use
  `ObjectColorSource.ColorFromObject` with the planned RGB. Surface color handling remains
  `ColorFromLayer`.
- Extended `PCSyncCrv` snapshot/planning/commit so existing selected-panel curves are marked changed
  when either the object-color source or RGB is wrong and are repaired without rebuilding geometry.
- Kept curve geometry, layer routing, PID/CID metadata, selected-panel scope, locked/hidden handling,
  and Undo ownership unchanged.
- Bumped the standalone plug-in package to `1.0.73`, built the corrected bundle, and added a focused
  regression plus one precise standalone-test exclusion in the MCP server project.

## 与计划的偏差

No implementation scope changed. A native viewport fixture was not executed from the agent context;
production activation and live Rhino verification remain subject to the repository's independent
host-registry attestation rule.

## 施工中发现并修复的问题

- The extrusion plan already has both curve kind and axis, so no geometry-orientation heuristic was
  needed. This also guarantees that vertical side members of the main frame stay Blue.
- Existing curve synchronization previously compared only metadata. Current color source/RGB and
  desired RGB now participate explicitly in curve change detection.
- Object colors would be ignored while `ColorFromLayer` remained active. Each curve write therefore
  sets both the RGB and `ColorFromObject`; surface writes are kept on the previous layer-color path.
- `PCUpdate` rebuilds authoritative attributes for retained objects, so the same planned RGB applies
  to existing as well as newly created dependencies while the retained locked/hidden object mode is
  preserved by the earlier mode-handling patch.

## 测试记录

Full commands and outcomes are recorded in
`Project_Test/260903_TEST_pc-curve-display-colors/RESULTS.md`.

- Focused Debug/Release color mapping, propagation, drift, and write-contract regressions: passed.
- Existing extrusion sync, PCUpdate, Undo, locked/hidden, and PC command Debug/Release regressions:
  passed.
- PanelCladdingEditor Debug/Release builds: passed, 0 warnings, 0 errors.
- Solution Debug/Release builds: passed, 0 warnings, 0 errors.
- Package `1.0.73` build: passed; 28 files.
- Isolated registry install/validate/repair regression: passed.
- Release assembly GUID/cross-product uniqueness gate: passed.
- Direct/package RHP SHA-256:
  `DCD529C51FC829C297239494F6D360C0456B595D9D653C04A5C14294FC31BFD2`.
- `git diff --check`: passed with existing line-ending notices only.

## 验收判据对齐

- Main-frame curves resolve to exact Blue regardless of axis: passed.
- Horizontal and vertical intermediate curves resolve to exact Purple/DarkGreen: passed.
- Spawn/update curve attributes use planned object color while surfaces remain layer-colored: passed.
- Sync detects wrong source/RGB and commits the authoritative object color: passed.
- Existing Undo and locked/hidden PCUpdate behavior remains passing: passed.
- Package, solution, identity, registration, and hash gates: passed.
- Native Rhino viewport check: pending host-side verification.

## 回退验证

No live document or production plug-in installation was changed. Reverting the color fields,
resolver, Rhino attribute reads/writes, focused regression/exclusion, README note, and version bump
restores the prior behavior. The currently installed prior version remains untouched and reachable.

## 当前遗留项

- Install `PanelCladdingEditor` `1.0.73` from ordinary host PowerShell while Rhino is closed.
- In Rhino, run `_PCUpdate` or `_PCSyncCrv` on selected panels containing existing managed curves and
  confirm Blue main frames, Purple horizontal intermediates, and DarkGreen vertical intermediates.
- Confirm locked, hidden, locked-layer, and hidden-layer existing curves are recolored while their
  modes and layer states remain unchanged.

## 结论

Panel-cladding curve color is now an authoritative planned attribute rather than a shared layer
display. New and reconciled curves receive the requested Blue/Purple/DarkGreen classification, and
`PCSyncCrv` repairs color drift on existing selected-panel curves. The verified build is packaged as
version `1.0.73` for host-side installation and live verification.
