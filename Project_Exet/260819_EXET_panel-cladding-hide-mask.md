# Panel Cladding Hide Mask Execution Report

## Corresponding plan

- Plan: `Project_Plan/260819_PLAN_panel-cladding-hide-mask.md`
- Execution date: 2026-08-19

## Related artifacts

- Tests: `Project_Test/260819_TEST_panel-cladding-hide-mask/`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.56/`
- Installed RHP: `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.56\PanelCladdingEditor.rhp`
- Commit / PR: none created

## Execution result

### Topology contract

- Added canonical panel key `CW_2.07_HIDE_MASK`.
- Added `HiddenSegments` to the domain topology state and `HideMask` to the encoded payload set.
- The hide payload uses the same dimension header and atomic-segment ordering as the segment mask, with a distinct payload kind.
- Missing and hidden atoms are mutually exclusive.
- Merge runs may be completely visible or completely hidden; partially hidden merge runs fail closed.
- Existing two-mask panels remain readable and default to no hidden atoms.

### Editor behavior

- Added **Hide** beside **Merge** and **Explode**.
- Hiding atomic segments does not call logical-assignment reset or full-track normalization.
- Hidden segments keep H/V offsets, logical cell divisions, cell names, and material assignments.
- Hidden segments remain selectable as dashed orange lines with `HIDDEN` labels.
- When all selected atoms are hidden, the action changes to **Unhide**.
- Hiding/unhiding a previously merged extrusion retains the merge run.
- Delete remains distinct: deleting hidden atoms removes their hide state and continues to merge/collapse logical topology as before.
- Undo snapshots and inserted-mullion remapping include hidden state.

### Persistence and downstream commands

- Save Extrusions and Save Both write the segment, merge, and hide payloads.
- Cladding-only save remains scoped and does not mutate topology masks.
- Type identity now includes the hide payload so panels with different physical extrusion suppression do not share an identity accidentally.
- `PCSpawnCrv` omits hidden atomic segments while retaining all four frame curves.
- `PCMatchCrv` copies all three masks and still ignores numerical offset values when grid dimensions match.
- `PCCreate` writes an explicit empty hide payload alongside its inferred topology.
- `PCSyncSrf` preserves hide state on a retained grid.
- `PCSyncCrv` preserves stored hidden-track offsets and reclassifies absent spawned curves back to hidden atoms when the grid maps safely; drawing a real curve at a formerly hidden atom unhides it.

## Deviations from plan

- No production-scope deviation.
- The formal type-signature prefix remains v4 for compatibility, but the canonical v4 payload now includes a `hidden=` field just as it already includes segment and merge topology.
- Direct live verification on `PID_BKT_N1_01_07` was not run because Rhino was closed for installation. The exact B/C hide condition is covered by the editor interaction smoke fixture.

## Problems found and fixed during construction

- Several older UI regressions still expected the previously retired Exit button; their assertions were updated to Save Extrusions / Save Cladding / Save Both and the current Delete danger style.
- Existing curve-match and create tests assumed exactly two topology writes; they now validate the three-mask contract.
- The server project explicitly links test sources, so the new standalone test directory had to be excluded from the production compile glob.
- A parallel Release solution build exposed the known dual-output race between the RHP project and its test-host DLL reference. Deterministic validation uses `-m:1`, which passed with zero warnings and errors.
- The first hide implementation would have exploded a merged curve. It was corrected so fully hidden merge runs survive Hide/Unhide.

## Test record

- Focused hide-mask smoke: Debug passed; Release passed.
- Final broad regression: all 40 panel-cladding / PCMatch / PCSync projects passed.
- Debug solution build: passed, 0 warnings / 0 errors.
- Release solution build: passed, 0 warnings / 0 errors.
- Package build and manifest validation: passed.
- Assembly GUID identity validation: passed; MCP_Rhino and PanelCladdingEditor have distinct non-empty IDs.
- Registry-only installer validation: passed.
- Packaged and installed RHP hashes match: `F3E1F33CE34B66913368E55FA249595B9E49318ABE9F117F1EF69AADE9553C28`.

## Acceptance alignment

- New `CW_2.07_HIDE_MASK`: passed.
- Hide action beside Merge/Explode: passed.
- Keep material-only cell divisions: passed.
- Suppress physical extrusion curves: passed.
- Preserve offsets/cell numbering: passed.
- Reversible editor state: passed.
- Save/load/match/sync/type integration: passed.
- Backward two-mask compatibility: passed.
- Package and current-user installation: passed.

## Rollback verification

- Reinstall package `1.0.55` to restore the prior plug-in.
- `CW_2.07_HIDE_MASK` is additive user text; older versions ignore it.
- No Rhino document or object was modified during construction/testing.

## Current remaining item

- Restart Rhino, open the BKT model, hide `INT_B0`, `INT_B1`, `INT_C0`, and `INT_C1` on `PID_BKT_N1_01_07`, then use Save Extrusions or Save Both for final live-model confirmation.
