# Reference Image Object Modeling Agent Plan

## Background

The sofa review exposed a runtime gap: an LLM can see a reference image and can call Rhino primitive tools, but the current MCP_Rhino runtime does not provide a goal-driven workflow that controls how image understanding becomes a Rhino model. The result was a sofa-like assembly made from architectural primitives, with no explicit checkpoint for whether the first massing was good enough before adding seams, fabric strips, and other details.

The required capability is broader than furniture. A user may provide a reference image of a product, object, fixture, vehicle, sculpture, tool, appliance, facade element, furniture item, or other physical artifact and ask Rhino to build an approximate model. The runtime needs an agent that can recognize the object, decompose it into buildable primitives, create an initial model, visually inspect the massing, refine details, apply materials, and decide when the result is close enough or when current capabilities are insufficient.

## Goal

Add a goal-driven `ReferenceImageObjectModelingAgent` that orchestrates reference-image-driven object modeling in the live Rhino document.

The agent should:

- identify the object or objects in supplied reference image context
- produce a structured modeling brief
- choose a build strategy based on primitive decomposition and available tools
- build an initial massing model
- run visual QA checkpoints after initial massing, after detail refinement, and before final response
- apply material and texture intent extracted from the reference image
- iterate when visual QA reports actionable corrections
- stop and report a capability gap instead of forcing a bad model

Non-goals for this plan:

- Implementing the skills, tools, or visual QA tool itself
- Building a furniture-specific agent
- Adding native image model inference inside the Rhino server
- Replacing the general MCP tool surface with one monolithic "make object" tool

## Architecture Ownership

- `Agents/Modeling/`
  - New `ReferenceImageObjectModelingAgent`.
  - Owns branching, sequencing, retry limits, and stop/gap decisions.
  - Calls skills and tool-facing services; does not call RhinoCommon directly.

- `Skills/Modeling/`
  - Agent dependencies are planned separately in `260508_PLAN_reference-image-object-modeling-skills.md`.

- `Tools/Geometry`, `Tools/Editing`, `Tools/File`, `Tools/Viewport`
  - Missing atomic tools are planned separately in `260508_PLAN_reference-image-object-modeling-tools.md` and `260508_PLAN_reference-image-visual-qa.md`.

- `Contracts/Requests` and `Contracts/Responses`
  - Agent-facing request/response DTOs for reference image path/context, target layer, quality budget, iteration budget, and final report.

- `Prompts/`
  - Agent prompt templates for routing and decision policy, if the implementation needs long instructions.

- `Server/AgentRegistration.cs`
  - Register the agent and any new skills.

The agent must remain live-only for Rhino truth. It may accept user-supplied external image paths as runtime context, but all Rhino model inspection and mutation must go through live MCP tools and live adapters.

## Key Design

### 1. General object modeling, not furniture modeling

The agent should be named and described around reference-image object modeling. It should not route only to furniture primitives or sofa-specific logic.

Proposed code name:

- `ReferenceImageObjectModelingAgent`

User-facing description should state that it builds an approximate Rhino model from supplied reference image context by decomposing visible objects into buildable primitive geometry, adding details and materials, and checking visual similarity.

### 2. Agent workflow

The first complete workflow should be:

1. **Assess inputs**
   - Validate reference image context exists and the live Rhino document is available.
   - Read document summary and target layer state.
   - Confirm whether existing capabilities can attempt the object.

2. **Extract modeling brief**
   - Call the planned image-to-model brief skill.
   - Capture object class, visible parts, viewpoint, proportions, edges, material cues, and uncertainty.

3. **Primitive decomposition**
   - Call the planned decomposition skill.
   - Convert the object into abstract buildable shapes such as boxes, rounded boxes, cylinders, spheres, cones, pipes, lofted forms, panels, and cutouts.
   - Select candidate tools for each part.

4. **Initial massing build**
   - Build rough geometry first.
   - Avoid small details until the main proportions and silhouette have been checked.

5. **Massing visual QA checkpoint**
   - Use the planned visual QA capability after the initial build.
   - Check object identity, major silhouette, part count, approximate scale, and proportions.
   - If massing fails, revise primitive decomposition or report the gap before adding detail.

6. **Detail refinement**
   - Add bevels, seams, grooves, holes, handles, pipes, pads, repeated elements, labels, or other reference-specific details.

7. **Material extraction and application**
   - Create or select materials from reference cues.
   - Apply by model part, not globally unless appropriate.

8. **Final visual QA checkpoint**
   - Compare reference and model again.
   - Decide whether to stop, iterate, or report what cannot be improved with current tools.

