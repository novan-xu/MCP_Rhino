# Results — 2026-09-03

## Focused regression

Both static/contract configurations exited `0`:

```powershell
dotnet run --project .\Project_Test\260903_TEST_pcupdate-command-undo\PCUpdateCommandUndoSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260903_TEST_pcupdate-command-undo\PCUpdateCommandUndoSmoke.csproj -c Release
```

Assertions confirmed:

- `PCUpdate` first reads `RhinoDoc.CurrentUndoRecordSerialNumber`;
- `BeginUndoRecord` is reached only when no ambient record exists;
- `EndUndoRecord` and automatic `RhinoDoc.Undo()` rollback remain inside the service-owned branch;
- package version `1.0.71` carries the correction.

The optional native RhinoCore probe was attempted. Without Rhino's System directory on `PATH`, the
test host could not locate `RhinoLibrary`; after adding the installed Rhino 8 System directory, the
in-process startup returned `COMException (0x80004005)`. This environment therefore supplied no
native RhinoCore attestation. The static regression and compiled production code passed, but the
first installed run should still confirm command-lifecycle behavior in Rhino.

## Production and solution builds

All final build commands exited `0` with zero warnings and zero errors:

```powershell
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Debug --nologo
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Release --nologo
dotnet build .\MCP_Rhino.sln -c Debug --nologo
dotnet build .\MCP_Rhino.sln -c Release --nologo
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Release -t:Rebuild --nologo
```

The solution-level builds were required because the new standalone regression was added to the
MCP server project's precise test-source exclusion list.

## Existing regressions

The following final runs exited `0`:

```powershell
dotnet run --project .\Project_Test\260903_TEST_panel-cladding-update-command\PanelCladdingUpdateCommandSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260903_TEST_panel-cladding-update-command\PanelCladdingUpdateCommandSmoke.csproj -c Release
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-pc-commands\PanelCladdingPcCommandsSmoke.csproj -c Release
```

One concurrent Release attempt encountered `CS2012` because two tests tried to write the same
intermediate `PanelCladdingEditor.dll`; the same update smoke was immediately rerun serially and
passed. This was a test-runner file lock, not a source or build failure.

## Package and identity

The package build exited `0` and created the 28-file bundle:

```text
Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.71
```

The isolated registry-only installation regression passed complete install, shorthand rejection,
incomplete-command rejection, and repair:

```powershell
powershell -ExecutionPolicy Bypass -File .\Project_Test\260826_TEST_panel-cladding-plugin-registration\Test-PanelCladdingPluginRegistration.ps1 -BundleRoot .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.71
```

The Release plug-in identity gate passed for both products. `PanelCladdingEditor.rhp` retained
assembly/plugin GUID `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, distinct from MCP_Rhino.

Direct-build and packaged RHP SHA-256 values match exactly:

```text
4ABC46259803D3C8F3EED0F80EACADE1E3EEB08207FB439B468ECB36491161BA
```

No production installation, registry activation, prior-version move, or live Rhino document
mutation was performed.
