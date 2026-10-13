# Reference Image Agent Access And Briefing Plan

## Background

`ReferenceImageObjectModelingAgent` exists as an internal server service and can be reached by developer smoke commands, but it is not exposed on the general MCP tool surface. Both Debug and Release MCP hosts register tools and resources through assembly scanning; neither host exposes internal agents unless a tool wrapper is present.

This creates two runtime problems:

- The Debug bridge and Release panel-bound MCP routes can call atomic tools but cannot call the reference-image object modeling agent directly.
- A user can say "model this image", but the server-side agent currently expects a structured `BriefRequest`; it does not perform raw image-to-brief vision inference inside Rhino.

The strategy is to make the structured agent available everywhere MCP tools are available, while keeping raw image interpretation as a separate, explicit briefing capability.

## Goal

Add a durable access structure for reference-image object modeling:

- expose `ReferenceImageObjectModelingAgent` through a thin MCP tool wrapper available in Debug bridge, Release debug pipe, and Release panel-bound routes
- define a clear image-to-brief contract so callers know when they must provide structured observations
- add a future-compatible briefing capability that can validate, normalize, or eventually obtain a structured brief from a vision-capable provider
- make capability gaps explicit when a user supplies only an image path and no image-brief provider is available
- keep business orchestration in `Agents/`, fixed substeps in `Skills/`, and MCP exposure in `Tools/`

Non-goals:

- putting MCP attributes directly on agent classes
- adding raw multimodal image inference inside Rhino without a provider contract
- reading arbitrary image files from disk as a hidden fallback
- replacing atomic geometry/material tools with a single untyped modeling command

## Architecture Ownership

- `Agents/Modeling/`
  - Keep `ReferenceImageObjectModelingAgent` as the orchestration owner.
  - The agent remains transport-agnostic and does not know MCP details.

- `Tools/Modeling/` or `Tools/Geometry/ReferenceImage/`
  - Add thin MCP wrappers that expose modeling workflows.
  - The preferred structure is `Tools/Modeling/` if the architecture guide is updated to recognize goal-level modeling entrypoints.
  - If no new tool family is approved, place the wrapper under `Tools/Geometry/ReferenceImage/` as a scoped modeling adapter.

- `Skills/Modeling/`
  - Keep existing brief, decomposition, massing, refinement, material, and iteration skills.
  - Add only fixed briefing helpers here if they are reusable by the agent.

- `Application/Services`
  - Add services for brief validation, normalization, and provider routing.
  - Do not put RhinoCommon access here except through existing live abstractions.

- `Application/Interfaces`
  - Add `IReferenceImageBriefProvider` only if a provider abstraction is implemented.
  - Initial provider can be `StructuredReferenceImageBriefProvider`, which accepts caller-supplied observations and returns a canonical brief.
  - Future providers can bridge to panel/client-side multimodal inference or an approved external connector.

- `Contracts/Requests` and `Contracts/Responses`
  - Add request/response types for agent wrapper access and brief-provider outcomes if existing DTOs are not enough.
  - Reuse `ReferenceImageObjectModelingAgentRequest` where possible.

- `Resources/` or `Tools/Reference/`
  - Publish the reference-image brief schema and modeling policy as reference-only content.
  - Add a read-only fallback tool only if MCP resources are not reliable in target clients.

- `Prompts/Modeling/` and `Prompts/Runtime/`
  - Keep agent-specific prompt policy in modeling prompts.
  - Add runtime guidance only if the panel/Companion needs to teach the LLM how to convert a visible image into `BriefRequest`.

## Key Design

### 1. Add a Thin Agent Tool Wrapper

Create a tool such as:

- `RunReferenceImageObjectModelingAgentTool`
- method: `RunReferenceImageObjectModelingAgent`

The tool should:

- be decorated with `[McpServerToolType]`
- expose a method decorated with `[McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]` when it only mutates the live Rhino document and does not read external image bytes
- inject `ReferenceImageObjectModelingAgent`
- accept `ReferenceImageObjectModelingAgentRequest`
- call `ReferenceImageObjectModelingAgent.Run(request)`
- return `OperationResponse<ReferenceImageObjectModelingAgentResponse>`

The wrapper must not duplicate agent logic. It is only an MCP access adapter.

