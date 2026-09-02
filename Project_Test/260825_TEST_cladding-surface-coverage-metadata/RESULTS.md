# Cladding Surface Coverage Metadata Results

Execution date: 2026-08-25

## Focused smoke

Commands:

```powershell
dotnet run --project Project_Test/260825_TEST_cladding-surface-coverage-metadata/CladdingSurfaceCoverageMetadataSmoke.csproj -c Debug
dotnet run --project Project_Test/260825_TEST_cladding-surface-coverage-metadata/CladdingSurfaceCoverageMetadataSmoke.csproj -c Release
```

Both commands exited `0`. Six managed assertions passed:

- spawn writes canonical single-cell and merged coverage values;
- baked merged coverage preserves its hidden mullion;
- separate baked coverage replaces moved boundaries and suppresses stale curves;
- overlapping and incomplete baked coverage fail before offset planning;
- `PCSyncSrf` migrates and canonicalizes baked coverage metadata;
- the live sync repository reads, validates, rewrites, and persists the key.

The exact reported numeric regression resolves old H offsets `21.375, 107.25, 169.09079` and current surface offsets `19.625, 105.75, 169.09079` to only `19.625, 105.75, 169.09079`. The obsolete values do not survive under new indices.

## Existing regressions

The following managed Debug smoke projects exited `0`:

- `Project_Test/260805_TEST_panel-cladding-spawn/PanelCladdingSpawnSmoke.csproj`
- `Project_Test/260807_TEST_panel-cladding-surface-sync/PanelCladdingSurfaceSyncSmoke.csproj`
- `Project_Test/260819_TEST_panel-cladding-surface-sync-structural-grid/PanelCladdingSurfaceSyncStructuralGridSmoke.csproj`
- `Project_Test/260819_TEST_pcsync-srf-preserve-structural-grid/PCSyncSrfPreserveStructuralGridSmoke.csproj`
- `Project_Test/260819_TEST_pcsync-srf-parent-persistence/PCSyncSrfParentPersistenceSmoke.csproj`
- `Project_Test/260819_TEST_panel-cladding-logic-persistence/PanelCladdingLogicPersistenceSmoke.csproj`
- `Project_Test/260825_TEST_pcsync-srf-offset-refresh/PCSyncSrfOffsetRefreshSmoke.csproj`
- `Project_Test/260812_TEST_panel-cladding-offset-sync/PanelCladdingOffsetSyncSmoke.csproj`
- `Project_Test/260812_TEST_panel-cladding-regions/PanelCladdingRegionsSmoke.csproj`
- `Project_Test/260818_TEST_panel-cladding-extrusion-sync/PanelCladdingExtrusionSyncSmoke.csproj`
- `Project_Test/260819_TEST_panel-cladding-layout-reconciliation/PanelCladdingLayoutReconciliationSmoke.csproj`
- `Project_Test/260819_TEST_panel-cladding-split-spawn-sync/PanelCladdingSplitSpawnSyncSmoke.csproj`
- `Project_Test/260820_TEST_pcspawnsrf-boundary-parent-order/PCSpawnSrfBoundaryParentOrderSmoke.csproj`

The Rhino-native Brep checks reported their existing `[SKIP]` condition outside a Rhino native host. All corresponding managed planning and source-contract checks passed.

## Builds

Commands:

```powershell
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Debug --nologo
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Release --nologo
```

Both builds exited `0` with `0 Warning(s)` and `0 Error(s)`.

## Package, identity, and current-user activation

Package `1.0.64` was built at:

```text
Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.64/
```

Rhino had exited before installation. Assembly validation confirmed:

- PanelCladdingEditor GUID: `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`;
- MCP_Rhino GUID: `7a3fc2f0-24a8-4b79-be58-5a08cfb0d10a`;
- both GUIDs are non-empty and distinct.

The registry-only installer's `Install` and separate `Validate` modes both exited `0`. Packaged and installed RHP SHA-256 values both equal:

```text
10943E4C50BE4649324A93D82A85F2464E7887155B78D626926F75EEFCCFFFA3
```

HKCU registration targets `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.64\PanelCladdingEditor.rhp` with `LoadMode=1` and `DirectoryInstall=0`.

## Diff audit

Targeted `git diff --check` completed with no whitespace errors. Git emitted only the repository's LF-to-CRLF working-copy notices.

## Runtime verification boundary

No Rhino document was opened or mutated during the automated verification. Native live-model and visual verification remain for the next Rhino session because Windows UI automation was not authorized and no Rhino-native test host was used.
