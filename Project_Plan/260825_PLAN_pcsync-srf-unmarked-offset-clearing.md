# PCSyncSrf Unmarked Offset Clearing PLAN

## Background

`PCSyncSrf` now supports baked cladding coverage metadata to distinguish hidden mullions inside merged surfaces from obsolete surface-boundary offsets. However, the all-unmarked legacy path still falls back to the PCEditor parent graph. That fallback can preserve an old offset even when current cladding geometry no longer matches it and no baked surface declares a merge.

The safe rule is stricter: absence of baked merged coverage is absence of permission to preserve a geometry-missing offset.

## Goal

- Make current cladding surface geometry authoritative when the associated surfaces have no `CW_1.03_CLADDING_CELLS` values.
- Clear every stored offset that does not match current surface geometry unless a complete baked coverage partition explicitly proves it is a hidden track inside a merged surface.
- Do not use the PCEditor parent graph as a preservation fallback for unmarked surfaces.
- Keep complete marked partitions, invalid/mixed fail-closed behavior, and canonical coverage writes intact.

## Architecture ownership

- Offset-role policy remains in `PanelCladdingSurfaceSyncPlanningService` under Application.
- Rhino reads, validation, and writes remain in `LivePanelCladdingSurfaceSyncRepository` under Infrastructure.
- Regression artifacts belong in `Project_Test/260825_TEST_pcsync-srf-unmarked-offset-clearing/`.

## Key design

1. Treat `storedSurfaceCoverages == null` as an empty set of protected non-splitting tracks.
2. Preserve a stored track only when a complete valid baked coverage partition maps every logical cell and assigns both sides of the full track to the same physical surface.
3. Continue taking all splitting-boundary coordinates from current surface inference.
4. Continue suppressing stale curve coordinates that align only with an unprotected old boundary.
5. Let the existing sync plan write geometry-derived canonical coverage after a successful unmarked sync, without using that new value retroactively to protect an old mismatch.
6. Keep mixed or malformed marked sets rejected before mutation.

## Files involved

- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSurfaceSyncPlanningService.cs`
- `Project_Test/260825_TEST_pcsync-srf-unmarked-offset-clearing/`
- `Project_Exet/260825_EXET_pcsync-srf-unmarked-offset-clearing.md`
- `Packaging/PanelCladdingEditor/package-manifest.json`

## Usage

1. Edit cladding surfaces so their current boundary offsets differ from the stored H/V values.
2. Run `PCSyncSrf`.
3. If surfaces contain no coverage key, unmatched old values are removed and current surface values are written.
4. If a complete coverage partition explicitly marks a merged region, its proven hidden track may still be preserved.

## Acceptance criteria

- With no baked coverage values, old `21.375` and `107.25` are removed when current surface geometry reports `19.625` and `105.75`.
- An unmarked PCEditor parent relationship cannot preserve a mismatched old value.
- A complete `0A;1A` marked merged surface still preserves its hidden internal track.
- A complete set of single-cell marks clears moved boundaries.
- Mixed, malformed, overlapping, or incomplete marked coverage remains rejected before mutation.
- Existing coverage, offset-refresh, structural-grid, surface-sync, parent-persistence, and spawn regressions pass.
- Debug and Release standalone RHP builds, package identity, installation, registry validation, and packaged/installed hashes pass.

## Risks and rollback

- Legacy merged surfaces without baked coverage will lose hidden offsets not evidenced by current surface boundaries. This is deliberate because the command has no surface-owned proof that those tracks are merges.
- Users who need a hidden mullion preserved must bake or repair complete coverage metadata before syncing.
- Roll back by restoring the former null-coverage owner-graph fallback and reinstalling the prior RHP.

## Future extensions

- Add an explicit repair/preview command that proposes coverage metadata for legacy merged surfaces without allowing normal `PCSyncSrf` to guess.
