# Reference Image Accurate Product Modeling PLAN

## Background

Recent reference-image furniture modeling exposed a capability gap in the current
`ReferenceImageObjectModelingAgent` path. The current pipeline can place simple
axis-aligned primitives from a structured brief, but it cannot reliably model
product geometry whose identity depends on sloped parts, tapered members, profile
curves, attachment relationships, real-world dimensions, and mirrored assemblies.

The Kyajah armchair case is the target regression:

- the chair has visible product dimensions
- the upholstered seat and back are sloped and rounded
- the wooden side frames are mirrored A-frame structures
- legs and rails are angled/tapered
- fabric weave, wood grain, room shadows, rug lines, and dimension labels are not
  geometry

The fix should start with better product-modeling structure before adding SubD.
SubD can improve upholstery later, but it will not solve incorrect placement,
orientation, scale, or topology by itself.

## Goals

- Extend the reference-image modeling brief so a caller can describe accurate
  product geometry instead of only center/size boxes.
- Make the agent honor part orientation, local axes, anchors, dimensions, taper,
  and mirror relationships.
- Add atomic tools for common product/furniture modeling operations that are too
  cumbersome with the current primitive surface.
- Add a preview modeling-plan stage before live Rhino mutation.
- Keep material, texture, lighting, shadow, rug, room-context, and product-label
  cues out of geometry.
- Validate with the Kyajah armchair reference as an acceptance fixture.

## Non-Goals

- Do not add SubD in this plan. SubD is handled by
  `Project_Plan/260509_PLAN_subd-modeling-tools.md`.
- Do not add raw image inference inside the Rhino server. Image understanding
  remains upstream and must produce a structured brief.
- Do not replace existing general primitive tools unless their current contracts
  are actively wrong.
- Do not model material or lighting cues as physical detail.

## Architecture Ownership

- `Contracts/Requests` and `Contracts/Responses`: new or extended DTOs for an
  accurate product-modeling brief, preview plan, and geometry strategy.
- `Domain/Models` and `Domain/Enums`: product part coordinate frames, anchors,
  profile definitions, taper definitions, symmetry/mirror metadata, and strategy
  kinds.
- `Application/Services`: decomposition, planning, preview, and mapping from
  structured product parts to executable tool requests.
- `Skills/Modeling`: fixed workflows for accurate product massing and product
  detail planning.
- `Agents/Modeling`: update `ReferenceImageObjectModelingAgent` to run a
  plan-preview-execute-QA loop.
- `Tools/Geometry` and `Tools/Geometry/CurveOps`: new atomic geometry tools.
- `Tools/Modeling`: keep the MCP wrapper thin; do not move orchestration into
  the tool wrapper.
- `Infrastructure/Rhino/Live`: RhinoCommon implementation details for new live
  geometry operators.
- `Project_Test`: smoke and fixture validation for the chair case.

## Key Design

### 1. Brief V2: Product Geometry Contract

Extend the current brief with product-specific geometry fields:

- `realWorldDimensions`: width/depth/height plus units and confidence
- `originPolicy`: center, front-center-floor, lower-left-front, or explicit point
- `coordinateFrame`: width/depth/height axis assumptions
- per-part `localFrame`: origin, x axis, y axis, z axis or equivalent rotation
- per-part `anchors`: named points/edges such as front-bottom, rear-top, left-side
- per-part `profile`: front/side/top profile points in local coordinates
- per-part `taper`: top/bottom width/depth, or start/end section sizes
- `connections`: part-to-part attachments, supports, overlaps, and gaps
- `symmetry`: mirror plane, source part names, generated counterpart names
- `cueClassification`: physical geometry, material-only, reference-only

Keep backward compatibility by accepting the existing brief fields and mapping
them into V2 defaults.

### 2. Geometry Strategy Preview

Add an internal preview stage before mutation:

- `PlanReferenceImageProductGeometry`
- returns planned parts, selected tool strategy, bounding boxes, object count,
  warnings, material-only cues, reference-only cues, and capability gaps
- does not mutate Rhino

The agent should stop before mutation if:

- scale is ambiguous and no reasonable default is allowed
- a required product part can only be represented by a wrong fallback
- material/shadow/product-label cues are being requested as geometry
- a mirrored or connected assembly cannot be resolved consistently

### 3. New Geometry Tools

Add the first product-modeling tool slice:

- `CreateTaperedBoxes`
  - For tapered legs, wedge arms, slanted rails, and simple product supports.
  - Inputs should support local frame, start/end section sizes, corner radius,
    and optional cap behavior.

- `CreateProfileExtrusionsFromPoints`
  - Creates an extrusion directly from closed profile point arrays without first
    creating separate curve objects.
  - Use for side panels, A-frame plates, bracket-like forms, and product panels.

- `CreateLoftsFromProfiles`
  - Creates lofted Breps directly from profile point arrays without requiring
    prior curve object ids.
  - Use for cushion backs, molded forms, shell-like products, and soft transitions.

- `CreatePipesFromPoints`
  - Creates pipe or seam-cord geometry directly from polyline/NURBS point arrays.
  - Use for real seams, piping, rods, rails, and rounded visible edges.

- `MirrorObjectsByPlane`
  - Mirrors explicit object ids across an explicit plane.
  - Use for furniture side frames and repeated symmetrical parts.
  - Mutation tool; should include preview/apply if it can duplicate many objects.

