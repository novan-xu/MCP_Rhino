# PCSyncSrf Offset Refresh PLAN

## Background

`PCSyncSrf` currently preserves the entire stored H/V offset set and unions it with offsets inferred from the edited cladding surfaces. That conservative rule protects non-splitting structural tracks, but it also preserves obsolete cladding-boundary positions. In the reported Level 5 panel, stored `H0=21.375` and `H1=107.25` remain while edited surface boundaries `19.625` and `105.75` are added, producing duplicate tracks and an invalid expanded cell grid.

The commit writer already deletes the old indexed offset keys before writing the planned sequence. The defect is therefore in offset planning, not user-text replacement.

## Goal

- Make current cladding-surface boundaries authoritative for previously splitting cladding tracks during `PCSyncSrf`.
- Replace moved stored H/V values instead of retaining the old values and appending new ones.
- Preserve stored tracks only when the saved cladding owner graph proves that cladding spans the complete track, meaning the track is structural/non-splitting.
- Continue preserving and refreshing structural tracks from associated extrusion curves without allowing stale curves at replaced cladding boundaries to reintroduce obsolete offsets.
- Leave `PCSyncCrv` behavior unchanged.

## Architecture ownership

- Deterministic owner-graph classification and offset reconciliation belong to `PanelCladdingSurfaceSyncPlanningService` in Application.
- Live Rhino geometry inference and the production call site remain in `LivePanelCladdingSurfaceSyncRepository` in Infrastructure.
- Regression coverage belongs in `Project_Test/260825_TEST_pcsync-srf-offset-refresh/`.

## Key design

1. Decode the saved material-independent `CW_2.08_CLADDING_LOGIC` graph already stored on the panel.
2. Classify each stored H/V track as non-splitting only when every pair of adjacent cells across that full track resolves to the same cladding owner.
3. In surface scope, start from current surface-inferred offsets, not from every stored offset.
4. Reconcile only the proven non-splitting tracks with associated curves; preserve their stored position when no curve exposes them, and accept a current curve position when one does.
5. Ignore curve positions aligned to old splitting tracks so stale spawned curves cannot re-add a cladding boundary that the edited surfaces moved.
6. Add genuinely new unmatched curve tracks and de-duplicate the final sorted offsets within model tolerance.
7. If no valid saved owner graph is available, retain the existing conservative preservation behavior rather than risking structural data loss; invalid logic is still rejected by the existing sync planner.

## Files involved

- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSurfaceSyncPlanningService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSurfaceSyncRepository.cs`
- `Project_Test/260825_TEST_pcsync-srf-offset-refresh/`
- `Project_Exet/260825_EXET_pcsync-srf-offset-refresh.md`

## Usage

1. Edit the height or width of associated cladding surfaces.
2. Select the source panel and run `PCSyncSrf`.
3. The panel's indexed H/V offset set is rewritten from the refreshed grid in the same Rhino Undo record.

## Acceptance criteria

- Stored horizontal offsets `21.375, 107.25, 169.09079` plus edited surface offsets `19.625, 105.75, 169.09079` resolve to exactly `19.625, 105.75, 169.09079` when the old tracks split cladding.
- The obsolete values `21.375` and `107.25` do not survive under any new H index.
- A full non-splitting structural track remains when no surface boundary or curve exposes it.
- A structural track follows an edited associated curve position.
- A stale curve at a replaced cladding split does not reintroduce the old offset.
- A genuinely new unmatched curve track remains available to the surface-sync grid.
- Existing structural-grid, surface-sync, parent-persistence, and offset-sync regressions pass.
- Focused Debug and Release builds/tests pass; package/installation is only required if deployment artifacts are changed.

## Risks and rollback

- Owner-graph classification depends on the saved `CW_2.08_CLADDING_LOGIC` payload. Missing legacy payloads use conservative preservation, while malformed payloads continue to fail sync before mutation.
- Ambiguous edits that simultaneously change the cladding partition count and structural curve layout are resolved by explicit owner roles, then by the existing ordered axis correspondence.
- Roll back by restoring the prior all-stored-offset surface union and the previous live repository call signature; Rhino Undo remains available for document mutations already performed.

## Future extensions

- Add a `PCSyncSrf` diagnostic report that labels each resulting offset as surface, structural stored, or structural curve-derived.
