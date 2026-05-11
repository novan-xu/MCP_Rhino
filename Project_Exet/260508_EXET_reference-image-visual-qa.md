# Reference Image Visual QA EXET

## Corresponding Plan

- Plan: `Project_Plan/260508_PLAN_reference-image-visual-qa.md`
- Execution date: 2026-05-08

## Related Artifacts

- Test folder: `Project_Test/260508_TEST_reference-image-visual-qa/`
- Commit / PR: not created in this execution

## Execution Result / Actual Scope

Implemented the first visual QA slice for reference-image object modeling:

- Replaced the direct instance `ViewCapture.CaptureToBitmap(RhinoView)` path with settings-based `ViewCapture.CaptureToBitmap(ViewCaptureSettings)` in viewport capture.
- Applied the same runtime-compatible capture path to file image export to avoid the same RhinoCommon method mismatch.
- Added a read-only visual QA capture tool:
  - `CaptureReferenceImageModelingQaViews`
- Added `ReferenceImageVisualQaCaptureService`.
- Added request/response DTOs for visual QA capture.
- Added checkpoint enum:
  - `InitialMassing`
  - `DetailRefinement`
  - `MaterialReview`
  - `FinalAcceptance`
- Added target object resolution through explicit object ids or existing filter criteria.
- Added bounded view capture count and object summary count.
- Added checklist text for massing, detail, material, and final acceptance checkpoints.
- Added a dedicated CLI/Rhino smoke:
  - CLI slug: `reference-image-visual-qa-smoke-test`
  - Rhino command: `_McpReferenceImageVisualQaSmoke`
- Updated MCP tool safety expectations for the new read-only tool.
- Updated the Rhino reference note for viewport capture to describe the settings-based capture path.

## Deviations From Plan

- The plan allowed a possible object-scoped view manipulation/isolation mode. This execution intentionally did not add temporary viewport mutation or object isolation. The first slice stays read-only and captures the active or named viewport state.
- Semantic image comparison remains LLM-owned. The server returns captures, object summaries, and QA checklists only.
- Live smoke was not run during execution because the currently open Rhino process has the Debug plugin DLL locked. The Release plugin was rebuilt and is ready for manual live testing after reload.

## Issues Found And Fixed During Execution

- The original viewport capture implementation failed in live Rhino with a missing `CaptureToBitmap(RhinoView)` method. The code now uses `ViewCaptureSettings`, which is also the safer path for bounded capture settings.
- `ExportToImage` used the same incompatible instance capture call, so it was updated to the settings-based path too.
- An alternate build attempt initially placed generated intermediate files under the server project directory, causing duplicate assembly attributes. The generated validation folder was removed and validation was rerun with `OutputPath` only.

## Test Record

Attempted required Debug solution build:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug
```

Result:

- Exit code: 1
- Cause: `Rhino 8 (PID 51408)` locked `src\MCP_Rhino.Server\bin\Debug\net8.0\MCP_Rhino.Server.dll`.
- No compile error was reported before the copy failure.

Debug server validation with alternate output:

```powershell
dotnet build .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug -p:OutputPath=C:\01_Projects\MCP_Rhino\.validation\server-debug\
```

Result:

- Exit code: 0
- Warnings: 0
- Errors: 0
- Produced `MCP_Rhino.Server.rhp` in the alternate output.

Release solution build:

```powershell
dotnet build .\MCP_Rhino.sln -c Release
```

Result:

- Exit code: 0
- Warnings: 0
- Errors: 0
- Produced updated Release plugin at `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.rhp`.

Debug safety smoke from alternate output:

```powershell
.\.validation\server-debug\MCP_Rhino.Server.exe mcp-tool-safety-annotations-smoke-test
```

Result:

- Exit code: 0
- Output: `[OK] MCP safety annotations verified for 129 tools.`

Debug visual QA CLI fallback smoke from alternate output:

```powershell
.\.validation\server-debug\MCP_Rhino.Server.exe reference-image-visual-qa-smoke-test
```

Result:

- Exit code: 0
- Output: `[OK] reference-image-visual-qa CLI fallback returned LIVE_RHINO_REQUIRED for live-only capture.`

Release safety smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-tool-safety-annotations-smoke-test
```

Result:

- Exit code: 0
- Output: `[OK] MCP safety annotations verified for 129 tools.`

Release visual QA CLI fallback smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- reference-image-visual-qa-smoke-test
```

Result:

- Exit code: 0
- Output: `[OK] reference-image-visual-qa CLI fallback returned LIVE_RHINO_REQUIRED for live-only capture.`

Live smoke command prepared but not run in this execution:

```text
_McpReferenceImageVisualQaSmoke
```

Reason:

- The open Rhino instance is still running the previously loaded Debug plugin and locks the Debug output DLL. The new command requires loading the rebuilt plugin.

## Acceptance Criteria Alignment

- Existing viewport capture failure path was replaced with settings-based capture.
- Visual QA capture tool targets objects by ids or filters.
- Initial massing checkpoint is explicit.
- Tool returns capture payloads, object summaries, warning list, and QA checklist.
- Payloads are bounded by max view count, image size clamp inherited from viewport capture, and max object summaries.
- Tool safety annotations are explicit and smoke-verified.
- Debug compile validation passed through alternate output because the normal Debug output was locked.
- Release solution build passed.
- CLI fallback smoke confirms live-only behavior.
- Live smoke remains pending until plugin reload.

## Rollback Verification

Rollback would remove:

- `src/MCP_Rhino.Server/Tools/Viewport/CaptureReferenceImageModelingQaViewsTool.cs`
- `src/MCP_Rhino.Server/Application/Services/ReferenceImageVisualQaCaptureService.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/CaptureReferenceImageModelingQaViewsRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/ReferenceImageVisualQaCaptureResponse.cs`
- `src/MCP_Rhino.Server/Domain/Enums/ReferenceImageVisualQaCheckpointKind.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpReferenceImageVisualQaSmokeCommand.cs`
- `Project_Test/260508_TEST_reference-image-visual-qa/`

and revert:

- capture-path changes in `LiveRhinoViewportCapture.cs`
- capture-path changes in `LiveRhinoFileExporter.cs`
- DI and CLI handler registration additions
- safety smoke expectation addition
- Rhino reference note update

No destructive live document mutation was performed during this execution.

## Current Remaining Items

- Run `_McpReferenceImageVisualQaSmoke` after loading the rebuilt Release plugin.
- Re-run normal Debug solution build after closing or unloading the currently loaded Debug plugin.
- Future plan work can add temporary object isolation or object-scoped camera framing, but only with exact viewport state restoration.

## Conclusion

The first visual QA capability slice is implemented and compile/smoke validated outside the currently locked Debug plugin output. The Release plugin is built for live testing. The remaining validation step is live Rhino smoke after reloading the plugin.
