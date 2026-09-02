# Panel cladding logical-cell cleanup execution

## Corresponding plan

- Plan: `Project_Plan/260819_PLAN_panel-cladding-logical-cell-cleanup.md`
- Execution date: 2026-08-19

## Related artifacts

- Tests: `Project_Test/260819_TEST_panel-cladding-logical-cell-cleanup/`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.44/`
- Commit / PR: none created in this execution

## Execution result / actual scope

- Added an application-layer logical-cell projection that unions physical cells across missing horizontal or vertical extrusion segments.
- Selected each group's representative by lowest row, then lowest column, matching PCEditor display behavior.
- Updated structural Save to write normalized assignments only for logical representatives while retaining the physical H/V lattice and topology masks in the v4 signature.
- Remapped parent labels that target hidden physical cells to their logical representative.
- Kept obsolete-source-key detection unchanged in shape; hidden cells now fall out of the write set and are therefore included in `UserTextDeletes`.
- Bumped the PanelCladdingEditor package from `1.0.43` to `1.0.44`.
- Cleaned the live `PID_BKT_N1_01_11` panel by removing only stale `CW_4.00_CLADDING_0D` and `CW_4.01_CLADDING_1D` user text.

## Deviations from plan

- No functional deviation. The v4 type identity remains physical-grid based as planned.
- The first parallel solution Debug build exposed an existing output collision between direct RHP and test-host DLL builds. Re-running the solution build serialized with `-m:1` completed successfully and was used for acceptance.

## Problems found and fixed during construction

- Root cause: `PanelCladdingSaveService` passed all physical normalized cell values directly into `UserTextWrites`. Since hidden cells were still present in `writes`, obsolete-key detection could never delete them.
- The new projection occurs after signature normalization, preventing a compatibility change to established topology-sensitive type identities.
- A test invocation initially used an incorrect PCMatch smoke project filename; the actual `PanelCladdingMatchParentFidelitySmoke.csproj` was run and passed.

## Test record

Focused Debug smoke:

```powershell
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-logical-cell-cleanup\PanelCladdingLogicalCellCleanupSmoke.csproj -c Debug
```

Result: exit 0; six logical keys written, `0D/1D` deleted, representative values and hidden-parent remap verified.

Focused Release smoke:

```powershell
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-logical-cell-cleanup\PanelCladdingLogicalCellCleanupSmoke.csproj -c Release
```

Result: exit 0 with the same assertions.

Regression smokes:

```powershell
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-topology-persistence\PanelCladdingTopologyPersistenceSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-match-parent-cells\PanelCladdingMatchParentCellsSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_pcmatch-parent-fidelity\PanelCladdingMatchParentFidelitySmoke.csproj -c Debug
```

Result: all exit 0.

Serialized solution validation:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug -m:1
dotnet build .\MCP_Rhino.sln -c Release -m:1
```

Result: both exit 0 with 0 warnings and 0 errors.

Package and identity validation:

```powershell
powershell -ExecutionPolicy Bypass -File .\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1 -Version 1.0.44
powershell -ExecutionPolicy Bypass -File .\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Debug
powershell -ExecutionPolicy Bypass -File .\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Release
```

Result: all exit 0. PanelCladdingEditor declares assembly/plugin GUID `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`; packaged Release RHP was separately probed and matched the manifest.

Live verification:

- Confirmed one Brep on `01_CW Panels::Surfaces-PNL::WT-04` for `PID_BKT_N1_01_11`.
- Preview reported exactly two removals: `0D [MPL-002]` and `1D [0B]`.
- Apply updated one object with zero failures.
- Re-read reported 42 user attributes and confirmed `0C=MPL-002`, `1C=0B`, with no `0D` or `1D` keys.

## Acceptance alignment

- Logical representative writes: passed.
- Obsolete hidden-cell deletes: passed.
- Representative material and parent assignment preservation: passed.
- Hidden parent-label remapping: passed.
- Topology and PCMatch regressions: passed.
- Debug/Release builds and plug-in identity: passed.
- Live target repair: passed.

## Rollback verification

- Code rollback is isolated to removing `PanelCladdingLogicalCellService` and restoring Save's physical normalized value dictionary.
- The live repair is one Rhino undoable attribute operation; the removed values were recorded in the preview (`MPL-002` and `0B`).

## Current remaining items

- After Rhino was closed, version `1.0.44` was installed registry-only at `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.44` and installer validation passed.
- The Rhino registry points plug-in GUID `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35` to that exact RHP.
- The live panel repair was made before Rhino closed; its persistence depends on whether the document was saved during close.

## Conclusion

Structural Save now removes assignment keys for cells hidden by deleted extrusion segments. The reported live panel was corrected and verified, and PanelCladdingEditor `1.0.44` is installed and ready for the next Rhino launch.
