# Panel cladding unit-type verification

Executed 2026-10-06. All eight regression runs passed with exit 0. These use
application snapshots and source contracts; no live Rhino geometry was changed.

## Regression commands

From the repository root:

```powershell
dotnet run --project Project_Test/260923_TEST_panel-cladding-role-cid/PanelCladdingRoleCidSmoke.csproj -c Debug
dotnet run --project Project_Test/260923_TEST_panel-cladding-role-cid/PanelCladdingRoleCidSmoke.csproj -c Release
dotnet run --project Project_Test/260903_TEST_panel-cladding-update-command/PanelCladdingUpdateCommandSmoke.csproj -c Debug
dotnet run --project Project_Test/260903_TEST_panel-cladding-update-command/PanelCladdingUpdateCommandSmoke.csproj -c Release
dotnet run --project Project_Test/260805_TEST_panel-cladding-spawn/PanelCladdingSpawnSmoke.csproj -c Debug
dotnet run --project Project_Test/260807_TEST_panel-cladding-surface-sync/PanelCladdingSurfaceSyncSmoke.csproj -c Debug
dotnet run --project Project_Test/260818_TEST_panel-cladding-extrusion-sync/PanelCladdingExtrusionSyncSmoke.csproj -c Debug
dotnet run --project Project_Test/260903_TEST_panel-cladding-managed-cid-scope/PanelCladdingManagedCidScopeSmoke.csproj -c Debug
```

The updated role fixture exercises `corner_parent`, `corner_child`, `flat`, and
absent unit type through spawn/update planning, all three editor save scopes,
PCCreate, and both sync scopes. Both legacy flags are deliberately set on every
fixture. Recognized types correct a stale CID; flat clears its suffix. Additional
assertions cover mixed casing/whitespace, unknown values, missing PID/CID, custom
CIDs, repeated execution, and sibling dependency isolation. PCUpdate fixtures
cover shared PIDs, duplicate-CID partial selection, and flat CID normalization
before duplicate classification.

## Build and package commands

```powershell
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Debug --nologo
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Release --nologo
& Project_Test/260805_TEST_rhino-plugin-assembly-identity/Verify-PluginAssemblyIdentity.ps1 -Configuration Release -SkipBuild
$packageOutput = Join-Path (Get-Location).Path '.tmp-package-unit-type-261006'
# A fresh, checked workspace path was used; the script can replace its own output.
& Packaging/PanelCladdingEditor/Build-PanelCladdingEditorPackage.ps1 -OutputRoot $packageOutput
$stagePath = Join-Path $env:LOCALAPPDATA 'PanelCladdingEditor\staged\1.0.85-unit-type-261006'
# The destination did not exist when staged.
Copy-Item -LiteralPath (Join-Path $packageOutput 'PanelCladdingEditor-1.0.85') -Destination $stagePath -Recurse
$serverRhp = Join-Path (Get-Location).Path 'src/MCP_Rhino.Server/bin/Release/net8.0/MCP_Rhino.Server.rhp'
& Project_Test/260805_TEST_rhino-plugin-assembly-identity/Verify-PluginAssemblyIdentity.ps1 -Configuration Release -SkipBuild -PluginPath @($serverRhp, (Join-Path $stagePath 'Plugin/PanelCladdingEditor.rhp'))
git diff --check
```

Debug/Release standalone builds and package publish/rebuild passed with zero
warnings/errors. GUID inspection passed before packaging and on the staged RHP,
matching the manifest and remaining distinct from MCP_Rhino. All 27 bundle file
hashes match. Portable results and the RHP hash are in
[verification-summary.json](verification-summary.json); raw command logs are
ignored local evidence in this folder.

Rhino was running version 1.0.84. Version 1.0.85 is staged only; its installer and
production Validate have not been run. Native Rhino acceptance remains pending.
