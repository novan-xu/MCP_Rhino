# Results — 2026-09-03

## Focused smoke

Both commands exited `0`:

```powershell
dotnet run --project .\Project_Test\260903_TEST_panel-cladding-managed-cid-scope\PanelCladdingManagedCidScopeSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260903_TEST_panel-cladding-managed-cid-scope\PanelCladdingManagedCidScopeSmoke.csproj -c Release
```

Assertions confirmed:

- canonical roots and descendants are recognized with a segment-aware boundary;
- the legacy STEP root, similar prefixes, and detail-modeling roots are excluded;
- spawn CID results are filtered by requested scope, geometry class, and managed root; and
- live sync discovery uses only the managed STEP/extrusion predicates.

## Plug-in builds

Both commands exited `0` with zero warnings and zero errors:

```powershell
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Debug
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Release
```

Narrow builds were used because the change is confined to the standalone `PanelCladdingEditor`
plug-in; the MCP server, Router, tool registration, and route lifecycle are unchanged.

## Affected regression smokes

The following Debug runs all exited `0`:

```powershell
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-spawn\PanelCladdingSpawnSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260807_TEST_panel-cladding-surface-sync\PanelCladdingSurfaceSyncSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260812_TEST_panel-cladding-offset-sync\PanelCladdingOffsetSyncSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-extrusion-sync\PanelCladdingExtrusionSyncSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-split-spawn-sync\PanelCladdingSplitSpawnSyncSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260820_TEST_pcspawnsrf-boundary-parent-order\PCSpawnSrfBoundaryParentOrderSmoke.csproj -c Debug
```

Two pre-existing Rhino-native probes reported their documented skip because no Rhino native host was
attached; all executable assertions passed.

`git diff --check` exited `0`, with only LF-to-CRLF working-copy warnings.

## Package installation verification

After Rhino was closed, the 1.0.69 package was rebuilt, installed from the generated bundle, and
validated with the product installer. Install and `-Mode Validate` both exited `0`.

- Installed RHP: `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.69\PanelCladdingEditor.rhp`
- Bundle/installed SHA-256: `28cb5e0adf66af5300a6b3fd885fd216771c91044fc53cf2dfd3922307de9e01`
- Installed assembly GUID: `7C1A4D3B-5E29-4F68-9A72-1D8C6B0F4E35`
