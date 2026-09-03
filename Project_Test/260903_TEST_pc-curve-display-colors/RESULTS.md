# Results — 2026-09-03

## Focused regression

Both configurations exited `0`:

```powershell
dotnet run --project .\Project_Test\260903_TEST_pc-curve-display-colors\PCCurveDisplayColorsSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260903_TEST_pc-curve-display-colors\PCCurveDisplayColorsSmoke.csproj -c Release
```

Assertions confirmed:

- every main-frame plan uses object color Blue (`RGB 0,0,255`) regardless of axis;
- horizontal intermediate segment/merged plans use Purple (`RGB 128,0,128`);
- vertical intermediate segment/merged plans use DarkGreen (`RGB 0,100,0`);
- extrusion plans carry the resolved authoritative color;
- a wrong color source or wrong RGB marks an existing curve for synchronization;
- an already-correct object-colored curve does not produce false color drift;
- `PCSpawnCrv`, `PCUpdate`, and `PCSyncCrv` write or repair curve object color;
- package version `1.0.73` carries the change.

## Existing regressions

The following final runs exited `0` in both Debug and Release:

```powershell
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-extrusion-sync\PanelCladdingExtrusionSyncSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-extrusion-sync\PanelCladdingExtrusionSyncSmoke.csproj -c Release
dotnet run --project .\Project_Test\260903_TEST_panel-cladding-update-command\PanelCladdingUpdateCommandSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260903_TEST_panel-cladding-update-command\PanelCladdingUpdateCommandSmoke.csproj -c Release
dotnet run --project .\Project_Test\260903_TEST_pcupdate-command-undo\PCUpdateCommandUndoSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260903_TEST_pcupdate-command-undo\PCUpdateCommandUndoSmoke.csproj -c Release
dotnet run --project .\Project_Test\260903_TEST_pcupdate-locked-managed-objects\PCUpdateLockedManagedObjectsSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260903_TEST_pcupdate-locked-managed-objects\PCUpdateLockedManagedObjectsSmoke.csproj -c Release
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-pc-commands\PanelCladdingPcCommandsSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-pc-commands\PanelCladdingPcCommandsSmoke.csproj -c Release
```

The Undo regression's optional RhinoCore branch remained skipped in the normal test host, as its
output documents. The source/API regressions for ambient Undo ownership and locked/hidden managed
objects passed.

## Builds

All final builds exited `0` with zero warnings and zero errors:

```powershell
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Debug --nologo
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Release --nologo
dotnet build .\MCP_Rhino.sln -c Debug --nologo
dotnet build .\MCP_Rhino.sln -c Release --nologo
```

Solution-level builds were included because the focused standalone test required one precise
MCP-server test-source exclusion.

## Package and identity

The package build exited `0` and created the 28-file bundle:

```text
Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.73
```

The isolated registry-only installation regression passed complete install, shorthand rejection,
incomplete-command rejection, and repair:

```powershell
powershell -ExecutionPolicy Bypass -File .\Project_Test\260826_TEST_panel-cladding-plugin-registration\Test-PanelCladdingPluginRegistration.ps1 -BundleRoot .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.73
```

The Release assembly identity gate passed for MCP_Rhino and PanelCladdingEditor. The editor retained
assembly/plugin GUID `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35` and remained distinct from MCP_Rhino.

Direct-build and packaged RHP SHA-256 values match exactly:

```text
DCD529C51FC829C297239494F6D360C0456B595D9D653C04A5C14294FC31BFD2
```

`git diff --check` exited `0`; its output contained existing line-ending notices only.

No production installation, registry activation, Rhino layer/object mutation, or live document
mutation was performed during construction.
