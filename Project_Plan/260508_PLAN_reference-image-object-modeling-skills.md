# Reference Image Object Modeling Skills Plan

## Background

The planned `ReferenceImageObjectModelingAgent` should not contain all modeling reasoning directly. Its steps need fixed, repeatable skill boundaries so the agent can route, retry, and report failures clearly.

The sofa review showed that skipping structured skills causes the LLM to jump from image recognition directly to available primitive calls. The missing middle layers are image-to-model brief extraction, primitive decomposition, initial massing planning/building, detail refinement, material planning, and iteration decisions.

## Goal

Add the skill layer required by a general reference-image object modeling agent.

This plan covers skills only. Atomic geometry, material, and viewport/visual QA tools are planned separately.

Target skills:

- `ReferenceImageModelBriefSkill`
- `ReferenceImagePrimitiveDecompositionSkill`
- `ReferenceImageInitialMassingSkill`
- `ReferenceImageDetailRefinementSkill`
- `ReferenceImageMaterialPlanningSkill`
- `ReferenceImageIterationDecisionSkill`

Non-goals:

- Adding MCP tools
- Implementing viewport capture or image comparison
- Creating one monolithic "model from image" skill
- Furniture-specific decomposition

## Architecture Ownership

- `Skills/Modeling/`
  - New skill classes for fixed modeling subworkflows.

- `Application/Services/`
  - Reusable services for converting brief/decomposition data into tool-ready construction plans.

- `Domain/Models/`
  - Pure structured models for image brief, object parts, primitive candidates, refinement items, material plans, and QA decisions.

- `Domain/Enums/`
  - Object modeling enums for edge character, primitive kind, material intent, detail kind, and iteration decision.

- `Contracts/Requests` and `Contracts/Responses`
  - Request and response DTOs for skill tools if these skills are exposed as MCP tools.

- `Prompts/Modeling/`
  - Prompt templates for image brief extraction, decomposition, refinement, and decision policy.

- `Server/AgentRegistration.cs`
  - Register skills for agent use.

Skills must not directly access RhinoCommon. They should call application services or MCP tools through established live-only paths.

## Key Design

### 1. Model brief skill

`ReferenceImageModelBriefSkill` produces a structured description before any Rhino mutation.

Expected output:

- object type and confidence
- number of visible objects
- primary target object
- viewpoint and camera assumptions
- approximate width/depth/height relationship
- major parts and hierarchy
- symmetry assumptions
- hard edge vs soft edge regions
- visible cutouts, seams, holes, legs, handles, supports, labels, fasteners, or repeated features
- dominant colors and material cues
- unknowns and risky assumptions

This skill should not choose exact Rhino commands yet.

### 2. Primitive decomposition skill

`ReferenceImagePrimitiveDecompositionSkill` turns the brief into buildable abstract shapes.

Expected output per part:

- part name
- parent part
- primitive kind candidates
- preferred primitive kind
- approximate local transform
- edge treatment intent
- whether the part is structural mass, subtractive detail, additive detail, surface detail, or material-only detail
- required tool family
- fallback primitive if preferred tool is unavailable

Primitive vocabulary should be general:

- box
- rounded box or bevelable box
- sphere or ellipsoid
- cylinder
- cone
- capsule
- pipe or tube
- planar panel
- profile extrusion
- sweep
- loft
- boolean cutter
- decal or texture plane

### 3. Initial massing skill

`ReferenceImageInitialMassingSkill` converts decomposition into a rough Rhino build plan and, when exposed through an agent, executes only the massing objects.

The skill should:

- create a target layer tree
- group primitives by object part
- avoid detail noise in first pass
- attach metadata that records source image, part role, primitive kind, and modeling stage
- return object ids and a massing summary

The skill should be usable before visual QA so the agent can inspect the initial massing.

### 4. Detail refinement skill

`ReferenceImageDetailRefinementSkill` consumes:

- the brief
- decomposition
- massing object ids
- visual QA feedback if available

It outputs detail operations:

- bevel or soften selected parts
- add seam/groove/raised strip geometry
- add holes or boolean cutouts
- add handles, feet, pads, cords, pipes, rims, panels, labels, or repeated elements
- remove detail candidates that would add visual noise without improving recognizability

The skill must not add texture-like geometry by default when a material or texture map is the better representation.

### 5. Material planning skill

`ReferenceImageMaterialPlanningSkill` plans material creation and assignment.

Expected output:

- material groups by object part
- base color
- roughness/specular/transparency intent where supported
- texture source or procedural approximation intent
- mapping scope
- fallback color-only assignment if texture support is missing

Actual material creation and assignment belongs to tools planned separately.

### 6. Iteration decision skill

`ReferenceImageIterationDecisionSkill` evaluates structured feedback after visual QA and chooses:

- continue massing revision
- continue detail revision
- continue material revision
- accept result
- stop and report gap

