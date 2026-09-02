# Panel cladding blank-cell persistence execution report

## Corresponding plan

- Plan: `Project_Plan/260819_PLAN_panel-cladding-blank-cell-persistence.md`
- Execution date: 2026-08-19

## Related artifacts

- Tests: `Project_Test/260819_TEST_panel-cladding-blank-cell-persistence/`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.46/`
- Installed RHP: `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.46\PanelCladdingEditor.rhp`
- Commit / PR: none created

## Execution result / actual scope

- Added `PanelCladdingKeyService.PersistedBlankCellValue` and `EncodeCellValueForStorage` as the Rhino storage-boundary representation for an unassigned cell.
- `PanelCladdingSaveService` now encodes every surviving empty logical-cell write as one regular space. Rhino receives a non-empty user string, while existing readers trim the stored value back to `string.Empty`.
- Assigned materials and parent-cell tokens are written unchanged.
- Logical collapse behavior remains intact: hidden physical members of merged cells and cells removed by complete-track normalization remain obsolete and are deleted.
- Type/signature generation still receives domain-empty values, so blank persistence does not change panel type identity.
- Bumped and packaged PanelCladdingEditor `1.0.46`, then installed and validated its current-user Rhino 8 registration.

## Deviations from plan

- The new standalone smoke folder also required an exact exclusion in `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`. That project intentionally compiles selected `Project_Test/**/*.cs` files; standalone executable smoke sources must be excluded from that aggregation.
- No production behavior deviated from the plan.

## Problems found and fixed during construction

- An initial assembly-identity run failed because the MCP server picked up the new standalone test's `Program.cs` and generated `obj` sources. Adding the exact test-folder exclusion restored the repository build without broadly excluding other historical smoke files.
- RhinoCommon `ObjectAttributes` cannot be instantiated in an ordinary PowerShell process because its native runtime is initialized by the Rhino host. The focused test therefore verifies the complete Save write contract, parser round-trip, and signature equivalence; installer validation verifies the deployed plug-in and registration.

## Test record

Focused Debug and Release smoke:

```powershell
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-blank-cell-persistence\PanelCladdingBlankCellPersistenceSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-blank-cell-persistence\PanelCladdingBlankCellPersistenceSmoke.csproj -c Release
```

Result: both exit 0. Assertions cover all-blank 2x3 writes, mixed material/parent/blank values, stored-blank parsing, signature equivalence, and deletion of a hidden physical member in a partial merge.

Regression smokes:

```powershell
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-logical-cell-cleanup\PanelCladdingLogicalCellCleanupSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-full-track-collapse\PanelCladdingFullTrackCollapseSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-topology-persistence\PanelCladdingTopologyPersistenceSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-cell-topology\PanelCladdingCellTopologySmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_pcmatch-parent-fidelity\PanelCladdingMatchParentFidelitySmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-split-spawn-sync\PanelCladdingSplitSpawnSyncSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-spawn\PanelCladdingSpawnSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260807_TEST_panel-cladding-surface-sync\PanelCladdingSurfaceSyncSmoke.csproj -c Debug
```

Result: all exit 0.

Debug and Release solution validation:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug -m:1 --nologo
dotnet build .\MCP_Rhino.sln -c Release -m:1 --nologo
```

Result: both exit 0 with 0 warnings and 0 errors.

Package and identity validation:

```powershell
powershell -ExecutionPolicy Bypass -File .\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1 -Version 1.0.46
.\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Debug
.\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Release -SkipBuild -PluginPath <MCP-RHP>,<packaged-PanelCladdingEditor-RHP>
```

Result: package build exit 0. Debug and packaged Release both declare PanelCladdingEditor plug-in id `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, matching the package manifest and remaining distinct from MCP_Rhino.

Installer validation:

```powershell
Install-PanelCladdingEditor.ps1 -Mode Install -BundleRoot .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.46
Install-PanelCladdingEditor.ps1 -Mode Validate -BundleRoot .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.46
```

Result: installation and validation exit 0. The installed RHP exists, and Rhino 8's HKCU registration points to `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.46\PanelCladdingEditor.rhp`.

## Acceptance alignment

- Every surviving unassigned logical-cell key is written: passed.
- Stored blank values parse back to unassigned: passed.
- Mixed material, parent, and blank values retain their semantics: passed.
- Hidden/obsolete physical cells are still deleted: passed.
- Blank persistence does not affect type identity: passed.
- Existing topology, match, spawn, and sync regressions: passed.
- Debug/Release builds, package identity, install, and registry validation: passed.

## Rollback verification

- Remove `EncodeCellValueForStorage` from the Save write projection to restore direct empty-string writes.
- Restore package version `1.0.45` and reinstall that artifact if deployment rollback is required.
- No Rhino document was opened or modified during this execution.

## Current remaining items

- Reopen Rhino to load `1.0.46`.
- A live PCEditor Save can be used to visually confirm that an unassigned cell is present in Rhino Attributes with an empty-looking value; this is a user-facing confirmation rather than a code or deployment blocker.

## Conclusion

PCEditor now preserves the current logical layout in panel attributes even when cladding materials are unassigned. Blank cells remain structurally present without changing signature semantics, and build `1.0.46` is installed and validated.
