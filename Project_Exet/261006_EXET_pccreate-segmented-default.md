# PCCreate segmented default EXET

## Corresponding plan

[PLAN](../Project_Plan/261006_PLAN_pccreate-segmented-default.md).
Execution date: 2026-10-06.

## Related artifacts

[TEST](../Project_Test/261006_TEST_pccreate-segmented-default/README.md) and
[verification summary](../Project_Test/261006_TEST_pccreate-segmented-default/verification-summary.json).
No commit or PR was requested or created. Previous dimension-fix changes and
pre-existing untracked work remain intact.

## Implemented scope

Removed the create planner's automatic conversion of continuous guide coverage
into merged junctions/runs. Guide coverage still controls which atoms exist.
New topology therefore has no merges; sparse persistence omits CW_2.10_MERGE_MASK
and the existing reset removes stale masks. Downstream extrusion planning emits
individual intermediate segments. Perimeter frames, explicit editor/template
merging, matching, and sync behavior remain unchanged.

Updated the create and curve-topology expectations, added a persisted-attributes
to-extrusion-plan regression, and documented the corrected default. Version 1.0.81
is staged with both this correction and the preceding unit-dimension fix.

## Deviations from plan

None. Production installation and live document migration were not attempted.

## Issues found and fixed

BuildAxis had treated a guide spanning two neighboring atoms as a merge request.
A continuous full track consequently initialized all its junctions merged. The
old tests explicitly expected that result. The revised regression reproduced the
user's report before the implementation change, then passed with the intended
segmented default. Partial coverage and logical cell construction were preserved.

## Test record

Exact commands and local transcript filenames are in TEST. Six regression runs
passed: create and curve-topology in Debug/Release, sparse-topology in Debug, and
extrusion-sync in Debug. The 2H/2V regression verifies nine cells, twelve distinct
single-atom intermediate curves, zero merged curves, and four perimeter frames.
Partial topology, stale-mask reset, explicit merging, scoped synchronization, and
unit dimensions remain covered.

Standalone Debug and Release builds and Release packaging passed with zero
warnings/errors. Narrow builds are sufficient because the MCP host, Router,
registration, and tool surface did not change. Compiled assembly identity passed
before packaging and on the staged RHP; all 27 package hashes matched. Final
`git diff --check` passed.

## Acceptance alignment

Continuous and split guides initialize segmented. Missing geometry still yields
the existing missing atoms/logical cells, old masks are reset, dimensions persist,
and explicit merge behavior remains available. Package 1.0.81 is staged at
`%LOCALAPPDATA%\PanelCladdingEditor\staged\1.0.81-pccreate-segmented-261006`.

## Rollback verification

No installed RHP or registry record was changed. The production live commit and
rollback code are unchanged; native Undo was not exercised. To revert only this
follow-up, restore the create planner's merge inference and associated expectations
without reverting the preceding dimension writes. No deployment rollback was
necessary.

## Remaining items

Production activation with independent registry attestation and native Rhino
command/Undo validation remain pending. Existing live panels were not changed.

## Conclusion

The cause is identified, the segmented default is fixed and regression-tested,
and the combined 1.0.81 build is staged. Installation is not claimed.
