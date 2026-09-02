# PCMatchSrf Repair Existing Targets Execution Report

## Corresponding plan

- Plan: `Project_Plan/260819_PLAN_pcmatch-srf-repair-existing-targets.md`
- Execution date: 2026-08-19

## Related artifacts

- Tests: `Project_Test/260819_TEST_pcmatch-srf-repair-existing-targets/`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.53/`
- Staged installer: `C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.53-20260819154128569\Installer\Install-PanelCladdingEditor.ps1`
- Commit / PR: none created

## Execution result / actual scope

- Removed the planner rejection that prevented `PCMatchSrf` from targeting a panel with existing cladding-cell values. A rerun can now repair a partially matched target that has owner values but is missing a parent reference such as `1A=0A`.
- Match planning still checks source/target logical cell-code compatibility before mutation.
- The target's cladding-cell set is replaced by the source projection. Stale target-only cell keys are removed, including formerly merged cells.
- Target offsets, segment/merge masks, type/signature, PID/CID, and unrelated user text remain target-owned and unchanged.
- Added a pure postcondition validator that checks every planned write for an exact value and every planned deletion for absence.
- The Rhino adapter now reads committed attributes after each modification. A mismatch restores the current and all previously modified targets before returning failure.
- Package version advanced from `1.0.52` to `1.0.53`.

## Deviations from plan

- No production or test-scope deviation.
- Rhino remained open with `1.0.52`, so the installer correctly staged `1.0.53` rather than replacing the loaded plug-in.

## Problems found and fixed during construction

- The explicit-parent projection fix in `1.0.52` was correct, but `PCMatchSrf` rejected a populated target before planning. A target left partially configured by an earlier run therefore could not be repaired by rerunning the command.
- A successful Rhino `ModifyAttributes` result was not sufficient proof that all parent writes and stale-key deletions were committed. Exact read-back verification now guards the success result.

## Test record

Focused Debug and Release smoke:

```powershell
dotnet run --project .\Project_Test\260819_TEST_pcmatch-srf-repair-existing-targets\PCMatchSrfRepairExistingTargetsSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_pcmatch-srf-repair-existing-targets\PCMatchSrfRepairExistingTargetsSmoke.csproj -c Release
```

Result: both exit 0.

Focused assertions:

- an already populated target is accepted and receives `1A=0A`;
- a stale target-only merged cell key is deleted;
- non-cell target attributes remain unchanged;
- validation rejects a missing parent write;
- validation rejects a retained planned deletion.

Sequential Debug regressions:

- `260805_TEST_panel-cladding-match`
- `260819_TEST_pcmatch-srf-explicit-parent-cells`
- `260819_TEST_pcmatch-srf-logical-cells`
- `260819_TEST_pcmatch-srf-logical-cell-cleanup`
- `260819_TEST_pcmatch-parent-fidelity`
- `260818_TEST_panel-cladding-match-parent-cells`
- `260818_TEST_panel-cladding-match-topology-masks`

Result: all exit 0.

Solution validation:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug --no-restore
dotnet build .\MCP_Rhino.sln -c Release --no-restore
```

Result: both exit 0 with 0 warnings and 0 errors.

Package and identity validation:

```powershell
.\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1 -Version 1.0.53
.\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Release -PluginPath <MCP-RHP>,<packaged-PanelCladdingEditor-RHP>
```

Result: package build and identity validation exit 0. PanelCladdingEditor declares manifest-matched plug-in id `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`; MCP_Rhino remains distinct. Packaged RHP SHA-256: `FEC5C449F093A2878B02F8DF4E1AADAA774DD3DCC759B32CC83870D504C2E910`.

Installer result:

```text
PanelCladdingEditor 1.0.53 was staged because Rhino is running.
C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.53-20260819154128569
```

## Acceptance alignment

- Repair missing `1A=0A` on a populated compatible target: passed.
- Remove stale target-only cladding-cell keys: passed.
- Preserve all non-cell target attributes: passed.
- Exact post-commit verification and batch rollback path: implemented and validator-covered.
- Existing logical-cell, explicit-parent, parent-fidelity, and topology regressions: passed.
- Debug/Release builds, package, and plug-in identity: passed.
- Current-user activation and live command rerun: pending Rhino close and staged installation.

## Rollback verification

- Restore the populated-target guard, remove post-commit verification, and reinstall package `1.0.52`.
- No Rhino document object was mutated during this construction turn.

## Current remaining items

- Close all Rhino windows.
- Run the staged `1.0.53` installer.
- Reopen the BKT model and rerun `PCMatchSrf` from `PID_BKT_N1_01_07` to `PID_BKT_N1_01_05` or another compatible target, then confirm `1A=0A` in object user text.

## Conclusion

`PCMatchSrf` can now repair an already populated target, preserves target-owned layout metadata, and cannot report success when a planned parent value or deletion is missing after the Rhino attribute commit. Package `1.0.53` is staged and ready to activate after Rhino closes.
