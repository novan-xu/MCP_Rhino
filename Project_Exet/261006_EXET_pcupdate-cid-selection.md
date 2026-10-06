# PCUpdate CID selection EXET

## Corresponding plan

[PLAN](../Project_Plan/261006_PLAN_pcupdate-cid-selection.md).
Execution date: 2026-10-06.

## Related artifacts

[TEST commands and results](../Project_Test/261006_TEST_pcupdate-cid-selection/README.md)
and [verification summary](../Project_Test/261006_TEST_pcupdate-cid-selection/verification-summary.json).
No commit or PR was requested or created. Earlier PCCreate/editor changes and
pre-existing untracked files were preserved.

## Implemented scope

PCUpdate validates selected Breps/PIDs, enters one Undo scope, and writes canonical
parent/child CIDs before re-reading and grouping the selection. CID comparison
trims whitespace and ignores case across the entire selection. Every member of a
duplicate group is skipped; all unique-CID sources proceed. Source CID corrections
also apply to skipped panels. An all-duplicate batch succeeds without dependency
mutations. Processed counts reflect only the panels that were updated.

Application selection logic and Domain result models expose duplicate groups.
Infrastructure prepares geometry only for processable sources, considers all
selected sources when resolving dependency ownership, excludes source Breps from
the dependency scan, and leaves only duplicate-group panels selected after a
successful mixed/all-duplicate batch. The command reports skipped counts and CIDs.

Legacy unsuffixed dependencies with multiple possible selected owners are retained
and reported. Dependencies of skipped or unselected opposite-role panels are
excluded; single-source legacy migration remains supported. Existing expected-CID
validation and mode-aware replacement/deletion remain active.

## Deviations from plan

None. Existing focused smoke projects were extended rather than adding another
compiled test project. Version 1.0.83 includes the earlier dimension, default
segmentation, and editor mask-preservation fixes.

## Issues found and fixed

Removing the duplicate-PID guard alone would still fail in a PID-keyed dictionary
and could admit skipped siblings' dependencies into reconciliation. Both the
selection guard and lookup now handle shared PIDs. Normalization previously ran
after geometry mutation; its transaction now precedes collision detection and
planning. Ambiguous legacy objects cannot safely be deleted or assigned by PID
alone, so they are retained and counted explicitly.

## Test record

Exact commands and coverage are recorded in TEST. Update-command tests passed in
Debug and Release. Role-CID, command-Undo contract, and locked/hidden-object API
contract regressions passed in Debug. Standalone Debug/Release builds and Release
packaging passed with zero warnings/errors. Narrow builds are appropriate because
no MCP host, Router, tool surface, or transport changed.

Compiled assembly identity passed before packaging and against the staged RHP;
all 27 bundled file hashes matched. Staged RHP SHA-256:
`afc101986bff3c889b7717eb2644b0bfc92d70d9d873d6d7a180070f0150d1d9`.
Final `git diff --check` passed.

## Acceptance alignment

Shared-PID parent/child panels have distinct corrected CIDs and both qualify.
Duplicate CID groups are excluded while unique groups proceed. The all-duplicate
case has an empty dependency plan. Skipped and ambiguously owned dependencies
are protected. Selection/reporting and mutation order are covered by source
contracts; native Rhino acceptance remains pending.

## Rollback verification

CID changes and dependencies participate in the same service-owned or ambient
command Undo scope. The existing contract verifies service ownership guards and
rollback after EndUndoRecord. Native Rhino Undo was not executed. No installed
RHP or registry was changed, so deployment rollback is unnecessary; this change
can be reverted independently of the earlier fixes.

## Remaining items

Native Rhino verification of actual geometry, selection, and Undo remains pending.
The package is staged at
`%LOCALAPPDATA%\PanelCladdingEditor\staged\1.0.83-pcupdate-cid-261006`.
Production activation was not attempted because independent host registry
attestation has not been established, as required by AGENTS.md. The prior
installed RHP remains reachable and unchanged.

## Conclusion

PCUpdate now normalizes role CIDs, processes unique selected CIDs, and leaves
duplicate-CID panels selected for inspection. Implementation and automated
verification are complete; version 1.0.83 is staged, not installed.
