# Panel Cladding Curve Topology Execution Report

## Corresponding plan

- Plan: `Project_Plan/260819_PLAN_panel-cladding-curve-topology.md`
- Execution date: 2026-08-19

## Related artifacts

- Tests: `Project_Test/260819_TEST_panel-cladding-curve-topology/`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.49/`
- Staged installation: `C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.49-20260819144707589`
- Commit / PR: none created

## Execution result / actual scope

### PCCreate

- Added a meaningful Brep-overlap test to every panel/guide snapshot. A guide must have a non-zero clipped interval and pass three actual closest-point probes against the selected Brep; endpoint-only contact is rejected.
- Replaced bounding-overlap grid creation with atomic span inference.
- Candidate H/V coordinates are clustered, clipped to panel extents, and subdivided by perpendicular candidate tracks.
- An atomic divider is present only when the union of on-panel guide spans completely covers its boundary-to-boundary or track-to-track bay.
- Tracks with no complete atoms are removed and the topology is recomputed until stable, preventing invalid tracks from making other dangling segments appear valid.
- Segment masks now encode every absent atomic divider.
- Merge runs require one continuous selected guide span to cover adjacent present atoms. Separate collinear curves can jointly establish segment presence without inventing one merged curve.
- PCCreate now writes canonical segment and merge payloads.
- Blank physical cells are collapsed through the inferred topology, so only surviving logical blank keys are persisted.
- Stale offsets, cell keys, masks, and signature/type data remain within the existing reset transaction; unrelated panel metadata is preserved.

### PCMatchCrv

- Added `PanelCladdingCurveMatchPlanningService` and public Rhino command `PCMatchCrv` with explicit unique command GUID `4A0E749F-021B-41E5-9A43-D0D5A0F043A7`.
- Added a small planning interface so PCMatchSrf and PCMatchCrv share the existing live read/prepare/Undo/rollback transaction without sharing transfer semantics.
- PCMatchCrv accepts panels with equal H/V track counts even when dimensions and numeric offset values differ.
- It validates and canonicalizes the source topology, validates target payload dimensions, and writes only `CW_2.05_SEGMENT_MASK` and `CW_2.06_MERGE_MASK`.
- Target offsets, cladding cell assignments, unit/type/signature metadata, identity, and unrelated user text are preserved.
- One incompatible target rejects the plan before live mutation.
- The public PanelCladdingEditor command inventory now contains nine PC-prefixed commands.

## Deviations from plan

- No behavioral deviation from the approved plan.
- Installation is not complete because Rhino remained running. The installer safely staged version `1.0.49` for completion after all Rhino processes close.

## Problems found and fixed during construction

- The historical full-grid PCCreate smoke expected an empty merge mask. Because its selected H and V guides are each continuous across both atomic bays, the corrected behavior is one H merge run and one V merge run; the fixture expectation was updated.
- The focused test initially lacked a direct RhinoCommon reference and explicit `System.IO` import for command/source-contract assertions. Both test-host dependencies were added.
- Concurrent build-output contention was avoided by running all WPF plug-in smoke projects sequentially.

## Test record

Focused Debug and Release smoke:

```powershell
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-curve-topology\PanelCladdingCurveTopologySmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-curve-topology\PanelCladdingCurveTopologySmoke.csproj -c Release
```

Result: both exit 0.

Key fixture output:

- Screenshot arrangement: H offsets `60,100,150`; V offset `50`.
- Missing segments: horizontal track 0/right bay, horizontal track 1/left bay, horizontal track 2/left bay.
- Merge runs: vertical track 0, bays 0 through 3.
- Surviving logical blank cells: 5.
- Endpoint-only and cascading dangling guide arrangements: no applicable layout and no plan.
- PCMatchCrv with equal `2H/1V` counts but different dimensions/offsets: accepted; exactly two masks transferred.
- PCMatchCrv count mismatch and invalid source mask: rejected.

Sequential Debug regressions:

- `260818_TEST_panel-cladding-create-command`
- `260818_TEST_panel-cladding-pc-commands`
- `260805_TEST_panel-cladding-match`
- `260818_TEST_panel-cladding-match-parent-cells`
- `260818_TEST_panel-cladding-match-topology-masks`
- `260819_TEST_pcmatch-parent-fidelity`
- `260819_TEST_pcmatch-srf-logical-cells`
- `260819_TEST_pcmatch-srf-logical-cell-cleanup`
- `260819_TEST_panel-cladding-blank-cell-persistence`
- `260819_TEST_panel-cladding-logical-cell-cleanup`
- `260819_TEST_panel-cladding-full-track-collapse`

Result: all exit 0.

Solution validation:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug -m:1 --nologo
dotnet build .\MCP_Rhino.sln -c Release -m:1 --nologo
```

Result: both exit 0 with 0 warnings and 0 errors.

Package and identity validation:

```powershell
powershell -ExecutionPolicy Bypass -File .\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1 -Version 1.0.49
.\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Debug
.\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Release -SkipBuild -PluginPath <MCP-RHP>,<packaged-PanelCladdingEditor-RHP>
```

Result: package build exits 0. Debug and packaged Release PanelCladdingEditor RHPs declare plug-in id `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`; MCP_Rhino remains distinct.

Installer result:

```text
PanelCladdingEditor 1.0.49 was staged because Rhino is running.
C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.49-20260819144707589
```

## Acceptance alignment

- Meaningful on-surface guide association, not endpoint contact: passed by planner/live-source contract and dangling fixtures.
- Closed atomic shape inference matching the screenshot: passed.
- Offset, segment mask, merge mask, and logical blank persistence: passed.
- Continuous-versus-separate curve merge behavior: passed.
- Cascading invalid track removal: passed.
- PCMatchCrv equal-count/different-value compatibility: passed.
- PCMatchCrv two-mask-only transfer and non-mask preservation: passed.
- Incompatible counts and invalid masks fail before mutation: passed.
- Nine public PC-prefixed commands with unique GUIDs: passed.
- Debug/Release builds, package, and RHP identity: passed.
- Current-user install/registry validation: pending Rhino shutdown.

## Rollback verification

- Restore the prior PCCreate planner and snapshot association behavior, remove `PCMatchCrv` and its planner interface, revert the manifest to the prior package version, and reinstall the prior artifact.
- No Rhino document was opened or modified by automated tests in this execution.

## Current remaining items

- Close every Rhino window and run the staged `1.0.49` installer.
- Validate the installed RHP and HKCU Rhino registration.
- Reopen Rhino and live-check PCCreate with the supplied curve arrangement plus PCMatchCrv on representative panels.

## Conclusion

PCCreate now infers curve topology from meaningful on-surface, closed atomic coverage instead of incidental contact, and PCMatchCrv provides the requested mask-only matching behavior. Code, tests, package, and identity verification are complete; deployment is staged pending Rhino shutdown.
