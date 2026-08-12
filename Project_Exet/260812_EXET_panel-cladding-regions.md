# Panel Cladding Regions Execution

## Corresponding Plan

- Plan: `Project_Plan/260812_PLAN_panel-cladding-regions.md`
- Execution date: 2026-08-12

## Associated Artifacts

- Test folder: `Project_Test/260812_TEST_panel-cladding-regions/`
- Commit / PR: none created in this execution

## Execution Result / Actual Scope

- Added owner/reference region resolution for panel cell values. A material owns a region; a value
  matching a cell label references that region.
- Added direct-reference normalization, deterministic owner selection (lowest row then lowest
  column), cycle/unknown/blank-target checks, and edge-connectivity validation.
- Replaced cell-based spawn plans with region-based plans. The live adapter now builds the same
  trimmed atomic Breps as before and joins all cells in one region into one Rhino Brep object.
- Added shared live geometry partitioning used by both spawn and Sync From Surfaces.
- Added surface-footprint classification based on interior mesh samples plus exact Brep area checks.
  Sync rejects partial cells, gaps, overlaps, unknown cells, and disconnected footprints.
- Sync now derives panel owner/reference values from one Rhino object per region, normalizes each
  surface CID to the canonical owner, refreshes `Cladding` from the layer, and keeps the existing
  transactional workbook/Rhino attribute commit and Undo behavior.
- Duplicate CIDs inherited during a manual split are repairable when geometry coverage is available;
  legacy metadata-only duplicate mappings continue to fail closed.
- Added support for reading material surfaces under the BKT root
  `03_Material Surfaces (STP)` as well as `02_Material Surfaces`. Spawn continues to use
  `02_Material Surfaces` as its default root.
- Advanced type identity to schema v3. The canonical payload includes millimeter-normalized panel
  size, H/V offsets, materials, and normalized owner references.
- Updated the editor field label to `Material or owner cell` and retained normalized reference values
  in panel attributes and workbook cells.
- Added an exact test-folder exclusion to `MCP_Rhino.Server.csproj` because the server intentionally
  compiles other `Project_Test/**/*.cs` smoke partials, while this capability uses a standalone test
  executable.

## Variance From Plan

- The live BKT inspection showed its current cladding surfaces under
  `03_Material Surfaces (STP)`. Exact support for that root was added during execution so surface
  sync can consume the condition that motivated the capability.
- The focused smoke includes a Rhino Brep partition/join/coverage probe, but Rhino's native
  `rhcommon_c` runtime does not initialize in a standalone `dotnet run` process. The probe reports a
  deliberate `[SKIP]`; live-host verification steps are documented in the TEST README.
- No package staging, installation, or mutation of the open BKT document was performed because the
  request authorized source updates, not deployment or live-model writes.

## Problems Found And Fixed During Construction

- The first solution build compiled the new standalone test `Program.cs` into `MCP_Rhino.Server`
  through the repository-wide test glob. Added the exact test-folder exclusion used by the other
  standalone PanelCladdingEditor smokes.
- A parallel solution build exposed the existing same-output race between the PanelCladdingEditor
  RHP build and its test-host DLL build. Final solution validation used `-m:1`, and final plug-in
  validation rebuilt the RHP project directly in each configuration.
- Existing surface-sync fixtures intentionally used different offsets while expecting one v2 type.
  The fixtures were aligned where they test type reuse; the new focused test separately verifies
  that v3 distinguishes different offsets.
- Duplicate surface CIDs were initially rejected before inspecting supplied footprints. The planner
  was adjusted so geometry-backed split surfaces can normalize their CIDs while metadata-only
  ambiguity remains an error.

## Test Record

All listed commands completed with exit code 0 unless noted.

```powershell
dotnet run --project .\Project_Test\260812_TEST_panel-cladding-regions\PanelCladdingRegionsSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260812_TEST_panel-cladding-regions\PanelCladdingRegionsSmoke.csproj -c Release
```

Each focused run reported four `[OK]` groups covering reference resolution, region spawn planning,
v3 identity, and surface-sync reconstruction. Each also reported one expected live-host geometry
`[SKIP]`.