9. **Report**
   - Return created object counts, layers, materials, QA summary, remaining limitations, and suggested manual follow-up if needed.

### 3. Visual QA during massing is required

The agent must not postpone visual QA until the end. The most important checkpoint is immediately after initial massing, before detailed geometry and materials are added. This prevents a bad coarse model from being decorated into a worse final result.

Minimum QA gates:

- **Gate A: initial massing**
  - Checks large silhouette, major component layout, part count, and proportions.
- **Gate B: detail/material refinement**
  - Checks whether detail additions made the model closer or just noisier.
- **Gate C: final acceptance**
  - Checks recognizability and whether the result is close enough for the user request.

The visual QA tool itself is intentionally deferred to `260508_PLAN_reference-image-visual-qa.md`.

### 4. Iteration policy

The agent needs bounded iteration. Suggested defaults:

- one initial attempt
- up to two massing correction loops
- up to two detail/material correction loops
- hard stop with explicit gap report when corrections require missing geometry tools or missing visual capture support

The agent should not keep adding details when QA says the massing is wrong.

### 5. Capability gap behavior

The agent should stop when:

- the image contains a subject that cannot be decomposed into currently supported primitives
- required geometry operators are missing
- required material operations are missing
- visual QA capture/comparison is unavailable
- image context is too ambiguous to choose a buildable object
- the live document is not accessible

The failure response must identify the missing layer: Tool, Skill, Agent, Resource, or external connector.

## Involved Files

Likely production files when executed:

- `src/MCP_Rhino.Server/Agents/Modeling/ReferenceImageObjectModelingAgent.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/ReferenceImageObjectModelingAgentRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/ReferenceImageObjectModelingAgentResponse.cs`
- `src/MCP_Rhino.Server/Domain/Models/ReferenceImageObjectModelingPlan.cs`
- `src/MCP_Rhino.Server/Domain/Enums/ReferenceImageModelingEnums.cs`
- `src/MCP_Rhino.Server/Prompts/Modeling/ReferenceImageObjectModelingAgent.md`
- `src/MCP_Rhino.Server/Server/AgentRegistration.cs`

Likely dependencies from other plans:

- `ReferenceImageModelBriefSkill`
- `ReferenceImagePrimitiveDecompositionSkill`
- `ReferenceImageInitialMassingSkill`
- `ReferenceImageDetailRefinementSkill`
- `ReferenceImageMaterialPlanningSkill`
- missing primitive/detail/material tools from the tools plan
- visual QA tool from the visual QA plan

Required test artifacts if executed:

- `Project_Test/260508_TEST_reference-image-object-modeling-agent/`

## Usage

Expected runtime use:

- User supplies one or more reference images and asks to build the object in Rhino.
- The agent creates a target layer tree, builds a rough model, performs a massing QA pass, refines detail/materials, performs final QA, and reports the outcome.

Example intent:

> Build a rough Rhino model based on this product photo. It does not need exact dimensions, but it should be recognizable and use similar materials.

## Acceptance Criteria

- Agent is general-purpose and not furniture-specific.
- Agent uses skills/tools instead of direct RhinoCommon access.
- Agent performs a visual QA checkpoint after initial massing before detail refinement.
- Agent records or returns a structured modeling trace: brief, decomposition, massing objects, QA results, revisions, material assignments, and final status.
- Agent has a bounded iteration policy.
- Agent stops with a clear gap report when visual QA, materials, or geometry operations are missing.
- Tool safety annotation smoke remains passing if any MCP tool surface changes are included in execution.
- `dotnet build .\MCP_Rhino.sln -c Debug` passes.
- `dotnet build .\MCP_Rhino.sln -c Release` passes.
- Live smoke demonstrates an end-to-end simple object from image context with at least one massing QA checkpoint.

## Risks And Rollback

- Risk: the agent may overfit to current primitive tools and still produce crude models.
  - Mitigation: require an explicit gap report when primitive decomposition cannot represent the object.
- Risk: visual QA can be expensive or unreliable.
  - Mitigation: make QA bounded and checkpoint-based, and defer implementation details to the visual QA plan.
- Risk: agent scope becomes too broad.
  - Mitigation: keep the agent as an orchestrator and push fixed substeps into skills.

Rollback removes the agent class, request/response DTOs, prompt files, DI/registration changes, and smoke hooks. Lower-level tools and skills remain available if they were implemented independently.

## Future Extension

- Multi-image modeling with front/side/detail references.
- User correction loop where the user marks what is wrong after a QA pass.
- Saved modeling trace as document user text for later revision.
- Optional use of external image-generation or segmentation connectors if the runtime explicitly supports them later.
