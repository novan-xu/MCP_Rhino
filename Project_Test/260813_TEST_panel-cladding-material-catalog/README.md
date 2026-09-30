# Panel Cladding Material Catalog Smoke

Run:

```powershell
dotnet run --project Project_Test/260813_TEST_panel-cladding-material-catalog/PanelCladdingMaterialCatalogSmoke.csproj -c Release
```

The smoke verifies:

- creation and round-trip reading of a seven-entry `Materials` worksheet;
- table filtering, frozen header, exact BKT colors, and absence of generated type sheets;
- panel save without a workbook, with no cladding type-code write and legacy signature cleanup;
- sidebar ordering, `CLADDING TYPE` naming, bright-red selection overwrite, and centered dash source contract.

`extract_bkt_materials.py` is a read-only migration helper. It reads the BKT `.3dm` with `rhino3dm`,
collects material codes used by panel metadata, matches them to material-layer colors, and emits
`bkt-materials.json`. It does not launch or modify Rhino.

The BKT workbook was separately rendered, migrated, rendered again, re-imported, value-inspected,
and scanned for formula errors through the spreadsheet artifact runtime. A timestamped sibling backup
was created before replacement.