This makes the agent available through all MCP hosts because Debug and Release both register the same `.AddRhinoTools()` surface.

### 2. Keep Agent Classes Off The MCP Surface

Do not place `[McpServerTool]` directly on `ReferenceImageObjectModelingAgent.Run`.

Reasons:

- agent classes should remain orchestration units, not transport adapters
- MCP safety metadata belongs at the externally callable boundary
- wrapper tools can have precise descriptions, safety annotations, and future compatibility behavior without coupling the agent to MCP SDK details

### 3. Define A Briefing Contract

The modeling agent should continue to require structured image observations. The required contract is:

- object type
- visible target object count
- primary target object
- viewpoint assumptions
- relative width/depth/height
- parts with roles, proportions, edge character, and material keys
- detail cues with role and preferred representation
- material cues with base color, roughness, transparency, and texture-likely flags
- unknowns and risky assumptions

This contract should be documented as:

- a reference resource if resources are reliable in the target MCP client
- a read-only reference tool fallback if resources are not reliable
- a runtime prompt note for panel/Companion routes that have image-understanding LLM context

### 4. Add Explicit Image-Only Gap Behavior

When `RunReferenceImageObjectModelingAgentTool` receives only a raw `ReferenceImagePath` and no structured `BriefRequest`, it should not guess from pixels.

It should return a clear gap:

- layer: `ExternalConnector` or `Agent`
- code: `IMAGE_BRIEF_REQUIRED`
- message: "Raw image inference is not available in this route. Provide structured BriefRequest or use an image-brief provider."

This keeps runtime behavior honest and prevents the agent from pretending that it inspected an image it cannot inspect.

### 5. Stage The Image Brief Capability

The image-to-brief side should be staged separately from the agent wrapper.

Stage A: structured brief validation

- expose or document a `BuildReferenceImageModelBriefRequest` schema
- validate required fields
- normalize caller-supplied observations into `ReferenceImageModelBrief`
- return warnings for missing proportions, ambiguous material cues, and non-geometry visual cues

Stage B: client/panel prompt contract

- add prompt guidance for vision-capable clients:
  - inspect the user-visible image
  - produce the structured `BriefRequest`
  - mark material and shadow cues correctly
  - call `RunReferenceImageObjectModelingAgent`
- keep this outside Rhino server pixel inference

Stage C: optional provider abstraction

- add `IReferenceImageBriefProvider`
- first implementation: structured input provider
- future implementation: approved external multimodal provider or panel-side provider
- provider failures must return capability gaps, not fallback geometry guesses

### 6. Add Routing Metadata For Model Selection

The tool description should be explicit:

- use this tool for full reference-image object modeling when a structured brief exists
- do not use atomic primitive tools for a full object if this tool is applicable
- do not call this tool for raw image-only requests unless a structured brief is supplied
- use material plans for fabric, grain, texture, highlight, and shadow cues instead of geometry

This is important because MCP clients route primarily from tool descriptions.

### 7. Preserve Debug And Release Symmetry

No special Debug-only agent path should be added.

The same MCP tool wrapper should be visible through:

- Debug bridge pipe: `\\.\pipe\mcp_rhino`
- Release debug pipe: `\\.\pipe\mcp_rhino`
- Release panel-bound document pipes

The only difference should remain document binding:

- global debug pipe targets `RhinoDoc.ActiveDoc`
- panel-bound pipe targets its bound document serial number

## Involved Files

Likely production files:

