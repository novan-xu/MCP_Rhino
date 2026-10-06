# PC editor offset layout preservation EXET

## Corresponding plan

[PLAN](../Project_Plan/261006_PLAN_pc-editor-offset-layout.md).
Execution date: 2026-10-06.

## Related artifacts

[TEST commands and results](../Project_Test/261006_TEST_pc-editor-offset-layout/README.md)
and [verification summary](../Project_Test/261006_TEST_pc-editor-offset-layout/verification-summary.json).
No commit or PR was requested or created. Earlier PCCreate fixes and pre-existing
untracked work were preserved.

## Implemented scope

Direct H/V edits and row/column dimension redistribution now capture the current
indexed topology before changing offsets. A shared editor method regenerates
coordinate-based display IDs from those same masks and rebuilds the working
layout. Cell material/parent values and indexed frame assignments remain intact.
Existing validation, dimension constraints, dirty-state/save flow, and Undo
snapshots are retained. Explicit track insertion/deletion behavior is unchanged.

Updated documentation and staged version 1.0.82, including this behavior fix plus
the earlier PCCreate dimension and segmented-default fixes.

## Deviations from plan

None. Verification uses the established in-process WPF test host and a fake
repository; no Windows UI automation or live Rhino mutation was used.

## Issues found and fixed

Both numeric-edit methods explicitly cleared merge, deleted, and hidden extrusion
state. Moreover, simply omitting those clears would leave state keyed by old
physical offsets. Capturing axis/track/bay masks first and reapplying them at the
new coordinates addresses both causes. The updated test failed before the fix on
the first H edit, then passed across both edit paths and both save workflows.

## Test record

Exact commands are in TEST. Topology-persistence passed in Debug and Release with
eight edit/save combinations per configuration, checking identical masks,
cladding materials/parents, profiles/modifiers, Undo, saved offsets, and reload.
Scoped-save, hide-mask, PCCreate, and curve-topology Debug regressions also passed.
The test fake was corrected to preserve parsed FrameAssignments on reload so
profile persistence is exercised end to end.

Standalone Debug/Release builds and Release packaging passed with zero warnings
and errors. Narrow product builds are sufficient because no MCP host, Router,
tool registration, or transport changed. Direct compiled assembly identity passed
before packaging and on the staged RHP. All 27 bundle hashes matched, and final
`git diff --check` passed.

## Acceptance alignment

Coordinate edits preserve indexed delete, merge, and hide masks; cladding and
profile assignments remain attached to the same logical cells/segments. Undo and
both save workflows pass. Invalid/no-op offsets leave state and Undo untouched.
The combined build is staged at
`%LOCALAPPDATA%\PanelCladdingEditor\staged\1.0.82-offset-layout-261006`.

## Rollback verification

The new test exercises editor Undo after every edit type and verifies original
coordinates plus retained masks/assignments. Native Rhino Undo was not exercised.
No installed RHP or registry record changed, so deployment rollback is unnecessary.
Source rollback can revert this editor-specific follow-up without undoing either
PCCreate fix.

## Remaining items

Production installation/independent registry attestation and live Rhino validation
remain pending. No existing document has been modified by this task.

## Conclusion

H/V and dimension edits now preserve the mask-defined layout and assignments.
Implementation and regression verification are complete; version 1.0.82 is staged,
not installed.
