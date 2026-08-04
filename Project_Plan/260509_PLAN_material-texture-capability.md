# Material Texture Capability Plan

## Background

The current reference-image object modeling path can create Rhino document materials with base color, roughness, and transparency, then assign those materials to objects. It cannot create bitmap or procedural texture images, attach image textures to materials, or control texture mapping on target objects.

This gap caused woven fabric and shadows from a sofa reference to be modeled as physical strips and dark geometry. That is the wrong representation: fabric weave, grain, color variation, gloss, and shadows are visual/material cues, not object parts.

## Goal

Add a material texture capability that lets runtime agents represent material appearance with Rhino materials and texture maps instead of geometry.

Primary goals:

- create or reuse Rhino materials with bitmap texture slots
- generate simple procedural texture images for common materials such as woven fabric when no user texture image exists
- accept explicit user-supplied texture image paths
- assign textured materials to objects by ObjectId or filter
- apply predictable texture mapping controls such as box, plane, UV, scale, repeat, rotation, and offset
- report explicit capability gaps when a cue is lighting, shadow, or unsupported material behavior

Non-goals:

- full raw image semantic recognition inside the Rhino server
- calling an external image generation model directly from the Rhino plugin
- modeling material texture, highlights, or shadows as geometry
- replacing Rhino's native renderer or material editor
- scanning directories for image assets

## Architecture Ownership

- `Tools/Materials`
  - Add new MCP tools for texture image generation, textured material creation/update, material assignment, and texture mapping.

- `Application/Services`
  - Extend `RhinoMaterialService` or add focused services for procedural texture generation, texture material creation, and mapping orchestration.

- `Application/Interfaces`
  - Extend `ILiveRhinoMaterialOperator` or add a sibling interface if the mapping surface becomes large.

- `Infrastructure/Rhino/Live`
  - Implement RhinoCommon material table mutation, bitmap texture attachment, and object texture mapping.

- `Contracts/Requests` and `Contracts/Responses`
  - Add typed DTOs for material texture specs, texture image generation specs, mapping specs, warnings, and gap records.

- `Domain/Enums` and `Domain/Models`
  - Add stable enums for texture source kind, material texture kind, mapping kind, procedural pattern kind, and texture channels.

- `Skills/Modeling` and `Agents/Modeling`
  - Keep reference-image modeling routing material cues to material tools. Do not let the agent convert material or shadow cues into geometry.

## Key Design

### 1. Split image asset creation from Rhino material mutation

Use separate tools for asset generation and live material mutation:

- `GenerateProceduralTextureImage`
  - Creates a deterministic bitmap file from structured parameters.
  - Open-world because it writes a file.
  - Supported first patterns: woven fabric, fine noise, linear grain, checker/stripe test pattern.

- `CreateTexturedRenderMaterials`
  - Creates Rhino materials that reference explicit texture image paths.
  - Open-world because it reads caller-provided image paths.
  - Mutates only the active Rhino document material table.

This split avoids forcing Rhino document mutation when the caller only needs an asset and keeps external file behavior explicit.

### 2. Keep texture generation deterministic and bounded

The server should not call an LLM or multimodal image model. It can generate simple procedural bitmaps from structured parameters:

- width and height in pixels
- base, warp, weft, and variation colors
- thread spacing and thickness
- noise amount
- tileable flag
- output path and overwrite policy

If the user wants AI-generated bitmap textures, that should remain an external connector or user-supplied image path. The material tool only consumes the explicit resulting image file.

### 3. Add textured material creation/update

Candidate tools:

- `CreateTexturedRenderMaterials`
- `UpdateRenderMaterialTextures`
- `InspectRenderMaterialTextures`

Initial channels:

- diffuse/base color bitmap
- bump/normal placeholder support only after RhinoCommon behavior is validated
- alpha/transparency only after a dedicated test proves stable behavior

The first execution should prefer Rhino's document material table path before deeper RenderMaterial/PBR APIs. If RhinoCommon requires RenderMaterial for texture slots, record that deviation in EXET.

### 4. Add texture mapping controls

Candidate tools:

- `PreviewApplyTextureMapping`
- `ApplyTextureMapping`

Inputs:

- ObjectIds or filter criteria
- mapping kind: box, plane, cylindrical, spherical, surface/UV
- mapping size or repeat
- rotation
- offset
- mapping channel

Use preview/apply if mapping resolution can expand from filters or if the tool can touch many objects. Direct apply is acceptable for explicit ObjectIds only if the response reports every changed object.

### 5. Preserve material-vs-geometry policy in agents

Reference-image object modeling should route cues as follows:

