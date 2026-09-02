# Cladding Surface Coverage Metadata PLAN

## Background

The panel stores a material-independent owner graph, so PCEditor can display `1A=0A` when one cladding surface spans both cells. The baked Rhino Brep itself currently stores PID, CID, and material only. It does not persist that its owner geometry represents both `0A` and `1A`.

Without surface-owned coverage metadata, `PCSyncSrf` cannot reliably distinguish two geometrically similar cases:

- a legitimate hidden mullion inside one merged cladding region, which must preserve its grid offset; and
- an obsolete boundary/curve left behind after separate cladding surfaces were resized, which must not be preserved.

The panel graph alone is insufficient after a bad sync has expanded the grid because both cases can appear as a parent-cell span.

## Goal

- Persist the logical cell coverage on every baked cladding Brep under canonical user text `CW_1.03_CLADDING_CELLS`.
- Encode a single-cell surface as `0A` and the merged example as `0A;1A`, with owner first and deterministic remaining-cell order.
- Make `PCSyncSrf` use a complete valid set of baked coverage metadata as the authority for which fully spanned tracks are hidden structural mullions.
- Continue supporting all-legacy surface sets for one migration sync through the existing panel owner graph.
- Fail before mutation on mixed, malformed, duplicate, unknown, overlapping, or incomplete baked coverage metadata rather than guessing.
- Refresh the baked coverage attribute from current geometry during `PCSyncSrf` so edits migrate to the canonical state.

## Architecture ownership

- Coverage encoding, decoding, canonicalization, and partition validation belong to a focused Application service under `Application/Services/PanelCladding/`.
- Spawn/sync contracts remain in `Domain/PanelCladdingModels.cs`.
- Rhino attribute reads and writes remain in `LivePanelCladdingSpawnService` and `LivePanelCladdingSurfaceSyncRepository` under Infrastructure.
- Offset role selection remains in `PanelCladdingSurfaceSyncPlanningService` and consumes validated surface coverage without RhinoCommon dependencies.
- Regression artifacts belong in `Project_Test/260825_TEST_cladding-surface-coverage-metadata/`.

## Key design

1. Define canonical key `CW_1.03_CLADDING_CELLS` and a semicolon-delimited value containing the complete region coverage, including the owner.
2. `PCSpawnSrf` writes the key on every created surface. Region cells already provide the authoritative labels; no new geometry inference is needed at bake time.
3. `PCSyncSrf` reads the raw key from every geometrically associated material surface.
4. Coverage authority modes are explicit:
   - all surfaces have valid metadata: use the baked partition;
   - no surfaces have metadata: use the saved panel owner graph and schedule canonical metadata writes (legacy migration);
   - only some surfaces have metadata: reject as incomplete before mutation.
5. A metadata partition is valid only when every existing logical panel cell occurs exactly once across the associated surfaces and every label is known.
6. A stored H/V track is protected only when every adjacent cell pair across the full track belongs to the same persisted baked surface. Splitting boundaries remain surface-geometry authoritative.
7. Current geometry-derived coverage determines the desired attribute after reconciliation. The sync plan deletes any casing variant of the old key and writes the canonical value.
8. Coverage-only drift counts as surface metadata change, even when PID, CID, material, and panel type remain unchanged.

## Files involved

- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSurfaceCoverageService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSpawnPlanningService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSurfaceSyncPlanningService.cs`
- `src/PanelCladdingEditor/Domain/PanelCladdingModels.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSurfaceSyncRepository.cs`
- `Project_Test/260825_TEST_cladding-surface-coverage-metadata/`
- `Project_Exet/260825_EXET_cladding-surface-coverage-metadata.md`

## Usage

1. Configure parent cells in PCEditor, for example set `1A` to parent `0A`.
2. Run `PCSpawnSrf`; the resulting owner Brep carries `CW_1.03_CLADDING_CELLS=0A;1A`.
3. Edit cladding geometry as needed and run `PCSyncSrf`.
4. The command uses baked coverage to preserve only hidden mullion tracks and refreshes the coverage attribute from current geometry.

## Acceptance criteria

- Spawn planning writes `CW_1.03_CLADDING_CELLS=0A;1A` for a merged 0A/1A region and `=0A` for a single-cell region.
- Coverage encoding is deterministic, owner-first, duplicate-free, and case-normalized.
- A complete baked coverage partition preserves the offset between merged `0A` and `1A` even when the cladding Brep has no boundary there.
- Separate baked coverage values `0A` and `1A` do not protect the boundary; edited surface offsets replace old values and stale curves cannot re-add them.
- The previously reported `21.375/107.25` to `19.625/105.75` refresh remains duplicate-free.
- All-legacy surfaces can migrate and receive the new key during sync.
- Mixed or invalid metadata fails before any commit request is produced.
- `PCSyncSrf` rewrites stale coverage metadata to the geometry-derived canonical value.
- Existing spawn, surface-sync, structural-grid, parent-persistence, logic-persistence, and offset-refresh regressions pass.
- Debug and Release standalone RHP builds, package identity, install, and registry/hash validation pass before handoff.

## Risks and rollback

- Existing baked surfaces do not have the new key. All-legacy batches deliberately use the panel graph for one migration sync; partial/mixed migrations fail closed.
- Manually copied surfaces with duplicated coverage labels will be rejected rather than silently reassigned.
- A fully spanned track is protected only when the complete partition proves the same baked surface owns both sides across every bay.
- Roll back by removing the coverage codec, spawn write, sync read/write, and metadata-aware offset overload, then reinstalling the prior RHP. Rhino mutations remain one Undo record.

## Future extensions

- Surface inspection UI can display owner and covered-cell badges directly from `CW_1.03_CLADDING_CELLS`.
- A future repair command can migrate legacy or previously corrupted models with an explicit preview of proposed surface coverage and protected tracks.