```powershell
dotnet run --project .\Project_Test\260804_TEST_standalone-panel-cladding-editor\PanelCladdingEditorSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260804_TEST_standalone-panel-cladding-editor\PanelCladdingEditorSmoke.csproj -c Release
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-spawn\PanelCladdingSpawnSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-spawn\PanelCladdingSpawnSmoke.csproj -c Release
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-match\PanelCladdingMatchSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-match\PanelCladdingMatchSmoke.csproj -c Release
dotnet run --project .\Project_Test\260807_TEST_panel-cladding-clear\PanelCladdingClearSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260807_TEST_panel-cladding-clear\PanelCladdingClearSmoke.csproj -c Release
dotnet run --project .\Project_Test\260807_TEST_panel-cladding-surface-sync\PanelCladdingSurfaceSyncSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260807_TEST_panel-cladding-surface-sync\PanelCladdingSurfaceSyncSmoke.csproj -c Release
```

All five existing standalone smoke suites passed in both configurations.

```powershell
dotnet build .\MCP_Rhino.sln -c Debug -m:1 --nologo
dotnet build .\MCP_Rhino.sln -c Release -m:1 --nologo
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Debug --nologo
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Release --nologo
```

Both serial solution builds and both direct RHP builds completed with 0 warnings and 0 errors.

The metadata probe was run against both final RHP files:

```text
Debug   pluginId=7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35 declared=True
Release pluginId=7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35 declared=True
```

The value matches `AssemblyInfo.cs`, `PanelCladdingEditorPlugin`, and
`Packaging/PanelCladdingEditor/package-manifest.json`.

## Deployment Verification

After Rhino was closed, the release bundle was rebuilt and installed with the repository-owned
registry installer:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.21\Installer\Install-PanelCladdingEditor.ps1 -Mode Repair -BundleRoot .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.21
powershell -NoProfile -ExecutionPolicy Bypass -File .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.21\Installer\Install-PanelCladdingEditor.ps1 -Mode Validate -BundleRoot .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.21
```

The installer reported a valid registry-only `1.0.21` installation at
`%LOCALAPPDATA%\PanelCladdingEditor\plugin\1.0.21`. Independent checks confirmed:

- installed and bundle RHP SHA-256:
  `d58eaabb1eb109e08e9e9540e5f5274b4012b1da39a4b2143f390ded1e4ee492`
- installed assembly GUID: `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, declared at assembly level
- Rhino registry `FileName` points to the installed RHP with `LoadMode=1` and
  `DirectoryInstall=0`

## Acceptance Alignment

- `GLS-001 / 0A / GLS-002` resolves and plans exactly two regions and two owner CIDs: passed.
- Equal adjacent materials without references remain separate regions: passed.
- Chains normalize; cycles, unknown targets, blank targets, and disconnected groups fail: passed.
- Joined-object coverage reconstructs owner/follower values; split same-material objects remain
  separate: passed in application planning, including missing and duplicate CID repair.
- Overlap, missing coverage, disconnected coverage, invalid roots, and partial-cell diagnostics fail
  closed: passed for deterministic planning; partial-cell Brep verification remains a live-host test.
- BKT `03_Material Surfaces (STP)` material-layer authority: passed in focused planning smoke.
- v3 type identities distinguish merge topology and offset geometry: passed.
- Debug/Release builds, regression smokes, and assembly GUID contract: passed.

## Rollback Verification

- Source rollback consists of reverting the files listed in the plan plus the exact standalone-test
  exclusion in `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`.
- The installer transaction preserved the prior active `1.0.21` files under
  `%LOCALAPPDATA%\PanelCladdingEditor\rollback`. Use the repository uninstaller or restore the
  latest installer-owned rollback entry while Rhino is closed if deployment rollback is required.
- No workbook mutation or live Rhino document mutation was performed during construction or
  deployment.
- Runtime spawn and sync changes retain one Rhino Undo record per successful command. Failed sync
  preparation or workbook finalization continues to leave no partial panel/surface attribute writes.

## Current Remaining Items

- Run the live fixture in `Project_Test/260812_TEST_panel-cladding-regions/README.md` after deploying
  the updated RHP. Confirm region joining and geometry-derived coverage on a copy or controlled panel
  in the open BKT document, especially for stepped curved-panel regions.

## Conclusion

Panel cladding owner/reference regions are implemented across panel parsing, save identity, spawn,
surface sync, workbook serialization, and regression coverage. The source and build artifacts are
installed and ready for live Rhino acceptance testing.
