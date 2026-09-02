# Panel Cladding Split Spawn and Sync Commands Plan

## Background

Version 1.0.41 introduced combined `PCSpawn` and `PCSync` workflows that create/read both cladding
surfaces and extrusion curves. The required Rhino workflow needs those object families to remain
independently callable so a user can generate or reconcile only cladding geometry or only extrusion
geometry.

## Goals

- Replace `PCSpawn` with `PCSpawnSrf` and `PCSpawnCrv`.
- Replace `PCSync` with `PCSyncSrf` and `PCSyncCrv`.
- Ensure surface commands neither require nor create/repair curve objects.
- Ensure curve commands neither require nor create/repair cladding surface objects.
- Preserve the shared panel-layout invariants, transactional Rhino Undo behavior, underscore curve
  naming, surface/curve layer contracts, metadata repair, and five-decimal unit dimensions.
- Remove the two combined commands from Rhino's visible command table.

## Architecture Ownership

- Domain models identify the requested surface/curve operation scope.
- Application services own scope-specific planning and prevent cross-family writes.
- Rhino infrastructure owns scope-specific geometry discovery, association, creation, and commits.
- Four thin UI command adapters own selection and command-line reporting.

## Key Design

1. Add an explicit surface/curve scope to spawn and sync application contracts instead of inferring
   behavior from which document objects happen to exist.
2. `PCSpawnSrf` resolves material regions and creates only Breps under `04_STEP Surfaces`.
3. `PCSpawnCrv` expands offsets/masks through the shared extrusion planner and creates only curves
   under `02_CW Extrusions::Curves-PNL::Main Frame`; it does not require populated cladding cells.
4. `PCSyncSrf` enumerates only supported material-surface layers, associates by geometry, infers the
   surface grid, reconstructs material-owner/parent-cell values, repairs surface metadata, and does
   not inspect or write curve objects. Existing topology is preserved when compatible; if surface
   edits change the row/column count, the panel receives a complete/unmerged topology for the new
   grid because no curve geometry was authorized as truth.
5. `PCSyncCrv` enumerates only the extrusion layer, associates by geometry, infers offsets plus
   segment/merge masks, repairs curve metadata, and does not inspect or write surface objects. Cell
   assignments whose canonical labels still exist are retained; newly introduced cells remain
   unassigned until `PCSyncSrf` is run.
6. Both sync modes recalculate panel identity and unit dimensions from their resulting layout and
   use one Undo-wrapped commit. Result reporting distinguishes the affected family.

## Files Involved

- `src/PanelCladdingEditor/Domain/PanelCladdingModels.cs`
- `src/PanelCladdingEditor/Application/Interfaces/IPanelCladdingServices.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSurfaceSyncPlanningService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSurfaceSyncService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSpawnService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSurfaceSyncRepository.cs`
- new `PCSpawnSrf`, `PCSpawnCrv`, `PCSyncSrf`, and `PCSyncCrv` UI command files
- command/spawn/sync regressions, package documentation/manifest, and matching TEST/EXET artifacts

## Usage

- Run `_PCSpawnSrf` to create only cladding surfaces for selected panels.
- Run `_PCSpawnCrv` to create only extrusion curves for selected panels.
- Run `_PCSyncSrf` after manually editing cladding surfaces/layers.
- Run `_PCSyncCrv` after manually editing extrusion curves.
- When both object families changed, run the curve sync first to establish the extrusion grid, then
  run the surface sync to assign the final cladding regions.

## Acceptance Criteria

- Rhino registers exactly eight production commands: the four existing editor/create/match/clear
  commands plus the four new scoped spawn/sync commands.
- `PCSpawn`, `PCSync`, `PCSpawnFrom*`, and `PCSyncFrom*` aliases are absent.
- Surface spawn succeeds without curve creation and curve spawn succeeds with blank cladding cells.
- Surface sync succeeds with no curves in the document and emits no curve writes.
- Curve sync succeeds with no cladding surfaces in the document and emits no surface writes.
- Each command retains correct layers, PID/CID/CRV/Cladding metadata, topology behavior, unit keys,
  one-Undo transaction, and rollback protection.
- Debug/Release focused regressions, solution builds, package, hashes, and RHP identity pass.

## Risks and Rollback

- A surface-only grid-count change has no curve geometry from which to infer deleted/merged spans;
  complete/unmerged topology is the deterministic fallback and is documented in command output.
- A curve-only grid-count change cannot geometrically infer cladding materials; matching existing
  cell labels are retained and new cells remain unassigned.
- Running only one sync after editing both families can temporarily leave the other family stale;
  command output will identify which family was reconciled.
- Rollback consists of restoring the combined commands/services. Each scoped live mutation remains
  recoverable through Rhino Undo.

## Future Extensions

- Add an optional `PCSyncAll` orchestration command only if a later workflow explicitly requests it.
- Add pre-commit visual diagnostics for surface-only topology fallback and newly unassigned cells.
