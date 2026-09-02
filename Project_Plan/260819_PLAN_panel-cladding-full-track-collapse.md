# Panel cladding full-track collapse plan

## Background

PCEditor currently stores every H/V offset even when every atomic extrusion segment on that track has been deleted. The segment mask can hide the curve, but the retained offset leaves a phantom grid point and dimension, prevents downstream cell renumbering, and causes PCSpawnSrf to partition physical cells that no longer have persisted assignments.

The reported `N1_PF_07` example deletes the original H0 across both columns. The editor still displays four physical rows (`A-D`) with a merged `A/B` region, while the expected stored layout has three rows (`A-C`): old C becomes B and old D becomes C.

## Goal

Make a completely deleted extrusion track collapse out of the editable panel lattice and ensure every saved panel attribute and spawned cladding surface reflects the resulting normalized layout.

## Architecture ownership

- Application services own topology normalization and logical-to-physical assignment expansion.
- PCEditor invokes the same normalization immediately after extrusion deletion so offsets, labels, dimensions, and topology redraw together.
- Save normalizes again as a fail-safe before generating the write/delete transaction.
- PCSpawnSrf expands logical representative assignments over their physical topology members before region planning.

## Key design

1. Detect an H track as complete when every column bay is missing; detect a V track as complete when every row bay is missing.
2. Remove complete tracks iteratively, because one collapse changes opposite-axis bay numbering.
3. Retain the lower/left cell as the merged representative, discard the removed upper/right physical label, and shift all subsequent row/column labels down.
4. Remap parent-cell tokens through the same coordinate map.
5. Remap missing-segment and merge-run coordinates, removing the collapsed track and splitting invalid merge runs around missing segments.
6. Merge the corresponding dimension-lock entries and redraw from the reduced offset lists immediately.
7. Save normalized offsets, cells, masks, type/signature, and unit dimension attributes; delete every obsolete cell/offset key.
8. Expand partial logical groups for signature and surface-region planning so spawned geometry covers every physical member without holes or overlaps.

## Involved files

- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingTopologyNormalizationService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingLogicalCellService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSpawnPlanningService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladdingSaveService.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml.cs`
- `Project_Test/260819_TEST_panel-cladding-full-track-collapse/`
- `Project_Exet/260819_EXET_panel-cladding-full-track-collapse.md`

## Usage

Select every segment on one intermediate H or V extrusion in PCEditor and delete it. The editor immediately removes that offset/dimension, renumbers later cells, and Save writes the reduced layout. PCSpawnSrf then creates complete surfaces from the saved logical assignments.

## Acceptance criteria

- Deleting both H0 segments from a 2x4 grid produces a 2x3 grid and removes H0 from the stored offset sequence.
- Old C/D assignments become new B/C assignments; old B disappears into A.
- Opposite-axis topology and parent references are remapped deterministically.
- Dimension badges and locks contain one fewer row/column immediately after deletion.
- Save writes current unit width, height, and dimension values and deletes obsolete cell/offset keys.
- Partial segment deletion still retains its offset and uses the segment mask.
- PCSpawnSrf region plans cover hidden physical members of partial logical cells.
- Existing topology, spawn, PCMatch, Debug, Release, and plug-in identity validation pass.

## Risks and rollback

- Risk: simultaneous H/V collapses can shift topology coordinates twice. Mitigation: normalize one deterministic track at a time and retain an ordered collapse event list.
- Risk: merge runs can cross newly missing coordinates. Mitigation: rebuild each run from remapped atomic bays and split around missing segments.
- Risk: conflicting assignments on cells being collapsed. Existing editor behavior clears the affected merged group, while unaffected later cells are preserved and renumbered.
- Rollback: remove the normalization call sites and helper; prior segment-mask behavior remains isolated.

## Future extensions

- Use the same topology normalizer in PCSyncCrv when geometry inference finds a fully absent extrusion track.
- Add an editor option to choose assignment conflict policy when collapsing differently assigned cells.