- `src/MCP_Rhino.Server/Tools/Modeling/RunReferenceImageObjectModelingAgentTool.cs`
- or `src/MCP_Rhino.Server/Tools/Geometry/ReferenceImage/RunReferenceImageObjectModelingAgentTool.cs`
- `src/MCP_Rhino.Server/Agents/Modeling/ReferenceImageObjectModelingAgent.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/ReferenceImageObjectModelingAgentRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/ReferenceImageObjectModelingAgentResponse.cs`
- `src/MCP_Rhino.Server/Application/Services/ReferenceImageModelingBriefService.cs`
- `src/MCP_Rhino.Server/Application/Services/ReferenceImageBriefValidationService.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IReferenceImageBriefProvider.cs`
- `src/MCP_Rhino.Server/Domain/Models/ReferenceImageModelBrief.cs`
- `src/MCP_Rhino.Server/Domain/Rules/ReferenceImageVisualCueGeometryPolicy.cs`
- `src/MCP_Rhino.Server/Resources/ReferenceImageBriefSchemaResource.cs`
- `src/MCP_Rhino.Server/Tools/Reference/GetReferenceImageBriefSchemaTool.cs`
- `src/MCP_Rhino.Server/Prompts/Modeling/ReferenceImageObjectModelingAgent.md`
- `src/MCP_Rhino.Server/Prompts/Runtime/McpRhinoRuntimePolicyBundle.md`
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`
- `src/MCP_Rhino.Server/Server/AgentRegistration.cs`
- `src/MCP_Rhino.Server/Server/ToolRegistration.cs`
- `Project_Guides/MCP_Rhino Architecture.md` if `Tools/Modeling/` is added as a new tool family

Likely test artifacts when executed:

- `Project_Test/260509_TEST_reference-image-agent-access-and-briefing/DeveloperCommandHandler.ReferenceImageAgentAccessAndBriefingSmokeTest.cs`
- Rhino smoke command such as `_McpReferenceImageAgentAccessSmoke`
- sample structured request JSON for a sofa or simple product object

Required safety validation:

- update or run MCP tool safety annotation smoke for the new wrapper tool
- run tool inventory/routing smoke to verify the wrapper is visible

## Usage

Structured route:

1. Caller or vision-capable client creates `BriefRequest` from the image.
2. Caller invokes `RunReferenceImageObjectModelingAgent`.
3. Agent builds massing, details, materials, QA, and returns trace/gaps.

Image-only route without provider:

1. Caller invokes the tool with only `ReferenceImagePath`.
2. Tool returns `IMAGE_BRIEF_REQUIRED`.
3. Caller obtains or supplies structured `BriefRequest`.
4. Caller invokes the tool again.

Future image-provider route:

1. Caller invokes a briefing capability with image context.
2. Brief provider returns structured `BriefRequest`.
3. Agent wrapper runs the modeling workflow.

## Acceptance Criteria

- `RunReferenceImageObjectModelingAgent` appears in the MCP tool inventory.
- The tool is available through Debug and Release builds because it is registered by assembly scanning.
- The tool has explicit MCP safety annotations and a non-empty description.
- The wrapper contains no business logic beyond validation and calling the agent.
- A structured sofa/product brief can run through the wrapper in live Rhino and create objects on the target layer.
- A raw image-only request returns `IMAGE_BRIEF_REQUIRED` instead of attempting unsupported pixel inference.
- The brief schema is discoverable through a resource or read-only reference tool.
- Runtime prompt/policy guidance tells image-capable clients to produce `BriefRequest` before calling the modeling agent.
- Material, texture, highlight, and shadow cues remain classified as non-geometry.
- Debug and Release server/plugin builds pass.
- MCP tool safety smoke passes in Debug and Release.
- A smoke test verifies tool visibility and the image-only gap behavior.

## Risks And Rollback

- Risk: adding `Tools/Modeling/` weakens the current "tools are atomic" boundary.
  - Mitigation: document it as a thin external access adapter family for agent entrypoints only; keep all workflow logic in `Agents/` and `Skills/`.

- Risk: MCP clients may route raw image-only requests to the modeling tool and expect automatic vision.
  - Mitigation: tool description and response must explicitly require structured brief input unless a provider exists.

- Risk: a future image provider could introduce hidden open-world file access.
  - Mitigation: require explicit provider selection, explicit image path/file handling, and correct `OpenWorld` annotation when image bytes are read.

- Risk: Release panel-bound and Debug global pipe behavior diverge.
  - Mitigation: expose only through `.AddRhinoTools()` and validate both configurations.

Rollback:

- remove the agent wrapper tool
- remove new brief provider/validation service files
- remove schema resource or fallback reference tool
- remove prompt/runtime policy additions
- revert any `Project_Guides` tool-family update
- remove smoke registration and test artifacts

The internal `ReferenceImageObjectModelingAgent` and atomic tools remain intact.

## Future Extensions

- multi-image brief creation for front/side/detail reference sets
- user correction ingestion that updates `BriefRequest` after a failed QA pass
- provider registry for approved multimodal image brief sources
- companion UI form for reviewing/editing generated briefs before Rhino mutation
- saved modeling traces linked to source image and target object ids
