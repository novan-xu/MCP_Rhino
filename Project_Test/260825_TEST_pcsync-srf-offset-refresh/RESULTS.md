# PCSyncSrf Offset Refresh Results

Execution date: 2026-08-25

## Focused smoke

Commands:

```powershell
dotnet run --project Project_Test/260825_TEST_pcsync-srf-offset-refresh/PCSyncSrfOffsetRefreshSmoke.csproj -c Debug
dotnet run --project Project_Test/260825_TEST_pcsync-srf-offset-refresh/PCSyncSrfOffsetRefreshSmoke.csproj -c Release
```

Both commands exited `0` and reported:

- moved Level 5 H offsets replace old indexed values;
- a proven non-splitting structural track remains without geometry evidence;
- a proven structural track follows its current associated curve;
- a genuinely unmatched curve adds a new structural track;
- live `PCSyncSrf` uses the role-aware layout overload.

The reported numeric regression resolves stored `21.375, 107.25, 169.09079` plus current surface `19.625, 105.75, 169.09079` and stale curve evidence to exactly `19.625, 105.75, 169.09079`.

## Existing regressions

The following Debug console smoke projects all exited `0`:

- `Project_Test/260819_TEST_pcsync-srf-preserve-structural-grid/PCSyncSrfPreserveStructuralGridSmoke.csproj`
- `Project_Test/260819_TEST_panel-cladding-surface-sync-structural-grid/PanelCladdingSurfaceSyncStructuralGridSmoke.csproj`
- `Project_Test/260819_TEST_pcsync-srf-parent-persistence/PCSyncSrfParentPersistenceSmoke.csproj`
- `Project_Test/260819_TEST_panel-cladding-layout-reconciliation/PanelCladdingLayoutReconciliationSmoke.csproj`
- `Project_Test/260807_TEST_panel-cladding-surface-sync/PanelCladdingSurfaceSyncSmoke.csproj`
- `Project_Test/260812_TEST_panel-cladding-offset-sync/PanelCladdingOffsetSyncSmoke.csproj`

The two geometry-inference checks that require Rhino native hosting reported their existing `[SKIP]` condition. All managed planning, persistence, orchestration, and source-contract assertions ran and passed.

## Builds

Commands:

```powershell
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Debug
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Release
```

Both builds exited `0` with `0 Warning(s)` and `0 Error(s)`, producing:

- `src/PanelCladdingEditor/bin/Debug/net8.0-windows/PanelCladdingEditor.rhp`
- `src/PanelCladdingEditor/bin/Release/net8.0-windows/PanelCladdingEditor.rhp`

## Diff audit

`git diff --check` on the two implementation files and this capability's PLAN/TEST artifacts produced no whitespace errors. Git emitted only the repository's existing LF-to-CRLF working-copy warning for the two C# files.

## Package and current-user activation

After Rhino fully exited, the existing package version was rebuilt and installed:

```powershell
powershell -ExecutionPolicy Bypass -File Packaging/PanelCladdingEditor/Build-PanelCladdingEditorPackage.ps1 -Version 1.0.63
Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.63/Installer/Install-PanelCladdingEditor.ps1 -Mode Install
Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.63/Installer/Install-PanelCladdingEditor.ps1 -Mode Validate
```

Results:

- package build exited `0` with zero warnings and zero errors;
- assembly identity validation reported PanelCladdingEditor GUID `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, non-empty and distinct from MCP_Rhino;
- packaged and installed RHP SHA-256 both equal `B25B543CB8E0FFABA28B58B727E9F07908F0FF7C8063F0AB047B8C746E186C52`;
- installer validation reported the registry-only installation valid;
- HKCU registration targets `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.63\PanelCladdingEditor.rhp` with `LoadMode=1` and `DirectoryInstall=0`.
