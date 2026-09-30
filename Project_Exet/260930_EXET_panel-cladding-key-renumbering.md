# Panel cladding key renumbering execution

Current final schema: `CW_2.10_MERGE_MASK`, `CW_2.11_HIDE_MASK`,
`CW_2.12_DELETE_MASK`, `CW_2.13_CLADDING_LOGIC`, `CW_2.14_CLADDING_TYPE`.
Source, the staged 1.0.78 package, and the live document reflect this final schema.
Production activation remains pending; the dated sections below preserve each pass.

## Corresponding plan

[PLAN](../Project_Plan/260930_PLAN_panel-cladding-key-renumbering.md).
Execution date: 2026-09-30.

## Related artifacts

[TEST and reproducible verification](../Project_Test/260930_TEST_panel-cladding-key-renumbering/README.md).
No commit or PR was requested or created. Pre-existing untracked work was preserved.

## Initial implemented scope

The initial key-service change mapped merge, hide, ownership logic, and cladding type
to `CW_2.10_MERGE_MASK`, `CW_2.11_HIDE_MASK`, `CW_2.12_CLADDING_LOGIC`, and
`CW_2.13_CLADDING_TYPE`. Existing save/load/create/match/spawn/sync/update/clear
consumers use these constants. The ownership JSON and binary hide mask remain
separate payload contracts. The logic name incorporates the user's follow-up correction.

Updated current packaging documentation and runnable regression expectations.
Built and staged version 1.0.78, verified compiled assembly identity and all 27
manifest hashes. The loaded 1.0.76 installation remains reachable and untouched.

Subsequently renamed 3,029 live attribute entries across 907 objects in the user's
selected document using existing bulk recipe tools. Counts by key: merge 748,
hide 531, logic 901, type 849. All old names are absent and values are preserved.

## Deviations

No implementation deviation. Production activation cannot complete while the old
RHP remains loaded and without independent host registry attestation. As planned,
the release is staged and the installation handoff is in TEST; installation is
not reported as complete.

## Issues found and handled

Pre-mutation validation detected unrelated concurrent document edits. No requested
source value changed. Refreshed the baseline before applying any recipe and verified
against that current state rather than overwriting or restoring unrelated work.
No production source defect or regression failure was found.

## Test record

The linked TEST records exact build/regression/package commands and live checks.
Standalone Debug and Release builds passed with zero warnings/errors. Seven
existing application suites passed, including save/type/workbook, topology,
hide, logic, clear, update, and type-code-format behavior. The packaged RHP assembly
GUID matches the class and manifest and is distinct from MCP_Rhino. All 27 staged
file hashes passed. Diff whitespace validation passed.

All four live recipes succeeded with zero failures. Complete readback verified
3,029 exact value transfers and preservation of 31,001 unrelated attributes.
Object count was 1,321 both immediately before and after migration. Names, IDs,
layers, and geometry-type metadata of all 907 affected objects were preserved.
The activity record was written through AppendActivityLogTool.

## Acceptance alignment

Source behavior, existing regressions, package staging, compiled identity, live key
renaming, and exact value preservation pass. Production installation, live use of
the new commands after restart, and manual Rhino Save remain pending.

## Rollback verification

All migration calls retain the existing native Undo contract, one record per key.
Undo was not exercised against the production document. Original values were
verified against the pre-apply in-session snapshot. Source rollback can revert
this task's narrow constant/documentation/manifest changes. Staging never modified
the active files or registration, so deployment rollback was unnecessary.

## Remaining items

Save the changed Rhino document. Close Rhino, install and validate the staged
bundle from ordinary user PowerShell, independently attest the registration,
then reopen Rhino and verify the exact loaded RHP and key timestamp behavior.
Do not use the old PC commands on the migrated document.

## Conclusion

The requested source behavior and live document renames are implemented and
verified. Version 1.0.78 is staged; production activation is explicitly pending.

## Follow-up correction (2026-09-30)

The user corrected `CW_2.12_HIDE_MASK` to `CW_2.12_CLADDING_LOGIC`. The canonical
constant, current documentation, and logic regression output now use the corrected
name. Debug and Release builds, the existing logic-persistence suite, assembly GUID
verification, and rebuilt package staging all passed. The same staged 1.0.78 bundle
was refreshed, and all 27 manifest file hashes were verified again.

The original Rhino session closed during this follow-up, then the same target
document reopened in a new routable session. Selected and re-read that session,
previewed the exact rename, and applied it through the existing bulk recipe tool.
All 901 interim keys became `CW_2.12_CLADDING_LOGIC`, with zero failures or old keys
remaining. Complete readback verified every value and 32,943 unrelated attributes;
object count remained 1,321, and object IDs, names, layers, and type metadata stayed
unchanged. See TEST/correction-summary.json for the verification summary. This
correction adds one native Undo record and still requires the user's Rhino Save.

