# 260429_EXET_drawing-export-workflow

## Corresponding Plan

- Plan file: `Project_Plan/260429_PLAN_drawing-export-workflow.md`
- Execution date: 2026-04-29

## Related Artifacts

- Test folder: `Project_Test/260429_TEST_drawing-export-workflow/`
- CLI smoke slug: `drawing-export-smoke-test`
- Rhino live smoke command: `_McpDrawingExportSmoke`
- commit hash / PR: not created in this session

## Execution Result / Actual Scope

Implemented the live-only drawing export workflow capability:

- Added atomic MCP tools:
  - `SetupDrawingViewsTool`
  - `CaptureDrawingExportStateTool`
  - `ApplyDrawingExportStyleTool`
  - `SetDrawingExportBackgroundTool`
  - `RestoreDrawingExportStateTool`
- Added workflow MCP tool:
  - `ExportDrawingPackageTool`
- Added `RhinoDrawingExportService` for orchestration.
- Added live adapters:
  - `LiveDrawingViewManager`
  - `LiveDrawingExportStateOperator`
- Added snapshot store:
  - `DrawingExportSnapshotStore`
- Added request/response DTOs for setup, state capture/style/background/restore, and package export.
- Added drawing export domain enums/models for Standard 8 views, response status, snapshots, layer scope, and setup results.
- Extended `LiveRhinoFileExporter` so existing PDF/JPG export can resolve `doc.NamedViews` by restoring a named view into the active viewport for capture, then restoring the original viewport.
- Added DI registrations for the new service/store/live adapters.
- Added test registration and Rhino smoke command:
  - `Project_Test/260429_TEST_drawing-export-workflow/DeveloperCommandHandler.DrawingExportSmokeTest.cs`
  - `src/MCP_Rhino.Server/Infrastructure/Plugin/McpDrawingExportSmokeCommand.cs`

## Deviation From Plan

- No separate `Skill` class was added. The workflow tool delegates to `RhinoDrawingExportService`, which owns the fixed workflow. This keeps the public MCP surface thin while avoiding an extra pass-through abstraction.
- `CaptureDrawingExportState` also returns `NeedsLayerSelection` with candidates, matching the final plan-review decision even though the first draft only specified that shape for view setup/package export.
- The workflow calls `ILiveFileExporter` directly inside one live document scope instead of calling `RhinoFileExportService`; this preserves the single `try/finally` state-restore flow while reusing the same live export adapter.

## Issues Found and Fixed During Execution

- `RhinoViewport` does not expose `SetCameraUp`; setup now sets camera location/direction, switches to parallel projection, and zooms the bounding box.
- Initial staged style/background/restore calls checked snapshot ids before entering the live accessor. This was adjusted so all atomic drawing-export operations prove live Rhino availability first.
- Running build and smoke in parallel caused a transient DLL lock in `obj/Debug/net8.0`; rerunning the build alone passed.

## Test Record

### Build

Command:

```powershell
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj --nologo
```

Result:

- Exit code: `0`
- `Build succeeded.`
- Warning: `0`
- Error: `0`

### CLI fallback smoke

Command:

```powershell
dotnet run --project src\MCP_Rhino.Server -- drawing-export-smoke-test Runtime_Test\MCP_rhino_test.3dm
```

Result:

- Exit code: `0`
- Output: `Drawing export smoke test completed successfully (CLI fallback mode).`

Covered assertions:

- `SetupDrawingViews` rejects without live Rhino.
- `CaptureDrawingExportState` rejects without live Rhino.
- `ApplyDrawingExportStyle` rejects without live Rhino.
- `SetDrawingExportBackground` rejects without live Rhino.
- `RestoreDrawingExportState` rejects without live Rhino.
- `ExportDrawingPackage` rejects without live Rhino.

### Live smoke

Command entry added:

```text
_McpDrawingExportSmoke
```

This session did not run the Rhino live smoke because the current shell is not inside the Rhino plugin host.

The live smoke code covers:

- `NeedsLayerSelection` response with candidate layers.
- Standard 8 named view creation/update.
- Package export for one named view to PDF and JPG.
- Deterministic file creation under `_validation/drawing-export-smoke-test/`.
- Exact object color/background restoration after workflow export.
- Missing snapshot rejection in live mode.

## Acceptance Criteria Alignment

Met:

- Plan artifact exists.
- Both atomic tools and workflow tool were added.
- Missing layer selection is modeled as `Success=true` with status `NeedsLayerSelection` and candidate layers.
- Standard 8 named view setup was implemented.
- View setup uses visible, non-deleted selected-layer objects with the default 10 percent margin.
- PDF/JPG export can capture generated named views via the new named-view bridge.
- Object attributes and background are snapshotted/restored by the workflow.
- Atomic snapshot restore rejects missing, stale, or document-mismatched snapshots.
- CLI fallback smoke and build passed.

Pending live verification:

- Actual Rhino viewport/named-view camera framing and PDF/JPG output in plugin host.
- Exact restoration checks in live Rhino after real export.

## Rollback Verification

To roll back this capability, remove the new drawing tools/contracts/domain models/interfaces/service/live adapters, remove DI/CLI hook additions, remove `McpDrawingExportSmokeCommand`, and remove `Project_Test/260429_TEST_drawing-export-workflow/`.

No destructive rollback was performed in this session.

## Current Remaining Items

- Run `_McpDrawingExportSmoke` in Rhino with a saved active document.
- If Rhino output framing needs adjustment, tune the Standard 8 camera directions/framing margin and record the deviation here.

## Conclusion

The drawing export workflow implementation is compiled and CLI-smoke verified. The only remaining validation is the Rhino plugin-host live smoke.
