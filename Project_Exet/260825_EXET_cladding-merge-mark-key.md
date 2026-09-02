# Cladding Merge Mark Key EXET

## Corresponding plan

- Plan: `Project_Plan/260825_PLAN_cladding-merge-mark-key.md`
- Execution date: 2026-08-25

## Related artifacts

- Tests: `Project_Test/260825_TEST_cladding-merge-mark-key/`
- Test record: `Project_Test/260825_TEST_cladding-merge-mark-key/RESULTS.md`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.66/`
- Installed RHP: `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.66\PanelCladdingEditor.rhp`
- Commit / PR: not created in this execution.

## Execution result / actual delivered scope

- Renamed the canonical baked cladding coverage key to exactly `Merge_Mark`.
- Retained `CW_1.03_CLADDING_CELLS` only as a legacy migration alias; no new spawn or sync output uses that key.
- `PCSpawnSrf` writes `Merge_Mark` for every baked cladding surface, including singleton marks such as `0A` and merged marks such as `0A;1A`.
- `PCSyncSrf` reads canonical and legacy values independently. Canonical-only data is used directly, legacy-only data schedules migration, equal dual-key data schedules legacy cleanup, and differing dual-key data fails before mutation.
- Sync commit deletes all casing variants of both names and writes only canonical `Merge_Mark`.
- Existing unmarked-surface behavior remains strict: geometry-missing stale offsets are cleared unless complete merged coverage explicitly proves a hidden track.
- Migrated all `1,495` legacy-marked cladding Breps in the open BKT Wireframe document to `Merge_Mark`, with value verification before deleting the former key.
- Bumped, built, packaged, identity-validated, installed, and registry-validated PanelCladdingEditor `1.0.66`.

## Differences from the plan

The live metadata-write phase was previewed before mutation. The runtime surface does not provide a separate preview-only deletion tool, so legacy deletion was executed only after all `1,495` canonical values had been reread and verified. The final read confirmed zero legacy keys and exact canonical-value preservation. This maintains the plan's data-safety requirement while using the available deletion contract.

## Problems found and fixed during construction

- A `CW_1.03` prefix incorrectly implied that merge coverage belonged to the regular cladding-cell key family. The purpose-specific `Merge_Mark` name now separates baked geometry topology from panel cell-value attributes.
- A blind rename would either strand old documents or permit two conflicting authorities. The resolver now distinguishes canonical-only, legacy-only, equal dual-key, and conflicting dual-key states explicitly.
- Leaving a legacy key after a sync would allow ambiguity to recur. `CoverageChanged` now includes any non-empty legacy value, forcing cleanup even when the canonical value is already correct.
- Spawn behavior could have drifted independently from sync. Both use the same canonical constant, and a focused regression asserts that spawn output never contains the old key.
- The open model contained `1,495` legacy marks, more than the subset originally added for merged panels. Migrating all marked cladding surfaces preserves a complete, uniform surface partition rather than creating a mixed-key document.

## Test record

Focused Debug and Release regression runs both exited `0`. Eight related managed regression projects also passed, covering surface coverage, unmarked clearing, exact offset refresh, spawn, surface sync, parent persistence, and structural-grid behavior. The one documented Rhino-native Brep inference check skipped outside a native host; its managed source and planning assertions passed.

Debug and Release standalone RHP builds both completed with zero warnings and zero errors. Package `1.0.66` built successfully. Assembly identity validation confirmed the non-empty PanelCladdingEditor GUID `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, distinct from MCP_Rhino GUID `7a3fc2f0-24a8-4b79-be58-5a08cfb0d10a`. The packaged RHP SHA-256 is `61199036FDE03AA10589ACF028ED74867E95B61C849ED8C8BCCE3C6C7C6B8E22`.

The live migration found, wrote, verified, and deleted `1,495` legacy values with zero conflicts, zero failures, and zero legacy keys remaining. The runtime activity was recorded through `append_activity_log`.

After Rhino exited, registry-only installation and installer `Validate` mode succeeded. Packaged and installed RHP SHA-256 values match exactly. Both HKCU registration paths target the `1.0.66` RHP with `LoadMode=1`, `DirectoryInstall=0`, and `IsDotNETPlugIn=1`; installed assembly identity validation also passed.

## Acceptance criteria alignment

- Canonical constant equals exactly `Merge_Mark`: passed.
- `PCSpawnSrf` writes `Merge_Mark` and never the former key: passed.
- `PCSyncSrf` handles canonical-only, legacy-only, equal dual-key, and conflict cases correctly: passed.
- Legacy-only and equal dual-key states schedule cleanup: passed.
- Conflicting values fail before mutation: passed.
- Commit removes both names and persists only `Merge_Mark`: passed.
- Coverage, offset-refresh, unmarked-clearing, spawn, sync, parent, and structural-grid regressions: passed, subject only to the documented native-host skip.
- Current open document preserves all `1,495` values under `Merge_Mark` and contains zero former keys: passed.
- Debug/Release builds, package construction, and assembly identity validation: passed.
- Registry-only installation, hash equality, registry ownership, and installed assembly identity for `1.0.66`: passed.

## Rollback verification

Before the document is saved, the live migration can be reversed with the two Rhino Undo records: first restore the legacy-key deletion, then undo the canonical writes. After saving, rollback requires restoring a document backup or applying the inverse metadata migration. Code rollback consists of restoring the previous canonical constant and reinstalling the prior RHP.

## Current remaining items

- Restart Rhino to load `1.0.66` and register its commands for the new session.
- The migration did not invoke Rhino Save automatically. Persistence depends on the save choice made when the user closed the document; no disk `.3dm` fallback was used to infer that choice.

## Conclusion

`Merge_Mark` is now the sole output key for baked cladding coverage, including `PCSpawnSrf`, while `PCSyncSrf` safely migrates old data and rejects conflicting authorities. The live document migration is verified, and version `1.0.66` is built, installed, hash-matched, identity-validated, and registered for the next Rhino launch.
