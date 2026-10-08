# PCpid planar measurement correction EXET

## Plan and artifacts

Executed 2026-10-07 under
[PLAN](../Project_Plan/261007_PLAN_panel-cladding-pid-planar-bounds.md).
Regression code, anonymous live-derived numeric fixture and logs:
[TEST](../Project_Test/261007_TEST_panel-cladding-pid-planar-bounds/).
No commit or PR created.

## Result and actual scope

PCpid now validates the actual face with Rhino TryGetPlane at the unchanged
document tolerance. LivePanelCladdingPidGeometryService measures detached outer
boundary coordinates in the accepted plane, preserving the original front sign,
then supplies four planar measurement corners. Polyline boundaries use all their
vertices; curved boundaries use detached curve extents. Projection affects only
measurement data, never the Rhino source geometry. Attribute/point-order writes,
whole-layer context, selected-only writes and transaction ownership are unchanged.

## Deviations and findings

The reported panel is a planar degree-1 2x2 surface with 90 by 180 extents and
floating-point coordinate noise far below its 0.00001 tolerance. Its live boundary
passes planning and point ordering. The old rejection occurred in the pure
planner checking synthetic box corners, after the actual face had passed Rhino's
planarity check. The precise native box/cache behavior was not observable through
existing read tools; this correction removes reliance on box thickness altogether.

## Tests

Verify-Regression.ps1 passed all five PID, scope, setup, point-order and focused
boundary suites in Debug and Release. Both serialized solution builds and direct
standalone RHP builds passed with zero warnings/errors. The exact Server TEST-glob
exclusion is the only Server change for this correction; no MCP surface changed.
The focused case verifies the reported boundary, 90.00000x180.00000 dimensions,
30 keys, point order and retained pure nonplanarity rejection.

A geometry-only native attempt failed at DLL initialization (rhcommon_c,
0x8007045A), before assertions. Earlier RhinoCore attempts also failed at startup.
Native assertions compile for 12 rotations/front/UV variants, trims, curved
boundaries, real warped/horizontal geometry and unchanged source geometry, but
remain unexecuted. No user document mutations were made during diagnosis/testing.

Package 1.0.93 publish/rebuild, compiled assembly GUID/manifest matching, distinct
product identities and all 27 bundle hashes passed. RHP SHA-256:
`c8c1a95f6ecae5f48882ff6ebcc7ea8cd4ac3908f809f8f005ab05ddde58b516`.
Bundle: `%LOCALAPPDATA%/PanelCladdingEditor/staged/1.0.93-planar-bounds-261007/PanelCladdingEditor-1.0.93`.
Git whitespace check passed.

## Acceptance and rollback

Measurement no longer treats box thickness as face geometry, and actual face
planarity remains a preflight gate before writes. Existing ID/dimension tests pass.
Native command/Undo acceptance remains pending. Reverting the geometry service
integration and manifest version restores the previous implementation; staged
packaging does not alter the installed RHP. Installation retains the prior RHP.

## Installation and remaining work

Installed 1.0.93 after confirming Rhino process count zero. A hidden host nonce
was independently verified through HKEY_USERS/current SID before activation and
then removed. Host Install and mandatory Validate passed; after that process exited,
a separate host Validate passed. Verify-Activation.ps1 independently confirmed the
new existing RHP path/hash, exact 12 command values, all three advanced registry
timestamps, and retained 1.0.92 rollback RHP hash. See TEST activation-summary.json.
Final Rhino process count was zero. Post-start exact loaded-module and registry
ownership checks, and interactive PCpid/Undo acceptance, await the next session.

## Conclusion

The measurement correction is implemented and installed, managed regressions and
build/package/independent activation checks pass. Native acceptance remains open.

## GitHub publication follow-up (2026-10-07)

Source commit: `e7b302502c62bb7e4a7ec21206bed57dbfa651dd`.
Pull request: [#10 — panel IDs, frame configuration, and catalogue controls](https://github.com/novan-xu/MCP_Rhino/pull/10).
