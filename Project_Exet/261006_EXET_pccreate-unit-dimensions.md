# PCCreate unit dimensions EXET

## Corresponding plan

[PLAN](../Project_Plan/261006_PLAN_pccreate-unit-dimensions.md).
Execution date: 2026-10-06.

## Related artifacts

[TEST instructions and results](../Project_Test/261006_TEST_pccreate-unit-dimensions/README.md)
and [verification summary](../Project_Test/261006_TEST_pccreate-unit-dimensions/verification-summary.json).
No commit or pull request was requested or created. Pre-existing untracked files
were preserved.

## Implemented scope

PCCreate's existing application planner now writes CW_2.00_UNIT_DIMENSION,
CW_2.01_UNIT_WIDTH, and CW_2.02_UNIT_HEIGHT from each panel's validated local
width/height. It reuses the key service's invariant five-decimal formatter, with
`widthxheight` for the combined value. Prior spellings/values of these keys are
deleted before canonical writes in the existing attribute commit.

The live geometry reader, command selection flow, H/V inference, topology, and
commit/rollback implementation are unchanged. Updated package documentation and
staged version 1.0.80. Documented the current surface/curve sync distinction for
the user's repeated PCSyncSrf question; neither sync command was modified.

## Deviations from plan

None. Production activation was not part of this scoped change and was not
attempted. No Windows UI automation was used.

## Issues found and fixed

The planner already calculates width and height for grid validation, but omitted
their user-text writes. The extended create smoke reproduced this omission before
the fix. Canonical replacement also removes old mixed-case dimension spellings
instead of leaving a second differently cased attribute.

## Test record

Exact reproducible commands are in the linked TEST README. The extended create
smoke passed in Debug and Release. Existing curve-topology and sparse-topology
smokes passed in Debug. Standalone Debug and Release RHP builds passed with zero
warnings/errors. These product-level builds are sufficient because the MCP
server, Router, host, routing, and registration surface were untouched.

Direct compiled assembly identity verification passed before packaging and again
on the staged RHP. The panel plug-in's nonempty assembly GUID matches the manifest
and class declaration and differs from MCP_Rhino. Release package publication and
rebuild passed; all 27 staged bundle file hashes passed. `git diff --check` passed.

## Acceptance alignment

Missing and stale dimensions, mixed-case cleanup, panel-local extent measurement,
multi-panel independence, decimal rounding/culture invariance, H/V preservation,
and unrelated metadata preservation passed. Existing topology regressions passed.
Version 1.0.80 is built and staged at
`%LOCALAPPDATA%\PanelCladdingEditor\staged\1.0.80-pccreate-dimensions-261006`.

## Rollback verification

The tests apply deletes/writes to an attribute map and check the resulting state.
Native Rhino Undo/rollback was not executed. The existing live commit/rollback
path is unchanged. Source rollback consists of reverting this task's narrow
planner, regression, manifest, and documentation edits. Staging made no installed
file or registry changes, so production deployment rollback was unnecessary.

## Remaining items

Production installation/independent registration attestation and native Rhino
command/Undo validation have not been performed. Do not report this staged build
as the loaded plug-in. No live model was changed.

## Conclusion

The requested PCCreate dimension fix is implemented, regression-tested, built,
identity-verified, and staged. Installation remains pending.
