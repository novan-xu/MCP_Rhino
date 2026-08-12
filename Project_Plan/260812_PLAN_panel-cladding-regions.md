# Panel Cladding Regions

## Background

Panel cladding offsets currently define a complete rectangular grid and the plug-in creates one
surface for every populated cell. BayHealth Kent Tower contains panels where a vertical or
horizontal offset is only a candidate boundary: adjacent atomic cells may belong to one manually
joined cladding object. The panel user text must remain a complete standalone description of this
condition.

## Goal

Support owner/reference cell values such as `0A=GLS-001`, `1A=0A`, `2A=GLS-002`. A material value
creates an owning region and a cell-label value joins that cell to the referenced region. Spawn must
create one Rhino Brep object per region, and Sync From Surfaces must reconstruct normalized
owner/reference values from the geometry of each cladding object.

## Architecture Ownership

- `Domain/`: region and region-cell models plus sync snapshot fields.
- `Application/Services/PanelCladding/`: reference parsing, validation, normalization, spawn plans,
  sync plans, and type identity.
- `Infrastructure/Rhino/Live/PanelCladding/`: shared atomic-cell geometry partitioning, region Brep
  joining, surface-footprint classification, and transactional attribute commits.
- `Infrastructure/File/`: workbook output continues to serialize the normalized panel values.
- `Project_Test/260812_TEST_panel-cladding-regions/`: focused regression smoke and live verification
  instructions.

## Key Design

1. Reserve cell-label-shaped values (`<column><row>`, for example `1A`) as references. A reference
   to an unknown cell is an error rather than a material code.
2. Resolve reference chains with cycle detection, require every resulting region to be edge
   connected, and normalize each region to the lowest row then lowest column. The canonical owner
   stores the material; every follower stores the canonical owner label directly.
3. Preserve blank-cell spawn behavior, but reject references to blank cells. Type save continues to
   require every atomic cell to be assigned.
4. Change spawn planning from one plan per atomic cell to one plan per normalized region. Build the
   proven trimmed atomic Breps first, then join all cells in one connected region into one Brep
   object whose CID suffix is the canonical owner label.
5. During surface sync, classify each material-surface Brep against the selected panel's atomic
   cell geometry. Validate sampled geometric coverage and area, reject overlaps, partial cells,
   gaps, and disconnected footprints, then derive one material owner plus cell references per
   surface object.
6. Normalize a surface's `CW_1.02_CID` to its derived owner CID while refreshing `Cladding` from the
   material layer. Keep the existing all-or-nothing workbook/Rhino attribute transaction and Rhino
   Undo behavior.
7. Advance type signatures to schema `v3`; include panel dimensions, H/V offset values, normalized
   materials, and references in the canonical payload so equal materials with different region
   boundaries cannot reuse one type accidentally.
8. Keep the panel attributes authoritative. Surface coverage metadata is derived from geometry and
   is not required to reconstruct the layout.

## Files Involved

- `src/PanelCladdingEditor/Domain/PanelCladdingModels.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingRegionService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladdingKeyService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladdingTypeSignatureService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSpawnPlanningService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSurfaceSyncPlanningService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSurfaceSyncService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingGeometryPartitionService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSpawnService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSurfaceSyncRepository.cs`
- relevant existing PanelCladdingEditor smoke assertions affected by the v3 schema and region plan
- `Project_Test/260812_TEST_panel-cladding-regions/`

## Usage

Set each panel cladding key to either a material code or another cell's short label. For example:

```text
CW_4.00_CLADDING_0A=GLS-001
CW_4.01_CLADDING_1A=0A
CW_4.02_CLADDING_2A=GLS-002
```

Run `_PanelCladdingSpawn` to create two cladding objects. After manually joining or separating
cladding objects, select the panel and run `_PanelCladdingSyncFromSurfaces` to rewrite the panel keys
to the normalized geometry-derived representation.

## Acceptance Criteria

- The example `GLS-001 / 0A / GLS-002` produces two spawn regions and two CIDs.
- Adjacent equal material values without references remain separate surfaces.
- Chains normalize to direct references; cycles, unknown targets, and disconnected groups fail.
- Spawn creates one connected Brep object per region, including stepped multi-row regions.
- Surface sync maps one joined object to one owner plus followers and maps separate same-material
  objects to separate material owners.
- Surface sync isolates invalid panels and rejects overlapping, incomplete, partial, or disconnected
  cell coverage without partial writes.
- Surface CID and `Cladding` metadata normalize transactionally with panel attributes and workbook
  output.
- v3 identities differ when region topology or offset geometry differs.
- Focused Debug and Release smokes pass; the standalone plug-in builds in Debug and Release; the
  compiled RHP assembly GUID is non-empty and matches the manifest and plug-in class.

## Risks And Rollback

- Geometric footprint inference may encounter modeling tolerances or manually edited boundaries that
  do not align with the atomic grid. It fails closed and leaves that panel unchanged.
- Joined curved Breps may retain internal seams; this is acceptable because one Rhino object, not one
  Brep face, defines a cladding region.
- The v3 signature intentionally creates new type identities for previously under-specified layouts.
- Rollback is a revert of this capability's source and test changes. Runtime mutations remain covered
  by one Rhino Undo record; failed workbook finalization restores prepared Rhino attributes.

## Future Extensions

- Add explicit merge/unmerge controls and suppressed-boundary rendering in the editor preview.
- Add optional region visualization or rectangular Excel merges while retaining owner/reference
  values as the machine-readable contract.
- Persist diagnostic coverage samples when field geometry requires tolerance tuning.