Use existing `CreateRoundedBoxes` where it is sufficient, but expose orientation
through the reference-image plan instead of patching it manually after the agent.

### 4. Agent Process Update

Update `ReferenceImageObjectModelingAgent` to follow this sequence:

1. Build or receive structured brief.
2. Normalize to product brief V2.
3. Calibrate scale and origin.
4. Resolve part topology, anchors, and symmetry.
5. Produce a geometry preview plan.
6. Execute the plan only if it has no blocking gaps.
7. Apply materials and textures.
8. Capture QA views when requested.
9. Decide accept / revise / stop with explicit gap.

The agent must not silently downgrade hard product geometry to generic rounded
boxes when that loses the object identity.

### 5. Chair Acceptance Fixture

Use the Kyajah armchair references as a golden fixture:

- `C:/Users/Novan/Desktop/default_name.jpg`
- `C:/Users/Novan/Desktop/Kyajah+27.5''+Wide+Armchair+With+Solid+wood+legs.jpg`
- `C:/Users/Novan/Desktop/Upholstered+Armchair+with+Wooden+Legs+(Set+Of+2)-96213300.jpg`

The test should not depend on those desktop paths permanently; execution should
copy or encode minimal request samples under the matching `Project_Test` folder.

## Involved Files

Likely additions:

- `src/MCP_Rhino.Server/Contracts/Requests/ReferenceImageProductModelingRequests.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/ReferenceImageProductModelingResponses.cs`
- `src/MCP_Rhino.Server/Domain/Models/ReferenceImageProductGeometryPlan.cs`
- `src/MCP_Rhino.Server/Domain/Models/ProductGeometrySpecs.cs`
- `src/MCP_Rhino.Server/Domain/Enums/ProductGeometryEnums.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveProductGeometryBuilder.cs`
- `src/MCP_Rhino.Server/Application/Services/ReferenceImageProductGeometryPlanningService.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoProductGeometryService.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveProductGeometryBuilder.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/CreateTaperedBoxesTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/CurveOps/CreateProfileExtrusionsFromPointsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/CurveOps/CreateLoftsFromProfilesTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/CurveOps/CreatePipesFromPointsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/PreviewMirrorObjectsByPlaneTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/MirrorObjectsByPlaneTool.cs`

Likely modifications:

- `src/MCP_Rhino.Server/Agents/Modeling/ReferenceImageObjectModelingAgent.cs`
- `src/MCP_Rhino.Server/Application/Services/ReferenceImagePrimitiveDecompositionService.cs`
- `src/MCP_Rhino.Server/Skills/Modeling/ReferenceImageInitialMassingSkill.cs`
- `src/MCP_Rhino.Server/Domain/Models/ReferenceImageModelBrief.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/ReferenceImageObjectModelingSkillRequests.cs`
- `src/MCP_Rhino.Server/Tools/Modeling/RunReferenceImageObjectModelingAgentTool.cs`
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`
- `src/MCP_Rhino.Server/Prompts/Runtime/McpRhinoRuntimePolicyBundle.md`
- `Project_Test/260509_TEST_reference-image-accurate-product-modeling/`

## Usage

The runtime caller supplies a structured product brief with real-world dimensions,
local frames, anchors, profiles, materials, and reference-only cues. The modeling
agent returns a preview plan and then executes when allowed.

Example behavior:

- orange woven fabric -> material and texture
- walnut wood grain -> material and texture
- dimension labels -> reference-only, ignored as geometry
- rug lines and cast shadows -> reference-only, ignored as geometry
- sloped back cushion -> oriented cushion/profile/loft strategy
- A-frame side supports -> tapered/profile extrusion plus mirror strategy

## Acceptance Criteria

- The agent can create a Kyajah-like chair without manual post-agent geometry
  patching.
- The chair dimensions are within a bounded tolerance of 27.55 in wide, 30.31 in
  deep, and 29.92 in high after unit conversion.
- The back cushion is actually sloped, not vertical.
- The wooden side frames are mirrored A-frame assemblies with angled front and
  rear members.
- Tapered legs or side supports are represented with tapered/profile geometry,
  not only vertical rectangular blocks.
- Fabric weave, wood grain, product labels, rug lines, highlights, and shadows
  are not geometry.
- The MCP surface governance smoke passes.
- Debug and Release plugin builds pass.
- New tool methods include explicit MCP safety annotations and descriptions.

## Risks And Rollback

- RhinoCommon loft/profile operations can fail on invalid point order or
  self-intersecting profiles. Mitigation: validate profiles before mutation and
  return part-level failures.
- More expressive briefs can become verbose. Mitigation: keep V1 compatibility
  and allow defaults for simple objects.
- Mirroring can duplicate incorrect parts quickly. Mitigation: add preview for
  mirror operations and require explicit source ids.
- Product geometry tools may overlap with architectural tools. Mitigation: keep
  product/furniture-specific tools in general geometry/curve ops and avoid
  architecture naming.
- Rollback is straightforward: remove new tools, service registrations, DTOs,
  product-planning services, tests, and agent V2 branches; existing V1 brief and
  primitives remain usable.

## Follow-Up Extensions

- SubD upholstery tools after the agent can place and orient parts accurately.
- Multi-image viewpoint reconciliation with confidence per part.
- Interactive plan review in the panel before mutation.
- Product-specific libraries for chairs, sofas, tables, lamps, fixtures, and
  appliances.
