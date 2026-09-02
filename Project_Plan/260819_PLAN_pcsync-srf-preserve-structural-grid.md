# PCSyncSrf Preserve Structural Grid PLAN

## Background

The saved BKT panel `PID_BKT_N1_01_11` proves the remaining failure. The panel stores `V0=22.5` and parent references including `1A=0A`, but it has no associated extrusion-curve objects. Its cladding surface spans both columns. `PCSyncSrf` currently replaces the panel grid with offsets inferred from surfaces and any discovered curves. With no curve at `V0` and no cladding boundary there, inference returns one column; the reduced grid then has no `1A` cell to persist.

Surface geometry cannot distinguish an obsolete divider from a retained structural mullion that intentionally does not split cladding. Since surface and curve synchronization are separate commands, `PCSyncSrf` must not remove an existing structural H/V track. `PCSyncCrv` remains responsible for authoritative extrusion-grid removal and replacement.

## Goal

- Preserve every existing panel H/V offset during `PCSyncSrf`, even when no associated curve or cladding boundary represents it.
- Continue adding genuinely new boundaries inferred from edited cladding surfaces or associated curves.
- Rebuild surface coverage on the combined grid so spanning claddings recreate parent values such as `1A=0A`.
- Leave `PCSyncCrv` offset-removal behavior unchanged.

## Architecture ownership

- Scope-specific grid-source composition remains in `LivePanelCladdingSurfaceSyncRepository` in Infrastructure.
- Deterministic offset union/normalization belongs to `PanelCladdingSurfaceSyncPlanningService` in Application so it is Rhino-independent and directly testable.
- Regression coverage belongs in `Project_Test/260819_TEST_pcsync-srf-preserve-structural-grid/`.

## Key design

1. Add a pure helper that unions existing and inferred offsets, clusters values within tolerance, and validates the result against the panel extent.
2. Apply this union only when sync scope is `Surfaces`.
3. Use the combined offsets for key-set construction, geometry grid creation, coverage resolution, parent reconstruction, and persisted panel writes.
4. Continue using raw geometry-inferred offsets for `Curves` scope so `PCSyncCrv` can remove tracks.
5. Cover the exact no-curve condition: stored `V0`, zero inferred vertical offsets, and one surface spanning `0A` plus `1A`.

## Files involved

- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSurfaceSyncPlanningService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSurfaceSyncRepository.cs`
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`
- `Project_Test/260819_TEST_pcsync-srf-preserve-structural-grid/`
- `Packaging/PanelCladdingEditor/package-manifest.json`

## Acceptance criteria

- Surface scope preserves stored `V0=22.5` when inferred vertical offsets are empty.
- Surface scope unions new surface-derived offsets with existing structural offsets without duplicates.
- A spanning surface on that combined two-column grid reconstructs `0A=<material>`, `1A=0A`.
- Curve scope still uses inferred offsets directly and may remove an existing track.
- Existing surface-sync structural-grid, parent-persistence, and base surface-sync suites pass.
- Debug/Release builds, package creation, identity validation, and current-user installation pass.

## Risks and rollback

- `PCSyncSrf` will no longer delete stale panel offsets. This is intentional because surface geometry alone cannot determine whether a non-splitting track is structural. Use `PCSyncCrv` to synchronize/removal extrusion tracks.
- Roll back by restoring direct use of `InferOffsets` output for surface scope and reinstalling the previous package.