This skill should not perform visual comparison itself. It consumes the visual QA tool output and the modeling trace.

### 7. Skill exposure

These skills may be exposed as MCP skill tools only when useful for direct runtime routing. If exposed, their `[Description]` metadata must describe when they beat atomic tools and when they are not applicable.

The agent remains the preferred route for full image-to-model workflows.

## Involved Files

Likely production files when executed:

- `src/MCP_Rhino.Server/Skills/Modeling/ReferenceImageModelBriefSkill.cs`
- `src/MCP_Rhino.Server/Skills/Modeling/ReferenceImagePrimitiveDecompositionSkill.cs`
- `src/MCP_Rhino.Server/Skills/Modeling/ReferenceImageInitialMassingSkill.cs`
- `src/MCP_Rhino.Server/Skills/Modeling/ReferenceImageDetailRefinementSkill.cs`
- `src/MCP_Rhino.Server/Skills/Modeling/ReferenceImageMaterialPlanningSkill.cs`
- `src/MCP_Rhino.Server/Skills/Modeling/ReferenceImageIterationDecisionSkill.cs`
- `src/MCP_Rhino.Server/Application/Services/ReferenceImageModelingBriefService.cs`
- `src/MCP_Rhino.Server/Application/Services/ReferenceImagePrimitiveDecompositionService.cs`
- `src/MCP_Rhino.Server/Application/Services/ReferenceImageModelingPlanService.cs`
- `src/MCP_Rhino.Server/Domain/Models/ReferenceImageModelBrief.cs`
- `src/MCP_Rhino.Server/Domain/Models/ReferenceImagePrimitiveDecomposition.cs`
- `src/MCP_Rhino.Server/Domain/Models/ReferenceImageRefinementPlan.cs`
- `src/MCP_Rhino.Server/Domain/Models/ReferenceImageMaterialPlan.cs`
- `src/MCP_Rhino.Server/Domain/Models/ReferenceImageModelingTrace.cs`
- `src/MCP_Rhino.Server/Domain/Enums/ReferenceImageObjectModelingEnums.cs`
- `src/MCP_Rhino.Server/Prompts/Modeling/ReferenceImageModelBrief.md`
- `src/MCP_Rhino.Server/Prompts/Modeling/ReferenceImagePrimitiveDecomposition.md`
- `src/MCP_Rhino.Server/Prompts/Modeling/ReferenceImageDetailRefinement.md`
- `src/MCP_Rhino.Server/Prompts/Modeling/ReferenceImageIterationDecision.md`
- `src/MCP_Rhino.Server/Server/AgentRegistration.cs`

Potential MCP skill-tool wrappers, if execution chooses to expose them:

- `src/MCP_Rhino.Server/Tools/Geometry/ReferenceImage/ExtractReferenceImageModelBriefSkillTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/ReferenceImage/DecomposeReferenceImagePrimitivesSkillTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/ReferenceImage/PlanReferenceImageRefinementSkillTool.cs`

Required test artifacts if executed:

- `Project_Test/260508_TEST_reference-image-object-modeling-skills/`

## Usage

The agent should use these skills as follows:

1. brief skill
2. decomposition skill
3. initial massing skill
4. visual QA from the visual QA plan
5. refinement skill
6. material planning skill
7. visual QA again
8. iteration decision skill

Direct skill-tool use is acceptable for diagnostics, for example asking only for a decomposition plan without creating Rhino objects.

## Acceptance Criteria

- Skills are general-purpose and object-oriented, not sofa/furniture-specific.
- Skills produce structured DTOs suitable for agent decisions and tests.
- Initial massing skill separates coarse model creation from detail refinement.
- Detail refinement skill avoids converting all texture cues into geometry.
- Iteration decision skill consumes QA output but does not own viewport capture or image comparison.
- Skills do not access RhinoCommon directly.
- Any exposed skill tools have explicit MCP safety annotations and routing descriptions.
- `dotnet build .\MCP_Rhino.sln -c Debug` passes.
- `dotnet build .\MCP_Rhino.sln -c Release` passes.
- Tests verify deterministic parsing/mapping on at least three different object categories.

## Risks And Rollback

- Risk: skill outputs become too verbose for MCP payloads.
  - Mitigation: keep bounded summaries and refer to created object ids for live geometry.
- Risk: decomposition quality depends on LLM prompt behavior.
  - Mitigation: use structured schemas, required uncertainty fields, and tests against representative briefs.
- Risk: overlapping skill boundaries create drift.
  - Mitigation: keep each skill focused on one stage and store shared models in Domain.

Rollback removes the skill classes, DTOs, prompt files, services, registration, and tests. Existing modeling tools remain unaffected.

## Future Extension

- Multi-image reconciliation skill for front/side/detail references.
- User correction ingestion skill.
- Domain-specific decomposition plugins only after general object modeling proves useful.
- A modeling trace viewer in the companion UI.
