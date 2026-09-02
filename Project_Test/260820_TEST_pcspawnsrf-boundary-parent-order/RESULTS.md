# PCSpawnSrf Boundary, Parent Order, And Material Color Results

Execution date: 2026-08-20

## Focused smoke

Commands:

```powershell
dotnet run --project .\Project_Test\260820_TEST_pcspawnsrf-boundary-parent-order\PCSpawnSrfBoundaryParentOrderSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260820_TEST_pcspawnsrf-boundary-parent-order\PCSpawnSrfBoundaryParentOrderSmoke.csproj -c Release
```

Both commands exited 0 and reported:

- `[OK]` the shared `0A`/`1A` divider is cancelled from the configured region boundary;
- `[OK]` internal boundary segments are distinguished from panel-perimeter trims;
- `[OK]` the parent list is `0A-0D`, then `1A-1D`;
- `[OK]` a temporary Material Setup workbook round-trips `XX=#123456`, and spawn planning receives
  exact RGB `(18, 52, 86)` with deterministic fallback for an absent catalog item;
- `[SKIP]` the one-face RhinoCommon geometry branch requires a running Rhino native host.

## Regression smokes

The following passed with exit code 0:

```powershell
dotnet run --project .\Project_Test\260812_TEST_panel-cladding-regions\PanelCladdingRegionsSmoke.csproj -c Debug --no-restore
dotnet run --project .\Project_Test\260812_TEST_panel-cladding-regions\PanelCladdingRegionsSmoke.csproj -c Release --no-restore
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-spawn\PanelCladdingSpawnSmoke.csproj -c Debug --no-restore
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-spawn\PanelCladdingSpawnSmoke.csproj -c Release --no-restore
dotnet run --project .\Project_Test\260818_TEST_material-setup-color-picker\MaterialSetupColorPickerSmoke.csproj -c Debug --no-restore
dotnet run --project .\Project_Test\260818_TEST_material-setup-color-picker\MaterialSetupColorPickerSmoke.csproj -c Release --no-restore
dotnet run --project .\Project_Test\260813_TEST_panel-cladding-direct-interactions\PanelCladdingDirectInteractionsSmoke.csproj -c Debug --no-restore
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-full-track-collapse\PanelCladdingFullTrackCollapseSmoke.csproj -c Debug --no-restore
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-surface-sync-structural-grid\PanelCladdingSurfaceSyncStructuralGridSmoke.csproj -c Debug --no-restore
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-split-spawn-sync\PanelCladdingSplitSpawnSyncSmoke.csproj -c Debug --no-restore
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-logic-persistence\PanelCladdingLogicPersistenceSmoke.csproj -c Debug --no-restore
```

The region and structural-grid suites also report their documented Rhino-native geometry skips in
a standalone process. All pure planning, command-contract, persistence, UI interaction, and
fallback-color assertions passed.

## Builds

Commands:

```powershell
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Debug --no-restore --nologo
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Release --no-restore --nologo
dotnet build .\MCP_Rhino.sln -c Debug -m:1 --no-restore --nologo
dotnet build .\MCP_Rhino.sln -c Release -m:1 --no-restore --nologo
```

All four builds exited 0 with 0 warnings and 0 errors. The standalone RHP was rebuilt directly
after solution validation.

## Assembly identity

Commands:

```powershell
.\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Debug -SkipBuild
.\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Release -SkipBuild
```

Both exited 0. `PanelCladdingEditor` declares plug-in id
`7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, matching its manifest and remaining distinct from
`MCP_Rhino`. The final Release RHP SHA-256 is
`5082EB190039AE5E67E17EADB851C80834BA2C9F16F7455F9040A23A07ECBFAD`.

## Independent pre-existing failure

This diagnostic command was also run:

```powershell
dotnet run --project .\Project_Test\260813_TEST_panel-cladding-material-catalog\PanelCladdingMaterialCatalogSmoke.csproj -c Debug --no-restore
```

Its material catalog create/write/read assertions completed before the executable stopped at the
unrelated existing Save-pipeline assertion:

```text
Panel save did not write the cladding signature key.
Program.cs:100
```

The failing Save implementation and signature contract were already modified in the incoming dirty
worktree and are not files changed by this capability. The focused workbook round-trip above
isolates and passes the requested Material Setup-to-spawn color behavior.

## Native/live status

No Rhino UI automation, live document mutation, package installation, or deployment was performed.
The single-face Rhino branch is compiled and covered by a Rhino-host assertion, but its live result
must be confirmed using the steps in `README.md` from a running Rhino session.
