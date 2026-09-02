# PCMatchSrf Logical Cell Cleanup Execution Report

## Corresponding plan

- Plan: `Project_Plan/260819_PLAN_pcmatch-srf-logical-cell-cleanup.md`
- Execution date: 2026-08-19

## Related artifacts

- Tests: `Project_Test/260819_TEST_pcmatch-srf-logical-cell-cleanup/`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.48/`
- Staged installation: `C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.48-20260819141109662`
- Commit / PR: none created

## Execution result / actual scope

- Changed `PanelCladdingMatchPlanningService` to collapse parsed source cells through `PanelCladdingLogicalCellService` before constructing PCMatchSrf writes.
- The planner still deletes all existing target cladding-cell keys, but now rewrites only the source's surviving logical representatives.
- Physical source cells hidden by merge topology—specifically the reported `0B` and `1C` case—are no longer recreated as retained blanks.
- Surviving unassigned logical cells still use Rhino's retained-blank representation.
- Material and parent-cell values on surviving cells remain intact.
- Compatibility remains based on generated physical cell codes; actual offsets and segment/merge topology equality remain intentionally ignored.
- Target offsets, masks, type/signature data, identity, and unrelated user text remain unchanged.
- Bumped and packaged PanelCladdingEditor `1.0.48`.

## Deviations from plan

- Installation could not complete during execution because Rhino was running. The validated package was staged by the installer and requires all Rhino windows to close before the staged installer can finish.

## Problems found and fixed during construction

- The initial regression attempted to reference a physical source cell that was already hidden and blank. Existing source graph validation correctly rejected that invalid parent target, so the fixture was corrected to use a valid surviving logical parent.
- Parallel standalone WPF test runs contended for the same intermediate plug-in DLL. The regressions were rerun sequentially and passed.
- A historical smoke fixture assigned a separate material to a physical cell that its own topology hid. The fixture was corrected to represent the supported logical persistence contract and now asserts that the hidden key is absent.

## Test record

Focused test:

```powershell
dotnet run --project .\Project_Test\260819_TEST_pcmatch-srf-logical-cell-cleanup\PCMatchSrfLogicalCellCleanupSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_pcmatch-srf-logical-cell-cleanup\PCMatchSrfLogicalCellCleanupSmoke.csproj -c Release
```

Result: both exit 0. Assertions verify deletion/non-recreation of `0B` and `1C`, retained surviving blanks, parent-cell fidelity, and preservation of target-owned attributes.

Regression suites executed in Debug:

- `260805_TEST_panel-cladding-match`
- `260818_TEST_panel-cladding-match-parent-cells`
- `260818_TEST_panel-cladding-match-topology-masks`
- `260818_TEST_panel-cladding-pc-commands`
- `260819_TEST_panel-cladding-blank-cell-persistence`
- `260819_TEST_panel-cladding-logical-cell-cleanup`
- `260819_TEST_panel-cladding-full-track-collapse`
- `260819_TEST_pcmatch-parent-fidelity`
- `260819_TEST_pcmatch-srf-logical-cells`
- `260819_TEST_pcmatch-srf-logical-cell-cleanup`

Result: all exit 0.

Solution validation:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug -m:1 --nologo
dotnet build .\MCP_Rhino.sln -c Release -m:1 --nologo
```

Result: both exit 0 with 0 warnings and 0 errors.

Package and identity validation:

```powershell
powershell -ExecutionPolicy Bypass -File .\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1 -Version 1.0.48
.\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Debug
.\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Release -SkipBuild -PluginPath <MCP-RHP>,<packaged-PanelCladdingEditor-RHP>
```

Result: package build exits 0. Debug and packaged Release PanelCladdingEditor RHPs declare plug-in id `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`; MCP_Rhino remains distinct.

Installer result:

```text
PanelCladdingEditor 1.0.48 was staged because Rhino is running.
C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.48-20260819141109662
```

## Acceptance alignment

- Hidden `0B`/`1C` target keys are deleted and not rewritten: passed.
- Surviving logical values and retained blanks are correct: passed.
- Parent-cell behavior remains correct: passed.
- Offset/mask equality remains outside compatibility: passed.
- Target-owned non-cell attributes are preserved: passed.
- Debug/Release builds, package creation, and RHP identity: passed.
- Current-user installation/registry validation: pending Rhino shutdown.

## Rollback verification

- Revert the logical-collapse write projection and package manifest from `1.0.48` to `1.0.47` to restore the prior behavior.
- No Rhino document was opened or modified by this construction/test execution.

## Current remaining items

- Close every Rhino window and run the staged installer.
- Validate the installed `1.0.48` current-user registry entry and RHP path.
- Reopen Rhino and rerun PCMatchSrf for the reported panel pair.

## Conclusion

The code, regression coverage, package, and plug-in identity are complete. Deployment is safely staged and awaits Rhino shutdown; until then, Rhino continues using the previously installed version.
