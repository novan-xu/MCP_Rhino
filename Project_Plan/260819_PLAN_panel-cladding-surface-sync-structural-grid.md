# Panel Cladding Surface Sync Structural Grid PLAN

## Background

`PCSyncSrf` currently reconstructs panel H/V offsets from cladding-surface boundaries only. A structural mullion can remain in the panel while one cladding surface spans across it. In that condition the surface geometry contains no split at the mullion, so surface sync incorrectly removes the corresponding offset (for example `V0`) and collapses the logical grid. Once the grid is collapsed, the sync planner can no longer restore parent-cell values such as `1A=0A`.

## Goal

- Preserve structural H/V tracks during `PCSyncSrf` when associated extrusion curves still define those tracks.
- Continue using cladding-surface coverage to decide whether adjacent logical cells are separate materials or parent-cell references.
- Reconstruct a spanning cladding as one region, with the first logical cell as owner and covered cells such as `1A` pointing to `0A`.
- Keep `PCSyncCrv` behavior unchanged.

## Architecture ownership

- Geometry-source composition belongs to `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSurfaceSyncRepository.cs`.
- Offset inference remains owned by `LivePanelCladdingGeometryPartitionService`; it already supports a union of cladding surfaces and extrusion curves.
- Logical owner/parent reconstruction remains owned by `PanelCladdingSurfaceSyncPlanningService`.
- Regression coverage belongs in `Project_Test/260819_TEST_panel-cladding-surface-sync-structural-grid/`.

## Key design

1. In surface scope, pass both associated cladding Breps and associated extrusion curves to offset inference.
2. In curve scope, continue passing extrusion curves without cladding Breps.
3. Treat the inferred structural grid and the inferred cladding coverage as separate concerns:
   - curves preserve structural H/V tracks;
   - surfaces claim one or more logical cells;
   - a surface spanning multiple cells writes its material on the first row/column owner and parent references on every other covered cell.
4. Preserve existing topology masks when the inferred structural grid dimensions still match the stored layout.
5. Do not make a cladding boundary invent a requirement for separate material values when one Brep spans the structural divider.

## Files involved

- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSurfaceSyncRepository.cs`
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`
- `Project_Test/260819_TEST_panel-cladding-surface-sync-structural-grid/`
- `Packaging/PanelCladdingEditor/package-manifest.json`

## Usage

1. Keep or manually edit the structural mullion curve associated with the panel.
2. Merge the cladding geometry across that mullion so one cladding Brep covers both logical sides.
3. Run `PCSyncSrf` on the panel.
4. The mullion offset remains, while the spanning surface reconstructs the non-owner logical cells as parent references such as `1A=0A`.

## Acceptance criteria

- A full-panel cladding Brep plus an associated center vertical mullion infers `V0` at the mullion location.
- A surface spanning logical cells `0A` and `1A` produces the material on `0A` and `1A=0A`.
- The surface remains one planned/spawned cladding region whose CID uses owner cell `0A`.
- Existing surface-sync, offset-sync, split/spawn/sync, and full-track-collapse regressions pass.
- Debug and Release solution builds pass.
- Package version, RHP assembly identity, and staged/install validation pass.

## Risks and rollback

- An unrelated curve incorrectly associated with a panel could preserve an unwanted track. Existing curve-to-panel association and interior-divider filtering remain the guardrails.
- Surface sync now depends on associated structural curves for this condition; if the curve is deleted too, the offset is expected to disappear.
- Roll back by restoring surface-scope offset inference to cladding Breps only and reinstalling the previous package.

## Future extensions

- Add an editor diagnostic distinguishing structural tracks from cladding-splitting boundaries.
- Add an optional sync report listing which offsets came from curves, surfaces, or both.
