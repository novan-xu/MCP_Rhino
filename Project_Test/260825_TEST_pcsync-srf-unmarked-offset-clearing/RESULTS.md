# PCSyncSrf Unmarked Offset Clearing Results

Execution date: 2026-08-25

## Focused smoke

Commands:

```powershell
dotnet run --project Project_Test/260825_TEST_pcsync-srf-unmarked-offset-clearing/PCSyncSrfUnmarkedOffsetClearingSmoke.csproj -c Debug
dotnet run --project Project_Test/260825_TEST_pcsync-srf-unmarked-offset-clearing/PCSyncSrfUnmarkedOffsetClearingSmoke.csproj -c Release
```

Both commands exited `0` and reported:

```text
[OK] unmarked parent relationships cannot preserve moved offsets
[OK] an unmarked geometry-missing stored track is cleared
[OK] complete merged coverage still protects its hidden track
[OK] coverage-blind planning never preserves stored tracks
```

The exact regression used stored H offsets `21.375, 107.25, 169.09079`, current surface offsets `19.625, 105.75, 169.09079`, stale curves at the old values, and a misleading PCEditor parent graph. With no baked coverage values, the result was exactly `19.625, 105.75, 169.09079`.

## Existing regressions

The following managed Debug smoke projects exited `0`:

- `Project_Test/260825_TEST_cladding-surface-coverage-metadata/CladdingSurfaceCoverageMetadataSmoke.csproj`
- `Project_Test/260825_TEST_pcsync-srf-offset-refresh/PCSyncSrfOffsetRefreshSmoke.csproj`
- `Project_Test/260819_TEST_pcsync-srf-preserve-structural-grid/PCSyncSrfPreserveStructuralGridSmoke.csproj`
- `Project_Test/260819_TEST_panel-cladding-surface-sync-structural-grid/PanelCladdingSurfaceSyncStructuralGridSmoke.csproj`
- `Project_Test/260805_TEST_panel-cladding-spawn/PanelCladdingSpawnSmoke.csproj`
- `Project_Test/260807_TEST_panel-cladding-surface-sync/PanelCladdingSurfaceSyncSmoke.csproj`
- `Project_Test/260819_TEST_pcsync-srf-parent-persistence/PCSyncSrfParentPersistenceSmoke.csproj`
- `Project_Test/260819_TEST_panel-cladding-logic-persistence/PanelCladdingLogicPersistenceSmoke.csproj`
- `Project_Test/260819_TEST_panel-cladding-layout-reconciliation/PanelCladdingLayoutReconciliationSmoke.csproj`
- `Project_Test/260812_TEST_panel-cladding-offset-sync/PanelCladdingOffsetSyncSmoke.csproj`

The coverage regression confirms that explicit `0A;1A` still preserves a hidden track and complete single-cell marks clear moved boundaries. Rhino-native Brep checks retained their existing `[SKIP]` condition outside a Rhino native host; their managed and source-contract checks passed.

One initial parallel regression invocation encountered a shared WPF `MarkupCompile` cache lock. The affected coverage test was rerun sequentially and exited `0`; this was a test-process collision, not a product build failure.

## Builds

Commands:

```powershell
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Debug --nologo
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Release --nologo
```

Both builds exited `0` with `0 Warning(s)` and `0 Error(s)`.

## Package, identity, and current-user activation

Rhino process inspection returned `RHINO_EXITED`. Package `1.0.65` was built at:

```text
Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.65/
```

Assembly validation confirmed:

- PanelCladdingEditor GUID: `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`;
- MCP_Rhino GUID: `7a3fc2f0-24a8-4b79-be58-5a08cfb0d10a`;
- both GUIDs are non-empty and distinct.

The registry-only installer's `Install` and separate `Validate` modes both exited `0`. Packaged and installed RHP SHA-256 values both equal:

```text
97000EBD103617741FBD06B924DECC6B429CBFE9529B7F3E70B328CDF71B8D67
```

HKCU registration targets `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.65\PanelCladdingEditor.rhp` with `LoadMode=1` and `DirectoryInstall=0`.

## Diff audit

Targeted `git diff --check` completed with no whitespace errors. Git emitted only LF-to-CRLF working-copy notices.

## Runtime verification boundary

No Rhino document was opened or mutated. Native live-model verification remains for the next Rhino session because Windows UI automation was not authorized and no Rhino-native test host was used.
