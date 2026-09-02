# Cladding Merge Mark Key PLAN

## Background

Baked cladding coverage is currently stored under `CW_1.03_CLADDING_CELLS`. The user requires a purpose-specific, non-`CW_1.03` name: `Merge_Mark`. The value contract remains complete surface coverage, such as `0A` for a single-cell surface and `0A;1A` for a merged surface.

The open live document already contains the former key, so the repository and live data must migrate without losing coverage or creating conflicting marks.

## Goal

- Make `Merge_Mark` the only canonical baked cladding coverage key.
- Make `PCSpawnSrf` write `Merge_Mark` and never create `CW_1.03_CLADDING_CELLS`.
- Make `PCSyncSrf` read, validate, refresh, and persist `Merge_Mark`.
- Allow the old key only as a legacy migration input; successful sync removes it and writes `Merge_Mark`.
- Fail before mutation if canonical and legacy values coexist with different non-empty values.
- Rename the existing marks in the current open Rhino document, verifying values before deleting the old keys.

## Architecture ownership

- Canonical and legacy key names belong to `PanelCladdingSurfaceCoverageService` under Application.
- Spawn writes remain in `PanelCladdingSpawnPlanningService`.
- Sync read/conflict detection and attribute cleanup remain in `LivePanelCladdingSurfaceSyncRepository` under Infrastructure.
- Snapshot/plan migration state remains in `Domain/PanelCladdingModels.cs`.
- Regression artifacts belong in `Project_Test/260825_TEST_cladding-merge-mark-key/`.

## Key design

1. Set `PanelCladdingSurfaceCoverageService.UserTextKey` to `Merge_Mark`.
2. Retain `CW_1.03_CLADDING_CELLS` only as `LegacyUserTextKey` for migration reads and deletion.
3. `PCSpawnSrf` uses the canonical constant, so all new surfaces receive only `Merge_Mark`.
4. Surface sync reads canonical and legacy values independently:
   - canonical only: use canonical;
   - legacy only: use legacy and schedule a metadata rewrite;
   - both equal: use canonical and schedule legacy deletion;
   - both different: reject the panel before offset planning or mutation.
5. Sync commit deletes all casing variants of both keys, then writes only `Merge_Mark`.
6. Live-document migration first previews/writes `Merge_Mark` from the old value, verifies equality, then previews/deletes the legacy key. Conflicts are not overwritten.

## Files involved

- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSurfaceCoverageService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSpawnPlanningService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSurfaceSyncPlanningService.cs`
- `src/PanelCladdingEditor/Domain/PanelCladdingModels.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSurfaceSyncRepository.cs`
- `Packaging/PanelCladdingEditor/README.md`
- `Packaging/PanelCladdingEditor/package-manifest.json`
- `Project_Test/260825_TEST_cladding-merge-mark-key/`
- `Project_Exet/260825_EXET_cladding-merge-mark-key.md`

## Usage

1. `PCSpawnSrf` creates baked cladding surfaces with `Merge_Mark` values.
2. `PCSyncSrf` treats complete `Merge_Mark` coverage as the hidden-track authority.
3. A legacy-only surface migrates to `Merge_Mark` when synced.
4. A conflicting canonical/legacy pair must be corrected explicitly before sync.

## Acceptance criteria

- Canonical constant equals exactly `Merge_Mark`.
- Spawn plans contain `Merge_Mark` and do not contain `CW_1.03_CLADDING_CELLS`.
- Sync uses canonical-only and legacy-only values correctly.
- Legacy-only and equal dual-key snapshots schedule cleanup; canonical-only values do not.
- Conflicting dual-key values fail before mutation.
- Commit removes both key names and writes only `Merge_Mark`.
- Existing coverage, unmarked-clearing, offset-refresh, spawn, and surface-sync regressions pass.
- Current open document contains the same coverage values under `Merge_Mark` and no former key after migration.
- Debug and Release standalone RHP builds and package identity validation pass.

## Risks and rollback

- Deleting the old key before verifying the new value could lose coverage. Live migration therefore writes and verifies first, then deletes.
- Dual-key conflicts are rejected rather than guessed.
- Rhino is currently open, so a new RHP cannot be activated until Rhino closes. Package construction may complete while installation remains pending.
- Roll back by restoring the former canonical constant and reinstalling the prior RHP; live document changes remain recoverable through Rhino Undo until saved.

## Future extensions

- Add a dedicated PanelCladdingEditor migration command if future schema renames require one-click in-product cleanup across multiple documents.
