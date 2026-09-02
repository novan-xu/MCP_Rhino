# Panel Cladding Extrusion Spawn and Geometry Sync Smoke

Run both configurations:

```powershell
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-extrusion-sync\PanelCladdingExtrusionSyncSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-extrusion-sync\PanelCladdingExtrusionSyncSmoke.csproj -c Release
```

The executable verifies topology-to-curve expansion, underscore naming, PID/CRV/CID metadata,
the new/legacy surface-root contract, the `PCSync` command name, metadata repair planning that does
not trust old object attributes, and five-decimal unit dimensions.