- true object parts: geometry
- seams, handles, rims, supports, holes: geometry when they change object recognition
- woven fabric, grain, printed patterns, speckle, roughness, gloss, highlights: material/texture
- cast shadows, contact shadows, ambient occlusion, studio lighting: do not model; ignore or record as render/lighting context

If texture tools are unavailable, the agent should assign base color material and emit a capability gap rather than creating fake weave/shadow geometry.

## Involved Files

Likely production files:

- `src/MCP_Rhino.Server/Tools/Materials/CreateTexturedRenderMaterialsTool.cs`
- `src/MCP_Rhino.Server/Tools/Materials/UpdateRenderMaterialTexturesTool.cs`
- `src/MCP_Rhino.Server/Tools/Materials/GenerateProceduralTextureImageTool.cs`
- `src/MCP_Rhino.Server/Tools/Materials/InspectRenderMaterialTexturesTool.cs`
- `src/MCP_Rhino.Server/Tools/Materials/ApplyTextureMappingTool.cs`
- `src/MCP_Rhino.Server/Tools/Materials/PreviewTextureMappingTool.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoMaterialService.cs`
- `src/MCP_Rhino.Server/Application/Services/ProceduralTextureImageService.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveRhinoMaterialOperator.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoMaterialOperator.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/ReferenceImageMaterialRequests.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/ReferenceImageMaterialResponses.cs`
- `src/MCP_Rhino.Server/Domain/Models/ReferenceImageMaterialSpecs.cs`
- `src/MCP_Rhino.Server/Domain/Enums/ReferenceImageObjectModelingEnums.cs`
- `src/MCP_Rhino.Server/Application/Services/ReferenceImageMaterialPlanningService.cs`
- `src/MCP_Rhino.Server/Prompts/Modeling/ReferenceImageObjectModelingAgent.md`
- `src/MCP_Rhino.Server/Prompts/Modeling/ReferenceImageDetailRefinement.md`

Likely test artifacts:

- `Project_Test/YYMMDD_TEST_material-texture-capability/DeveloperCommandHandler.MaterialTextureCapabilitySmokeTest.cs`
- Rhino smoke command such as `_McpMaterialTextureCapabilitySmoke`
- generated texture sample outputs under a test-controlled temp directory

## Usage

Expected runtime sequence for a woven fabric sofa:

1. Agent classifies fabric weave as material-only, not detail geometry.
2. Agent requests or receives structured fabric parameters from the LLM observation layer.
3. `GenerateProceduralTextureImage` writes a tileable woven fabric image to an explicit output path.
4. `CreateTexturedRenderMaterials` creates a Rhino material with the generated image in the diffuse channel.
5. `ApplyObjectMaterials` or a textured assignment tool assigns the material to sofa cushion and shell objects.
6. `ApplyTextureMapping` sets box or surface mapping scale so the weave reads at plausible object scale.
7. Visual QA checks that no material or shadow cue was converted into fake geometry.

## Acceptance Criteria

- A live Rhino smoke can create a small tileable woven texture image and attach it to a material.
- The material appears in `GetDocumentSummary` material output or a dedicated material inspection response.
- The material can be assigned to explicit object ids.
- Texture mapping can be applied to explicit object ids and reports changed object count.
- Reference-image material planning records texture fallbacks when texture creation or mapping fails.
- The reference-image object modeling agent does not create geometry for fabric weave, material grain, highlights, or shadows.
- MCP safety annotations are explicit for every new tool.
- Debug and Release server/plugin builds pass, or EXET records a known lock issue with alternate output validation.

## Risks And Rollback

- RhinoCommon material APIs may differ between document materials and render materials.
  - Mitigation: start with one diffuse bitmap channel and validate in live Rhino before adding PBR channels.

- Texture mapping may be renderer- or viewport-dependent.
  - Mitigation: expose inspection output and use a live smoke with viewport capture.

- Generated texture files can become unmanaged external assets.
  - Mitigation: require explicit output paths, overwrite flags, and return all written paths.

- Open-world filesystem handling increases risk.
  - Mitigation: no directory scanning, no implicit asset discovery, and clear `OpenWorld` annotations.

- Rollback:
  - Remove the new material texture tools, DTOs, services, and smoke registrations.
  - Existing color/roughness material tools remain unchanged.
  - Agent prompt guardrails can remain because they describe correct modeling policy even without texture tools.

## Future Extensions

- normal/bump map generation
- displacement map support
- decal/image-plane material carriers for labels and logos
- material library presets
- texture asset packaging with 3dm export workflows
- external AI texture generator connector that returns explicit image paths for the material tools to consume

## Revision Record (2026-05-09)

- Added an immediate enforcement slice before full texture tooling: reference-image detail planning and `ReferenceImageObjectModelingAgent` execution must suppress material, texture, lighting, highlight, and shadow cues as non-geometry while preserving true physical details such as seams.
