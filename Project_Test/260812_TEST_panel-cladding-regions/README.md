# Panel Cladding Regions Test

## Automated smoke

Run from the repository root:

```powershell
dotnet run --project .\Project_Test\260812_TEST_panel-cladding-regions\PanelCladdingRegionsSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260812_TEST_panel-cladding-regions\PanelCladdingRegionsSmoke.csproj -c Release
```

The smoke verifies owner/reference parsing, direct-reference normalization, canonical owner choice,
cycle/unknown/blank/disconnected failures, separate equal-material regions, two-region spawn
planning for `GLS-001 / 0A / GLS-002`, v3 signature topology/offset identity, and geometry-coverage
surface-sync planning for joined, separated, missing-CID, overlapping, disconnected, and incomplete
surface mappings under the `04_STEP Surfaces` root. It also attempts to construct a planar Rhino Brep, partition it into atomic cells,
join a two-cell region, recover its footprint, and reject a partial-cell surface. The geometry probe
reports `[SKIP]` under the CLI because Rhino's native geometry runtime initializes only inside Rhino;
the live fixture below is therefore required for final geometry verification.

## Live Rhino fixture

1. Open the saved BKT wireframe document and select one checked panel with V0/V1 boundaries that
   terminate in different horizontal bands.
2. Set its cell attributes so at least one adjacent cell references an owner label, for example
   `0A=GLS-001`, `1A=0A`, `2A=GLS-002`.
3. Run `_PCSpawn`. Confirm two objects are created, the first object's CID ends in `-0A`,
   and its Brep contains both 0A and 1A footprints.
4. Undo once and confirm both spawned objects are removed in one Undo operation.
5. Spawn again, manually join two adjacent cladding objects, and retain the joined object on one
   valid material leaf below `04_STEP Surfaces`.
6. Run `_PCSync`. Confirm the panel owner cell stores the layer material, all
   other covered cells store the canonical owner label, and the joined surface CID is normalized to
   that owner.
7. Separate two same-material footprints into two Rhino objects and sync again. Confirm each cell
   stores the material code rather than a reference.
8. Create a deliberately partial or overlapping surface and sync. Confirm the panel is reported and
   selected as skipped, with no partial panel/workbook/surface metadata writes.
9. Run Rhino Undo after a successful sync and confirm panel and surface metadata return together.
