# Reference Image Visual QA Plan

## Background

The existing `CaptureViewportImageTool` is intended to provide in-band visual feedback, but the sofa audit found that it currently fails in the live Rhino environment with:

`Method not found: System.Drawing.Bitmap Rhino.Display.ViewCapture.CaptureToBitmap(Rhino.Display.RhinoView)`

Without a reliable way to see what Rhino actually built, an LLM cannot perform meaningful image-to-model iteration. Visual QA is especially important immediately after initial massing. If the rough silhouette and part layout are wrong, later detail and material passes will not fix the model.

This plan isolates visual QA from the broader object modeling tool plan because it is a critical dependency for agent reliability.

## Goal

Add a robust visual QA capability for reference-image object modeling.

The capability should:

- repair or replace the current viewport capture path
- capture object-scoped review views after initial massing and after refinement
- package reference image context, live viewport captures, object metadata, and QA prompts into a bounded response
- allow the LLM agent to compare the reference image and generated model at multiple checkpoints
- preserve or restore Rhino viewport state when temporary view changes are needed

Non-goals:

- Embedding an LLM/computer-vision model inside the Rhino server
- Performing final semantic image comparison entirely in C#
- Implementing the object modeling agent
- Adding material or geometry tools unrelated to capture/QA packaging

## Architecture Ownership

- `Tools/Viewport`
  - Repair `CaptureViewportImageTool`.
  - Add an object-scoped visual QA capture tool if needed.

- `Application/Services`
  - Visual QA capture orchestration and response packaging.

- `Application/Interfaces`
  - Live viewport capture and temporary view-state interfaces.

- `Infrastructure/Rhino/Live`
  - RhinoCommon viewport capture implementation and state restoration.

- `Contracts/Requests` and `Contracts/Responses`
  - DTOs for capture settings, target object filters, reference image info, QA checkpoints, and returned images.

- `Prompts/Modeling`
  - QA prompt templates for massing, detail/material, and final acceptance.

This capability reads live Rhino viewport state and may optionally write temporary viewport state only if exact restoration is implemented. File output should remain separate from in-band QA unless explicitly requested.

## Key Design

### 1. Fix current capture first

Before adding higher-level QA, repair the current capture failure.

Implementation should verify the installed RhinoCommon API and choose a compatible path, for example:

- current `ViewCapture` API if the method signature has changed
- `ViewCaptureSettings` where appropriate
- Rhino view capture alternatives available in Rhino 8

Acceptance requires live proof that a saved active Rhino document can return a non-empty PNG payload.

### 2. In-band capture remains read-oriented

The basic capture tool should:

- return base64 PNG or MCP image content
- not write files
- not mutate geometry
- not change selection
- avoid changing viewport projection or camera

Safety:

- `ReadOnly = true`
- `Destructive = false`
- `OpenWorld = false`

### 3. Object-scoped QA capture

Reference-image modeling needs capture of created objects, not just whatever viewport the user happened to have active.

Candidate tool:

- `CaptureReferenceImageModelingQaViewsTool`

Inputs:

- `filePath`
- target object ids or filter criteria
- reference image label/path supplied by user
- checkpoint kind: `InitialMassing`, `DetailRefinement`, `MaterialReview`, `FinalAcceptance`
- requested views: front, side, top, perspective, current
- image size bounds
- optional transparent background
- optional temporary isolate/hide behavior only if state restoration is robust

Outputs:

- captured images
- view names and camera metadata
- target object summary
- object counts by layer/type
- bounding box if available
- reference image info
- QA checklist prompt for the LLM agent
- warnings when views cannot be captured or state cannot be safely changed

### 4. Massing QA checklist

The visual QA response should include a structured checklist for the LLM agent. The server does not judge semantic similarity itself, but it can return the exact checklist the agent should apply.

Initial massing checklist:

- Does the model read as the same object category?
- Is the large silhouette close?
- Are major parts present and in roughly correct positions?
- Are width/height/depth proportions plausible?
- Are obvious negative spaces or openings accounted for?
- Is there detail noise that should be delayed or removed?

Detail/material checklist:

- Did added details improve recognizability?
- Are seams, grooves, handles, feet, rims, labels, or repeated features placed correctly enough?
- Are any details represented as geometry when material/texture would be better?
- Are colors/materials close enough to the reference?

Final acceptance checklist:

- Is the object recognizable from the captured view?
- What are the largest remaining mismatches?
- Are mismatches due to missing tools, ambiguous image context, or poor modeling choices?
- Should the agent iterate, accept, or stop with a gap report?

