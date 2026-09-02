# PCSyncSrf Parent-Cell Persistence Execution Report

## Corresponding plan

- Plan: `Project_Plan/260819_PLAN_pcsync-srf-parent-persistence.md`
- Execution date: 2026-08-19

## Related artifacts

- Tests: `Project_Test/260819_TEST_pcsync-srf-parent-persistence/`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.54/`
- Installed RHP: `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.54\PanelCladdingEditor.rhp`
- Commit / PR: none created

## Execution result / actual scope

- Separated type-signature normalization from Rhino cell persistence in `PanelCladdingSurfaceSyncService`.
- Type/signature calculation continues using its normalized region graph.
- The panel write now uses the complete geometry-derived surface-sync graph, retaining parent tokens exactly, including `1A=0A`.
- Every inferred layout cell must be present in the planner graph before mutation. An incomplete graph returns `PANEL_CLADDING_SURFACE_SYNC_CELL_VALUE_MISSING`.
- Blank values are encoded with the existing persisted-blank sentinel so the panel's logical structure remains visible in object user text.
- The live repository now carries the corresponding panel write through commit, reads the committed object back, and verifies every planned cell key/value exactly.
- A missing or changed committed parent value returns `PANEL_CLADDING_SURFACE_SYNC_ATTRIBUTE_VERIFY_FAILED` and restores all objects already modified in the batch.
- Package version advanced from `1.0.53` to `1.0.54`.

## Deviations from plan

- No production or test-scope deviation.
- The open Rhino document exposed by the router was unsaved and therefore not eligible for routed live inspection. The correction was validated through exact application/service and commit-postcondition tests.
- Rhino was initially open, so the installer staged `1.0.54`. Rhino closed before the turn ended; the staged installer then activated and validated the package.

## Problems found and fixed during construction

- The surface planner already derived a parent reference for a spanning surface, but orchestration substituted the type-signature normalizer's dictionary as the persistence payload. The persistence contract now explicitly retains the planner's geometry-derived graph.
- Panel writes previously deleted all cell keys and rewrote them without validating the result. Read-back validation now prevents a silent successful sync with a missing `1A` key.
- Blank logical values were passed to Rhino as empty strings. They now use the established retained-blank representation.

## Test record

Focused Debug and Release smoke:

```powershell
dotnet run --project .\Project_Test\260819_TEST_pcsync-srf-parent-persistence\PCSyncSrfParentPersistenceSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_pcsync-srf-parent-persistence\PCSyncSrfParentPersistenceSmoke.csproj -c Release
```

Result: both exit 0.

Focused assertions:

- one spanning cladding produces final panel writes `0A=MPL-001` and `1A=0A`;
- every inferred logical cell is required before commit;
- blank cells use the persisted-blank sentinel;
- exact read-back passes;
- a missing or replaced `1A` value fails postcondition validation.

Regression suites:

- `260819_TEST_panel-cladding-surface-sync-structural-grid`: passed; native Rhino Brep subtest remains skipped outside Rhino host.
- `260807_TEST_panel-cladding-surface-sync`: passed.
- `260819_TEST_panel-cladding-blank-cell-persistence`: passed.

The base surface-sync suite was rerun sequentially after one parallel run encountered a transient WPF markup-cache file lock. The sequential run passed completely.

Solution validation:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug --no-restore
dotnet build .\MCP_Rhino.sln -c Release --no-restore
```

Result: both exit 0 with 0 warnings and 0 errors.

Package and identity validation:

```powershell
.\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1 -Version 1.0.54
.\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Release -PluginPath <MCP-RHP>,<packaged-PanelCladdingEditor-RHP>
```

Result: package build and identity validation exit 0. PanelCladdingEditor declares manifest-matched plug-in id `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`; MCP_Rhino remains distinct. Packaged RHP SHA-256: `1FC8D787394BE61A3145E4A47E18ECBA5067490E495E13F7754D92734719E4F4`.

Installer result:

```text
PanelCladdingEditor 1.0.54 was staged because Rhino is running.
C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.54-20260819155059190
PanelCladdingEditor 1.0.54 installed registry-only at C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.54.
PanelCladdingEditor 1.0.54 registry-only installation is valid.
```

The current-user registry points to the installed `1.0.54` RHP, and its SHA-256 matches the tested package exactly.

## Acceptance alignment

- Final persistence graph retains `1A=0A`: passed.
- Signature normalization cannot substitute the persisted graph: passed.
- Missing cells fail before mutation: passed.
- Blank cell keys remain present: passed.
- Missing or changed committed parent values fail: passed.
- Structural-grid and base surface-sync regressions: passed.
- Debug/Release builds, package, and identity: passed.
- Current-user installation, registry target, and installed-file hash: passed.
- Live command rerun: pending Rhino restart and affected-model selection.

## Rollback verification

- Restore `identity.NormalizedCellValues` as the panel-write payload, remove cell postcondition verification, and reinstall package `1.0.53`.
- No Rhino document object was mutated during this construction turn.

## Current remaining items

- Reopen Rhino and the affected model, run `PCSyncSrf`, and confirm the parent key remains `1A=0A`.

## Conclusion

`PCSyncSrf` now persists the exact geometry-derived logical cell graph independently of type-signature normalization and verifies the committed Rhino attributes before returning success. Package `1.0.54` is installed, registry-validated, and ready for the live rerun after Rhino restarts.
