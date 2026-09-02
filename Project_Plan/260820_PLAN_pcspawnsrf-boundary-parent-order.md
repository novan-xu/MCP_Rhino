# PCSpawnSrf Boundary Surface And Parent Order PLAN

## Background

`PCSpawnSrf` currently constructs every atomic cladding cell as a trimmed Brep and returns
`Brep.JoinBreps(...)` for a parent-linked region. A condition such as `0A=XX` and `1A=0A` is one
logical cladding region, but the spawned object can retain the former cell divider as an internal
face seam and therefore report as a polysurface. The editor's parent-cell ComboBox also sorts by
row before column, producing `0A, 1A, 0B, 1B, ...`.

This work supersedes the 2026-08-12 region-plan acceptance of internal seams. The region remains
one logical Rhino object, but it must now also be created as one boundary-trimmed face when the
source panel is a single face.

## Goal

- Derive the exterior boundary of each logical cladding region from its configured atomic cells.
- Split the original panel face with only the region's internal boundary segments and select the
  resulting face whose coverage exactly matches the region.
- Return one Brep face for parent-linked regions instead of returning joined atomic-cell faces.
- Preserve fail-closed diagnostics when a region cannot be reconstructed unambiguously.
- Order parent-reference choices by numeric column first and alphabetical row second: `0A` through
  `0D`, then `1A` through `1D`.
- Use each material's configured Material Setup color for the spawned Rhino material layer instead
  of deriving an unrelated color from the spawn planner's fallback palette.

## Architecture Ownership

- `Application/Services/PanelCladding/`: pure atomic-cell boundary planning and cancellation of
  shared region edges.
- `Infrastructure/Rhino/Live/PanelCladding/`: conversion of planned internal boundary segments to
  Rhino curves, source-face splitting, and exact coverage-based face selection.
- `UI/`: parent-reference presentation ordering only.
- `Application/Services/PanelCladding/`: configured material-catalog color parsing and spawn-plan
  color selection remain independent of WPF and RhinoCommon.
- `UI/PanelCladdingSpawnSrfCommand`: read the document-associated Material Setup catalog and pass
  its color map into spawn planning.
- `Project_Test/260820_TEST_pcspawnsrf-boundary-parent-order/`: focused pure boundary/order
  regression plus a Rhino-native single-face probe when a Rhino host is available.

## Key Design

1. Represent each atomic cell by four directed grid edges. Cancel an edge when the adjacent region
   cell contributes the same undirected edge; the remaining edges are the configured region
   boundary.
2. Mark a remaining edge as panel perimeter when no panel cell exists on its opposite side. The
   source face already owns those outer trims, so only internal boundary segments are supplied to
   `BrepFace.Split`.
3. Resolve each internal segment from the corresponding already-trimmed atomic cell edge. This
   retains the exact source-panel curvature instead of manufacturing a planar or patched surface.
4. Split the original single face once with the complete internal boundary network. Duplicate only
   a split face whose geometric coverage labels exactly equal the planned region cells.
5. If a region covers the complete grid, duplicate the original single-face Brep directly. If the
   source is not a single face or face selection is ambiguous, fail rather than silently return a
   seam-bearing joined polysurface.
6. Keep atomic cell Breps for coverage sampling and extrusion-curve construction; stop using their
   joined Brep as the spawned region result.
7. Isolate parent-choice ordering in a deterministic UI method and sort by `Column`, then `Row`.
8. Resolve `#RRGGBB` values from the workbook material catalog into application-level RGB values.
   An exact material-code match overrides the legacy family palette. A material absent from the
   catalog retains the deterministic fallback color for backward compatibility.
9. If a document has an associated workbook but its material catalog cannot be read or validated,
   stop `PCSpawnSrf` before geometry mutation rather than silently use colors that disagree with
   Material Setup. Documents without an associated workbook retain legacy fallback behavior.

## Files Involved

- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingRegionBoundaryService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingMaterialColorService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSpawnPlanningService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingGeometryPartitionService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSpawnService.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingSpawnSrfCommand.cs`
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj` (exact standalone-test exclusion only)
- `Project_Test/260812_TEST_panel-cladding-regions/Program.cs` (updated native seam assertion)
- `Project_Test/260820_TEST_pcspawnsrf-boundary-parent-order/`

## Usage

1. Configure a material owner and one or more parent references, for example `0A=XX` and `1A=0A`.
2. Run `PCSpawnSrf` on the panel.
3. Inspect the result: the region is one Brep object with one face and no internal cell-divider seam.
4. In `PCEditor`, select cells and open the parent-reference dropdown. Choices are grouped by
   numeric column, then listed bottom-to-top by row.
5. Colors saved in Material Setup are applied to the matching spawned material layers. Existing
   layers with the same material path are updated to the configured color during spawn.

## Acceptance Criteria

- Pure boundary planning cancels the shared edge between `0A` and `1A`.
- Boundary planning distinguishes panel-perimeter edges from the internal curves needed to isolate
  a partial region.
- A complete parent-linked region needs no split curves and duplicates the original single face.
- A partial parent-linked region is reconstructed from the source face and the returned Brep has
  exactly one face when executed in a Rhino native host.
- Exact coverage selection still resolves every configured region cell and no other cell.
- The parent list sequence is `0A, 0B, 0C, 0D, 1A, 1B, 1C, 1D` for a 2-by-4 grid.
- A catalog value such as `XX=#123456` produces `PanelColorRgb(18, 52, 86)` in the spawn region and
  therefore on the `XX` Rhino material layer.
- A configured color overrides the deterministic fallback; a material absent from the catalog
  still receives its existing fallback color.
- Focused Debug and Release smokes pass; relevant panel-cladding regressions pass; the standalone
  PanelCladdingEditor RHP builds in Debug and Release with zero warnings and errors.

## Risks And Rollback

- Rhino face splitting can fail on invalid, multi-face, or tolerance-damaged panel geometry. The
  new path fails closed with a region-specific diagnostic and creates no objects because geometry
  preparation still precedes the Undo-wrapped mutation.
- Coincident or incomplete atomic boundary curves may make face selection ambiguous. Exact coverage
  comparison prevents choosing the wrong fragment.
- A stale document workbook association now prevents spawning because the requested Material Setup
  colors cannot be trusted. Clearing/fixing the association or restoring the workbook resolves the
  diagnostic without any Rhino document mutation.
- Rollback restores the prior `JoinRegion` path and the row-first ComboBox sort. Any successfully
  spawned objects remain recoverable through the command's single Rhino Undo record.

## Future Extensions

- Support intentional source polysurfaces by reconstructing one face per source-face patch only
  when the product contract explicitly allows seam-bearing output.
- Add Rhino-hosted curved-panel fixtures for L-shaped and holed parent regions.
- Reuse the pure region-boundary plan for editor boundary highlighting and diagnostics.

## Revision Record (2026-08-20)

During execution the user added the explicit requirement that spawned layer colors match Material
Setup. The goal, architecture ownership, design, involved files, usage, acceptance criteria, and
risk sections were revised before implementing that added scope.