The earlier migration record intentionally retains the interim name for audit
history. The final source, staged RHP, and live document all use the corrected name.
No production installation or registry write was performed; the restarted Rhino
process was observed loading the unchanged 1.0.76 RHP.

## Delete-mask and final numbering revision (2026-09-30)

The user requested three further renames: `CW_2.05_SEGMENT_MASK` to
`CW_2.12_DELETE_MASK`, `CW_2.12_CLADDING_LOGIC` to `CW_2.13_CLADDING_LOGIC`, and
`CW_2.13_CLADDING_TYPE` to `CW_2.14_CLADDING_TYPE`. Implemented all three in the
central key service, packaging documentation, and affected runnable test
expectations/instructions. Internal segment-mask models, payload encoding/polarity,
and type-generation algorithms are unchanged. No MCP surface or host change.

Standalone Debug and Release builds passed with zero warnings/errors. Re-ran the
seven initial regression suites plus the sparse-topology suite: all eight passed.
The sparse suite was re-run after updating its old diagnostic key numbers and
passed again. The compiled Release RHP assembly identity verifier passed before
packaging. Rebuilt and refreshed the same not-yet-activated 1.0.78 staged bundle;
all 27 manifest file hashes matched. No installed RHP or registry key was modified.

Selected the user's same saved live document and found no destination collisions.
Three previews matched 12 delete masks, 901 logic entries, and 849 type entries.
Immediately before mutation the complete preflight snapshot still matched. All
three existing bulk recipe calls succeeded with zero failures. Complete readback
verified 1,762 exact value transfers across 907 objects, no old names remaining,
and 31,361 unchanged unrelated attribute pairs. Object count remained 1,321;
IDs, names, layers, and geometry-type metadata were preserved. Results are in
TEST/final-numbering-summary.json. The runtime activity was logged through the
existing tool. No disk .3dm operation or Windows UI automation was used.

Each rename added one native Undo record; production Undo was not exercised.
The user must Save again. Production activation and independent registry/load
attestation are still pending under the existing AGENTS.md gate. The loaded RHP
was still 1.0.76 at this revision's preflight. No implementation deviations or
regression failures occurred.

## Lot key revision — 2026-09-30

Implemented the user's plug-in-only rename from `CW_1.05_RELEASE` to
`CW_1.05_LOT` under the third revision of the matching PLAN. The shared metadata
constant now directs surface/curve spawn, update, and sync to LOT; missing required
metadata reports `LOT_NUMBER_REQUIRED`. Internal release DTO names remain unchanged.
There is no old-key fallback and no Rhino document migration was performed.

Seven existing Debug regressions passed, including new literal-key, leading-zero,
conflicting-old-key, and missing-lot checks in the existing inheritance fixtures.
Standalone Debug and Release builds passed with zero warnings/errors. These narrow
builds are appropriate because MCP server/Router/host and tool registration were
not changed. Direct assembly GUID verification, Release packaging, all 27 bundle
hashes, and `git diff --check` passed. See the matching
[TEST follow-up](../Project_Test/260930_TEST_panel-cladding-key-renumbering/README.md)
and `lot-key-summary.json` for commands, suite names, and counts.

Version 1.0.79 was staged at
`%LOCALAPPDATA%/PanelCladdingEditor/staged/1.0.79-lot-key-260930`. Rhino initially ran
but closed during the work. A fresh service-launched host nonce probe established
persistent registry access before activation. The existing TEST host helper was
parameterized with an optional BundleRoot, preserving its original default, and
ran the product installer in Install then Validate modes. A separate host process
validated after the installer exited. The exact existing new RHP/hash, eleven
command entries, and advancement of all three registration timestamps passed;
the independent Windows registry provider also confirmed the new path.

The installer preserved 1.0.78 in its rollback directory and the prior registry
and ownership manifest were captured locally. Temporary probe keys were removed.
Installed RHP SHA256 is
`60c20aefb88f09d0c83d3b26ab911e9e3f42919f2c06d260de30da6eb69df411`.
No regression failures or product-design deviations occurred. The minor execution
change from staging to activation was enabled by Rhino closing and satisfies the
existing activation authorization and host-attestation requirements.

Implementation, tests, packaging, installation, and independent registration
validation are complete. Rhino remains closed; a new startup is required to load
1.0.79, and loaded-module/post-start timestamp confirmation remains pending that
startup. No new-version live PC command or document mutation is claimed.
