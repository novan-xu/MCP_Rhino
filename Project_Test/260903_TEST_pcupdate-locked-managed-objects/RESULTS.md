# Results — 2026-09-03

## Focused regression

Both configurations exited `0`:

```powershell
dotnet run --project .\Project_Test\260903_TEST_pcupdate-locked-managed-objects\PCUpdateLockedManagedObjectsSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260903_TEST_pcupdate-locked-managed-objects\PCUpdateLockedManagedObjectsSmoke.csproj -c Release
```

Assertions confirmed:

- RhinoCommon exposes `Replace(Guid, GeometryBase, bool ignoreModes)`;
- RhinoCommon exposes `Delete(RhinoObject, bool quiet, bool ignoreModes)`;
- retained PCUpdate dependencies use mode-bypassing geometry replacement;
- stale/duplicate dependencies use mode-bypassing deletion;
- retained object-level `ObjectAttributes.Mode` is reapplied;
- the service contains no unlock/show workaround;
- package version `1.0.72` carries the correction.

## Existing regressions

The following final runs exited `0`:

```powershell
dotnet run --project .\Project_Test\260903_TEST_panel-cladding-update-command\PanelCladdingUpdateCommandSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260903_TEST_panel-cladding-update-command\PanelCladdingUpdateCommandSmoke.csproj -c Release
dotnet run --project .\Project_Test\260903_TEST_pcupdate-command-undo\PCUpdateCommandUndoSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260903_TEST_pcupdate-command-undo\PCUpdateCommandUndoSmoke.csproj -c Release
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-pc-commands\PanelCladdingPcCommandsSmoke.csproj -c Release
```

The Undo regression's optional RhinoCore branch remained skipped in the normal test host as
documented by its own result record.

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
Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.72
```

The isolated registry-only installation regression passed complete install, shorthand rejection,
incomplete-command rejection, and repair:

```powershell
powershell -ExecutionPolicy Bypass -File .\Project_Test\260826_TEST_panel-cladding-plugin-registration\Test-PanelCladdingPluginRegistration.ps1 -BundleRoot .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.72
```

The Release assembly identity gate passed for MCP_Rhino and PanelCladdingEditor. The editor retained
assembly/plugin GUID `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35` and remained distinct from MCP_Rhino.

Direct-build and packaged RHP SHA-256 values match exactly:

```text
B1C731FF3596CA8263F334C2E0C9D9D809273775AD487B18E792C89951A08805
```

`git diff --check` exited `0`; its output contained existing line-ending notices only.

No production installation, registry activation, layer/object state mutation, or live document
mutation was performed during construction.
