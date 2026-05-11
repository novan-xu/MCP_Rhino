# Document Visual State Tools EXET

## Corresponding Plan

- Plan: `Project_Plan/260507_PLAN_document-visual-state-tools.md`
- Execution date: 2026-05-08

## Associated Artifacts

- Test folder: `Project_Test/260507_TEST_document-visual-state-tools/`
- Commit / PR: none in this workspace execution

## Execution Result / Actual Scope

- Added six live-only MCP tools:
  - `GetDocumentSummary` under `Tools/Analysis`
  - `GetSelectedObjectsInLive` under `Tools/Selection`
  - `SelectObjectsInLive` under `Tools/Selection`
  - `GetCurrentLayerInLive` under `Tools/Layers`
  - `SetCurrentLayerInLive` under `Tools/Layers`
  - `CaptureViewportImage` under `Tools/Viewport`
- Added request/response DTOs for document summary, selected objects, selection mutation, current layer, current-layer mutation, and base64 PNG viewport capture.
- Added `RhinoDocumentStateService`, `RhinoSelectionService`, and `RhinoViewportCaptureService`.
- Added live Rhino adapters:
  - `LiveRhinoDocumentStateOperator`
  - `LiveRhinoSelectionOperator`
  - `LiveRhinoViewportCapture`
  - shared `LiveRhinoObjectInfoMapper`
- Registered the new services and live adapters in `DependencyInjection`.
- Updated `DeveloperCommandHandler` with the plan-specific smoke hook.
- Updated MCP tool safety expectations for the six new tools.
- Added Rhino command `_McpDocumentVisualStateToolsSmoke` through `McpDocumentVisualStateToolsSmokeCommand`.

## Deviation From Plan

- `CaptureViewportImage` returns structured base64 PNG data instead of MCP image content. The .NET SDK support path for direct image content was not used in this execution; base64 keeps the tool client-compatible and avoids file output.
- Saved named views are not restored for viewport capture in this first version because restoring a named view mutates viewport state. The tool captures the active viewport or an already-open viewport by name.
- The live Rhino smoke command was added but not executed in this CLI-only session because it requires Rhino with the plugin loaded and a saved active document.

## Issues Found And Fixed During Execution

- The first Debug build failed on CA1416 platform analysis for `System.Drawing` PNG serialization. The bitmap encoding path was moved behind a Windows-only guarded method, and the build then passed cleanly.
- The document visual state smoke initially could not be run before a successful rebuild because the CLI command was not yet in the compiled assembly. It passed after rebuilding.

## Test Record

Command:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug
```

Result:

- Exit code: 0
- Output summary: `Build succeeded. 0 Warning(s), 0 Error(s).`

Command:

```powershell
dotnet build .\MCP_Rhino.sln -c Release
```

Result:

- Exit code: 0
- Output summary: `Build succeeded. 0 Warning(s), 0 Error(s).`

Command:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- document-visual-state-tools-smoke-test
```

Result:

- Exit code: 0
- Output summary: `[OK] document-visual-state-tools CLI fallback returned LIVE_RHINO_REQUIRED for live-only paths.`

Command:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- document-visual-state-tools-smoke-test
```

Result:

- Exit code: 0
- Output summary: `[OK] document-visual-state-tools CLI fallback returned LIVE_RHINO_REQUIRED for live-only paths.`

Command:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-tool-safety-annotations-smoke-test
```

Result:

- Exit code: 0
- Output summary: `[OK] MCP safety annotations verified for 112 tools.`
- Output summary: `[OK] No bare method-level [McpServerTool] attributes remain.`

Command:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-tool-safety-annotations-smoke-test
```

Result:

- Exit code: 0
- Output summary: `[OK] MCP safety annotations verified for 112 tools.`
- Output summary: `[OK] No bare method-level [McpServerTool] attributes remain.`

Command:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-surface-structure-governance-smoke-test
```

Result:

- Exit code: 0
- Key output: `[OK] MCP tool inventory discovered 112 tools.`
- Family counts include `Selection: 2` and `Viewport: 1`.

Command:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-surface-structure-governance-smoke-test
```

Result:

- Exit code: 0
- Key output: `[OK] MCP tool inventory discovered 112 tools.`
- Family counts matched Debug.

Live Rhino smoke:

```text
_McpDocumentVisualStateToolsSmoke
```

Result:

- Not executed in this CLI-only session.
- The command is implemented and routes to `document-visual-state-tools-smoke-test` with the saved active document path.

## Acceptance Criteria Alignment

- All new tools use `ILiveRhinoDocumentAccessor` through live adapters.
- No tool falls back to disk `.3dm` reads.
- Selection and current-layer mutations are separate tools from read tools.
- Viewport capture returns base64 PNG data and does not write files or accept output paths.
- Document summaries are bounded and emit truncation warnings when caps are exceeded.
- Safety annotations are explicit and included in the safety smoke expectation map.
- Debug and Release solution builds passed.
- Debug and Release MCP tool safety smokes passed.
- CLI fallback smoke verifies all new live-only paths return `LIVE_RHINO_REQUIRED` outside Rhino.
- Live smoke is implemented but still requires manual execution inside Rhino.

## Rollback Verification

Rollback path:

- Remove the six tool files under `Tools/Analysis`, `Tools/Selection`, `Tools/Layers`, and `Tools/Viewport`.
- Remove document visual state request/response DTOs and `RhinoSelectionMode`.
- Remove `RhinoDocumentStateService`, `RhinoSelectionService`, `RhinoViewportCaptureService`.
- Remove `ILiveRhinoDocumentStateOperator`, `ILiveRhinoSelectionOperator`, `ILiveRhinoViewportCapture`.
- Remove `LiveRhinoDocumentStateOperator`, `LiveRhinoSelectionOperator`, `LiveRhinoViewportCapture`, and `LiveRhinoObjectInfoMapper`.
- Remove DI registrations and `DeveloperCommandHandler` smoke hook additions.
- Remove safety smoke expectation entries for the six tool names.
- Remove `Project_Test/260507_TEST_document-visual-state-tools/` and `McpDocumentVisualStateToolsSmokeCommand`.

Existing analysis, layer, export, drawing, and geometry tools remain independent of this rollback.

## Current Remaining Items

- Execute `_McpDocumentVisualStateToolsSmoke` inside Rhino with a saved active document to validate the full live path.
- Consider direct MCP image content if the client/SDK path proves reliable across the target clients.
- Consider saved named-view capture in a later mutating/restoring viewport plan if needed.

## Conclusion

The document visual state tool surface is implemented and validated at build, registration, metadata, safety, and CLI fallback levels. Full live Rhino validation is available through `_McpDocumentVisualStateToolsSmoke` and remains pending in this session.
