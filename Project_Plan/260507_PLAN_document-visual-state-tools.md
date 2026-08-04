# Document Visual State Tools Plan

## Background

The `rhinomcp` reference project exposes several tools that are small but useful for runtime orientation:

- `get_document_summary`
- `get_selected_objects_info`
- `select_objects`
- `get_or_set_current_layer`
- `capture_viewport`

MCP_Rhino already has richer analysis, filtering, layer management, drawing export, and file export tools, but it does not expose these document/session state capabilities as direct live tools. The current workflow can often compose similar information through multiple calls, but a model benefits from a low-cost first call that summarizes the active Rhino document and from direct tools for UI selection/current-layer/viewport feedback.

## Goal

Add live-only document and visual state tools that improve runtime orientation without adding arbitrary code execution.

This plan covers:

- live document summary
- read current Rhino selection
- set Rhino selection from resolved ids or filters
- read current layer
- set current layer
- capture active or named viewport as in-band image data or a structured base64 payload, depending on .NET MCP SDK support

## Architecture Ownership

- `Tools/Analysis`: read-only document summary.
- `Tools/Selection`: read current Rhino UI selection and mutate selection state.
- `Tools/Layers`: get/set current layer.
- `Tools/Viewport`: in-band viewport capture. File-based image export remains under `Tools/File/Export`.
- `Application/Services`: document state and viewport services.
- `Application/Interfaces`: live document state and viewport capture abstractions if needed.
- `Infrastructure/Rhino/Live`: RhinoCommon implementation for selection, current layer, and viewport capture.
- `Contracts/Requests` and `Contracts/Responses`: request/response DTOs.

## Key Design

### 1. GetDocumentSummaryTool

Add a read-only tool that returns:

- active document path
- document name
- units
- absolute and angle tolerance
- object count
- layer count
- object counts by normalized type
- object counts by layer
- current layer
- named views count and names, capped
- materials count, capped summary
- selected object count
- optional first N object summaries

The response must be bounded by default to avoid large MCP payloads.

Safety:

- `ReadOnly = true`
- `Destructive = false`
- `OpenWorld = false`

### 2. GetSelectedObjectsInLiveTool

Add a read-only tool under `Tools/Selection` that returns selected live object ids and metadata using existing object-info summary conventions.

Safety:

- `ReadOnly = true`
- `Destructive = false`
- `OpenWorld = false`

### 3. SelectObjectsInLiveTool

Add a tool under `Tools/Selection` that sets Rhino UI selection. This changes Rhino UI state but not document geometry. Avoid a mixed get/set tool because mixed safety is unclear.

Inputs:

- `filePath`
- explicit object ids
- optional filter criteria using the existing `FilterObjectsRequest` shape
- `selectionMode`: replace, add, remove, clear

Safety:

- `ReadOnly = false`
- `Destructive = false`
- `OpenWorld = false`

### 4. GetCurrentLayerInLiveTool

Read the current layer as a separate read-only tool.

Safety:

- `ReadOnly = true`
- `Destructive = false`
- `OpenWorld = false`

### 5. SetCurrentLayerInLiveTool

Set the current layer by id, full path, or confirmed candidate. Reuse layer candidate resolution to avoid accidental ambiguous matches.

Safety:

- `ReadOnly = false`
- `Destructive = false`
- `OpenWorld = false`

### 6. CaptureViewportImageTool

Add a read-only viewport capture tool for visual verification. First execution scope is strictly current-state capture:

- capture active, perspective, top, front, right, or named viewport
- optional width/height bounded to a safe max
- no zoom-to-fit and no projection changes in the first version
- return MCP image content if supported by the .NET SDK
- otherwise return structured base64 PNG data with metadata

Do not write output files. Existing `ExportToImage` already covers file output.

Safety:

- `ReadOnly = true`
- `Destructive = false`
- `OpenWorld = false`

Zoom-to-fit or temporary projection changes must be deferred to a separate mutating viewport plan unless a later implementation can prove exact state restoration.

## Involved Files

Likely production files:

- `src/MCP_Rhino.Server/Tools/Analysis/GetDocumentSummaryTool.cs`
- `src/MCP_Rhino.Server/Tools/Selection/GetSelectedObjectsInLiveTool.cs`
- `src/MCP_Rhino.Server/Tools/Selection/SelectObjectsInLiveTool.cs`
- `src/MCP_Rhino.Server/Tools/Layers/GetCurrentLayerInLiveTool.cs`
- `src/MCP_Rhino.Server/Tools/Layers/SetCurrentLayerInLiveTool.cs`
- `src/MCP_Rhino.Server/Tools/Viewport/CaptureViewportImageTool.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoDocumentStateService.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoSelectionService.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoViewportCaptureService.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoDocumentStateReader.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoSelectionOperator.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoViewportCapture.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/*`
- `src/MCP_Rhino.Server/Contracts/Responses/*`
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`

Required test artifacts if executed:

- `Project_Test/260507_TEST_document-visual-state-tools/`

Required safety update:

- `Project_Test/260506_TEST_mcp-tool-safety-annotations/DeveloperCommandHandler.McpToolSafetyAnnotationsSmokeTest.cs`

## Usage

Expected runtime flow:

1. Call `GetDocumentSummary` at the start of open-ended modeling or inspection tasks.
2. Use `GetSelectedObjectsInLive` when the user says "the selected objects".
3. Use `SelectObjectsInLive` only when the user asks to select/highlight objects or when selection is needed before a Rhino UI workflow.
4. Use `GetCurrentLayerInLive` and `SetCurrentLayerInLive` instead of overloading layer creation/modification tools.
5. Use `CaptureViewportImage` after geometry changes to visually verify results.

## Acceptance Criteria

- All tools are live-only and go through `ILiveRhinoDocumentAccessor`.
- No tool falls back to disk `.3dm` reads.
- Selection and current-layer mutations use one clear operation per tool.
- Viewport capture does not write files and does not require caller-provided output paths.
- Large document summaries are bounded and include truncation warnings when capped.
- Safety annotations are explicit and included in the safety smoke expectation map.
- `dotnet build .\MCP_Rhino.sln -c Debug` passes.
- `dotnet build .\MCP_Rhino.sln -c Release` passes.
- `dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-tool-safety-annotations-smoke-test` passes.
- `dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-tool-safety-annotations-smoke-test` passes.
- Live smoke demonstrates document summary, selection read/set, current layer get/set, and viewport capture against a saved active Rhino document.

## Risks And Rollback

- Risk: UI selection mutation may surprise users.
  - Mitigation: make selection tools explicit and avoid changing selection in read tools.
- Risk: viewport capture may accidentally alter view state.
  - Mitigation: first version forbids zoom-to-fit/projection changes and captures current viewport state only.
- Risk: returned image payloads may exceed MCP client limits.
  - Mitigation: enforce width/height caps and document defaults.

Rollback removes the new tools, services, contracts, DI registrations, and safety smoke entries. Existing analysis, layer, and export tools remain unaffected.

## Future Extension

- Add multi-view capture workflow as a Skill after repeated runtime usage justifies it.
- Add visual comparison or before/after snapshot support if needed.
- Add panel UI affordances for selected objects and captured viewport thumbnails.