### 5. State restoration discipline

If the QA tool changes view, display mode, selection, hidden state, clipping, or isolation state, it must restore state before returning.

Preferred first version:

- capture current active/named views without temporary isolation
- support named view restoration only if current code can prove exact restore
- avoid hiding unrelated objects unless a robust snapshot/restore mechanism is implemented

Any state-changing capture mode must be marked:

- `ReadOnly = false`
- `Destructive = false`
- `OpenWorld = false`

If a purely offscreen or non-mutating capture path is available, keep it read-only.

### 6. Bounded payloads

Visual QA payloads can become large. Enforce:

- max width/height
- max number of views
- max object metadata count
- optional low/medium/high capture quality presets
- warnings on truncation

## Involved Files

Likely production files when executed:

- `src/MCP_Rhino.Server/Tools/Viewport/CaptureViewportImageTool.cs`
- `src/MCP_Rhino.Server/Tools/Viewport/CaptureReferenceImageModelingQaViewsTool.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoViewportCaptureService.cs`
- `src/MCP_Rhino.Server/Application/Services/ReferenceImageVisualQaCaptureService.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveRhinoViewportCapture.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveRhinoViewStateOperator.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoViewportCapture.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoViewStateOperator.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/CaptureReferenceImageModelingQaViewsRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/ReferenceImageVisualQaCaptureResponse.cs`
- `src/MCP_Rhino.Server/Domain/Enums/ReferenceImageVisualQaEnums.cs`
- `src/MCP_Rhino.Server/Domain/Models/ReferenceImageVisualQaChecklist.cs`
- `src/MCP_Rhino.Server/Prompts/Modeling/ReferenceImageVisualQaMassing.md`
- `src/MCP_Rhino.Server/Prompts/Modeling/ReferenceImageVisualQaDetail.md`
- `src/MCP_Rhino.Server/Prompts/Modeling/ReferenceImageVisualQaFinal.md`
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`

Required test artifacts if executed:

- `Project_Test/260508_TEST_reference-image-visual-qa/`

Required safety update:

- `Project_Test/260506_TEST_mcp-tool-safety-annotations/DeveloperCommandHandler.McpToolSafetyAnnotationsSmokeTest.cs`

## Usage

Expected agent use:

1. Build initial massing.
2. Call visual QA capture for initial massing.
3. LLM compares captured model views against the reference image using the returned checklist.
4. Agent revises massing or proceeds to details.
5. Repeat visual QA after details/materials.
6. Run final acceptance QA before reporting completion.

Direct runtime use:

- User asks, "show me what you built" or "compare this model against the reference image."
- The tool returns review captures and a checklist for the LLM response.

## Acceptance Criteria

- Existing viewport capture failure is fixed or replaced.
- Live capture returns a valid non-empty PNG/base64 payload from a saved active Rhino document.
- QA capture can target generated model objects by ids or filters.
- QA capture supports at least current view and one object-scoped perspective or named view path.
- Initial massing QA is explicitly supported as a checkpoint kind.
- Payloads are bounded and include warnings on truncation.
- Any temporary view/state changes are restored, or the mode is not shipped.
- Safety annotations match actual state behavior.
- `dotnet build .\MCP_Rhino.sln -c Debug` passes.
- `dotnet build .\MCP_Rhino.sln -c Release` passes.
- Live smoke creates simple objects, captures them, validates non-empty image bytes, and verifies state restoration or read-only behavior.

## Risks And Rollback

- Risk: RhinoCommon viewport capture APIs differ from assumptions.
  - Mitigation: inspect installed RhinoCommon API locally and test live before building higher-level QA.
- Risk: visual payloads are too large for MCP clients.
  - Mitigation: strict image size/view count caps.
- Risk: temporary viewport manipulation leaves user state changed.
  - Mitigation: first ship non-mutating capture; only add state-changing modes with smoke-proven restoration.
- Risk: users expect the server to perform semantic visual comparison.
  - Mitigation: tool returns images and checklists; the LLM agent performs semantic comparison.

Rollback restores prior capture implementation if needed and removes the QA capture tool, services, DTOs, prompts, DI registration, safety smoke entries, and tests.

## Future Extension

- Multi-reference comparison bundles.
- Automatic silhouette or edge-map metrics as deterministic helper data.
- Companion UI display of QA snapshots.
- User annotation of captured QA images for guided correction.
