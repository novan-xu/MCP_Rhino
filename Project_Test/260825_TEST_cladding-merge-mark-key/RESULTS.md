# Cladding Merge Mark Key Results

Execution date: 2026-08-25

## Focused regression

Commands:

```powershell
dotnet run --project Project_Test/260825_TEST_cladding-merge-mark-key/CladdingMergeMarkKeySmoke.csproj -c Debug
dotnet run --project Project_Test/260825_TEST_cladding-merge-mark-key/CladdingMergeMarkKeySmoke.csproj -c Release
```

Both commands exited `0`. The assertions confirmed:

- the canonical baked key is exactly `Merge_Mark`;
- `CW_1.03_CLADDING_CELLS` is retained only as a migration alias;
- conflicting dual-key values fail closed;
- `PCSpawnSrf` plans contain `Merge_Mark` and never write the legacy key;
- `PCSyncSrf` schedules legacy-key cleanup and canonical persistence;
- the live commit path removes both key names before writing only `Merge_Mark`.

## Existing regressions

The following managed Debug smoke projects exited `0`:

- `Project_Test/260825_TEST_cladding-surface-coverage-metadata/CladdingSurfaceCoverageMetadataSmoke.csproj`
- `Project_Test/260825_TEST_pcsync-srf-unmarked-offset-clearing/PCSyncSrfUnmarkedOffsetClearingSmoke.csproj`
- `Project_Test/260825_TEST_pcsync-srf-offset-refresh/PCSyncSrfOffsetRefreshSmoke.csproj`
- `Project_Test/260805_TEST_panel-cladding-spawn/PanelCladdingSpawnSmoke.csproj`
- `Project_Test/260807_TEST_panel-cladding-surface-sync/PanelCladdingSurfaceSyncSmoke.csproj`
- `Project_Test/260819_TEST_pcsync-srf-parent-persistence/PCSyncSrfParentPersistenceSmoke.csproj`
- `Project_Test/260819_TEST_pcsync-srf-preserve-structural-grid/PCSyncSrfPreserveStructuralGridSmoke.csproj`
- `Project_Test/260819_TEST_panel-cladding-surface-sync-structural-grid/PanelCladdingSurfaceSyncStructuralGridSmoke.csproj`

These checks cover merged-surface hidden tracks, unmarked stale-offset clearing, the exact Level 5 offset refresh, parent persistence, structural-grid reconstruction, spawn metadata, and end-to-end sync planning. The structural-grid suite retained its documented Rhino-native Brep inference skip outside a native test host; its managed checks passed.

## Live open-document migration

Target document:

```text
V:\01 Project Folders\P00020 BayHealth Kent Tower BKT\03-Design-Eng\03-BIM\05-Wireframe\BKT - Wireframe.3dm
```

The migration used the supported Router session and completed as follows:

- legacy cladding surfaces found: `1,495`;
- pre-existing `Merge_Mark` conflicts: `0`;
- canonical writes previewed: `1,495`;
- canonical writes applied: `1,495`;
- values verified before deletion: `1,495`;
- legacy keys deleted: `1,495`;
- legacy keys remaining: `0`;
- final `Merge_Mark` cladding surfaces: `1,495`;
- final migrated values verified: `1,495`;
- failed writes/deletes: `0`.

The write and deletion produced two Rhino Undo records. The document was not automatically saved and must be saved by the user to persist the migration.

## Builds and package

Commands:

```powershell
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Debug --nologo
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Release --nologo
Packaging/PanelCladdingEditor/Build-PanelCladdingEditorPackage.ps1 -Version 1.0.66
```

Both builds exited `0` with zero warnings and zero errors. Package `1.0.66` was built at:

```text
Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.66/
```

Assembly identity validation passed for both shipped plug-ins:

- PanelCladdingEditor: `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`;
- MCP_Rhino: `7a3fc2f0-24a8-4b79-be58-5a08cfb0d10a`;
- both identifiers are non-empty and distinct.

Packaged PanelCladdingEditor RHP SHA-256:

```text
61199036FDE03AA10589ACF028ED74867E95B61C849ED8C8BCCE3C6C7C6B8E22
```

After Rhino exited, the registry-only installer installed `1.0.66` at:

```text
C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.66\PanelCladdingEditor.rhp
```

Installer `Validate` mode passed. Independent verification confirmed that the packaged and installed RHP hashes both equal `61199036FDE03AA10589ACF028ED74867E95B61C849ED8C8BCCE3C6C7C6B8E22`. Both registry paths target the installed RHP with `LoadMode=1`, `DirectoryInstall=0`, and `IsDotNETPlugIn=1`. Assembly identity validation passed again against the installed RHP.

## Diff audit

Targeted `git diff --check` completed without whitespace errors. Production source contains the former key only once, as `LegacyUserTextKey`; the canonical constant is `Merge_Mark`.
