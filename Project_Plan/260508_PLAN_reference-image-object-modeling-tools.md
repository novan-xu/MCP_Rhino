# Reference Image Object Modeling Tools Plan

## Background

MCP_Rhino already has useful low-level and architectural geometry tools: points, lines, arcs, circles, ellipses, polylines, NURBS curves, spheres, cones, cylinders, surfaces, boxes, extrusions, slabs, walls, columns, beams, booleans, pipes, sweeps, lofts, offsets, projections, transforms, replacements, and object edits.

For reference-image object modeling, the missing tool layer is not one broad "model from image" command. The missing tools are atomic operations that let an agent turn abstract primitive decomposition into a recognizable artifact: rounded volumes, bevels, shape deformation, detail features, material creation/assignment, texture/decal support, and reference-image placement.

This plan intentionally excludes the visual QA capture/comparison tool. Visual QA is planned separately in `260508_PLAN_reference-image-visual-qa.md`.

## Goal

Add missing atomic tools needed by a general `ReferenceImageObjectModelingAgent` and its skills.

Tool groups:

- soft and rounded primitive creation
- primitive assembly support
- shape refinement and edge treatment
- additive and subtractive detail support
- material and texture support
- reference image placement and metadata support

Non-goals:

- Visual QA capture/comparison
- Agent or skill implementation
- Arbitrary Rhino command execution
- Disk `.3dm` reads
- Native BIM semantics

## Architecture Ownership

- `Tools/Geometry`
  - General object primitive creation and shape mutation tools.

- `Tools/Geometry/Edit`
  - Preview/apply shape refinement tools that mutate existing geometry.

- `Tools/Geometry/CurveOps`
  - Curve-derived detail construction when based on curves, rails, offsets, sweeps, pipes, or lofts.

- `Tools/Editing`
  - Object-level material assignment or metadata edits if they fit existing object edit patterns.

- `Tools/File` or `Tools/Materials`
  - A new `Tools/Materials` folder may be justified if multiple material tools are added.

- `Application/Services`
  - Validation, operation planning, and live adapter orchestration.

- `Application/Interfaces`
  - Live material, texture, and geometry refinement interfaces.

- `Infrastructure/Rhino/Live`
  - RhinoCommon implementation details.

- `Contracts/Requests` and `Contracts/Responses`
  - Typed DTOs for all tools.

All Rhino document truth and mutation must remain live-only.

## Key Design

### 1. Avoid a monolithic create-object tool

Do not add a single untyped tool that accepts arbitrary primitive dictionaries. The existing repository favors typed tool schemas. The agent can assemble many typed calls from a structured plan.

If batching across primitive kinds becomes necessary, add a typed `PreviewPrimitiveAssembly` and `ApplyPrimitiveAssembly` pair only after narrow tools exist and repeated runtime use proves the need.

### 2. Soft and rounded primitives

Reference objects often use softened forms that boxes/cylinders alone cannot represent.

Candidate tools:

- `CreateRoundedBoxesTool`
- `CreateEllipsoidsTool`
- `CreateCapsulesTool`
- `CreateToriTool`

Expected uses:

- rounded boxes for cushions, appliances, casings, handles, pads, consumer products
- ellipsoids for organic volumes and softened domes
- capsules for rods, handles, soft rolls, pill-shaped parts
- tori for rings, wheels, gaskets, loops, rims

Creation-only tools should be apply-only unless they replace or delete existing objects.

### 3. Edge and surface refinement

Initial primitives need controlled refinement after massing QA.

Candidate preview/apply pairs:

- `PreviewBevelOrFilletEdgesTool`
- `ApplyBevelOrFilletEdgesTool`
- `PreviewOffsetOrThickenSurfacesTool`
- `ApplyOffsetOrThickenSurfacesTool`
- `PreviewTaperOrScaleSubObjectsTool`
- `ApplyTaperOrScaleSubObjectsTool`

These are mutation tools and need preview/apply where object replacement is involved.

The first execution should choose the smallest viable subset. `CreateRoundedBoxesTool` may be safer than a general fillet-edge mutator if RhinoCommon edge filleting proves fragile.

### 4. Additive detail geometry

The agent needs detail tools that are more specific than raw lines and boxes but still general.

Candidate tools:

- `CreateRaisedStripsTool`
- `CreateGrooveCurvesOrCutsTool`
- `CreateRepeatedDetailInstancesTool`
- `CreateLabelOrDecalPlanesTool`

Expected uses:

- seams, trim, ribs, bezels, raised borders
- grooves and panel gaps
- repeated vents, buttons, feet, screws, studs
- simple image or color decals as planes

Texture-like details should not default to geometry. The material plan should decide whether to use material/texture instead.

### 5. Subtractive detail support

Current boolean tools are architecture-oriented but may be reusable. Object modeling needs a general detail-oriented cutter workflow.

Candidate tools:

- `PreviewDetailCutoutsTool`
- `ApplyDetailCutoutsTool`

Inputs should support simple cutter primitives:

- box
- cylinder
- sphere or ellipsoid
- profile extrusion

These tools should preserve source metadata where possible and clearly report boolean failures.

### 6. Material and texture tools

The sofa result had no Rhino materials, only layer colors and geometry strips. Reference-image modeling needs explicit material tools.

Candidate tools:

- `CreateRenderMaterialsTool`
- `ApplyObjectMaterialsTool`
- `UpdateRenderMaterialPropertiesTool`
- `CreateTextureFromReferenceImageSampleTool`
- `ApplyTextureMappingTool`

First execution can be staged:

1. color/roughness material creation and assignment
2. texture image assignment
3. mapping controls

The server should not perform full LLM image analysis internally. It can accept structured material parameters from the skill/agent and create Rhino materials.

### 7. Reference image placement

For manual review and future QA, it is useful to place the source image in Rhino as a reference plane.

Candidate tool:

- `PlaceReferenceImagePlaneTool`

This tool may read a user-supplied external image file path because the user explicitly provides it as runtime input. It must not scan directories or infer unrelated files.

### 8. Metadata support

Every object created through this workflow should carry enough metadata for filtering and later QA:

- `mcp.capability=reference-image-object-modeling`
- `mcp.modeling.stage=massing|detail|material|qa`
- `mcp.source.image=<stable user supplied path or label>`
- `mcp.part.name=<part>`
- `mcp.primitive.kind=<kind>`
- `mcp.object.role=primary|detail|cutter|reference|material-carrier`

Existing object user text tools may be enough, but creation/refinement tools should write core metadata directly when they create objects.

## Involved Files

Likely production files when executed:

- `src/MCP_Rhino.Server/Tools/Geometry/CreateRoundedBoxesTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/CreateEllipsoidsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/CreateCapsulesTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/CreateToriTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Edit/PreviewBevelOrFilletEdgesTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Edit/ApplyBevelOrFilletEdgesTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Edit/PreviewOffsetOrThickenSurfacesTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Edit/ApplyOffsetOrThickenSurfacesTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/CurveOps/CreateRaisedStripsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/CurveOps/PreviewDetailCutoutsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/CurveOps/ApplyDetailCutoutsTool.cs`
- `src/MCP_Rhino.Server/Tools/Materials/CreateRenderMaterialsTool.cs`
- `src/MCP_Rhino.Server/Tools/Materials/ApplyObjectMaterialsTool.cs`
- `src/MCP_Rhino.Server/Tools/Materials/ApplyTextureMappingTool.cs`
- `src/MCP_Rhino.Server/Tools/Viewport/PlaceReferenceImagePlaneTool.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoObjectPrimitiveService.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoObjectDetailService.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoMaterialService.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveObjectPrimitiveBuilder.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveObjectDetailOperator.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveRhinoMaterialOperator.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveObjectPrimitiveBuilder.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveObjectDetailOperator.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoMaterialOperator.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/*`
- `src/MCP_Rhino.Server/Contracts/Responses/*`
- `src/MCP_Rhino.Server/Domain/Models/ReferenceImageObjectPrimitiveSpecs.cs`
- `src/MCP_Rhino.Server/Domain/Enums/ReferenceImageObjectToolEnums.cs`
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`

Required test artifacts if executed:

- `Project_Test/260508_TEST_reference-image-object-modeling-tools/`

Required safety update:

- `Project_Test/260506_TEST_mcp-tool-safety-annotations/DeveloperCommandHandler.McpToolSafetyAnnotationsSmokeTest.cs`

## Usage

The agent and skills use these tools after decomposition:

1. create initial rough primitives
2. run visual QA from the separate visual QA plan
3. refine edges and details only where QA or brief says they matter
4. create and apply materials
5. maintain metadata for all created objects

Direct runtime use is also expected for users asking for specific primitive/detail operations.

## Acceptance Criteria

- New tools are typed and live-only.
- Creation tools add geometry without deleting or replacing existing objects.
- Replacement/detail mutation tools use preview/apply pairs.
- Material tools create actual Rhino render materials, not only layer colors.
- Reference image placement only reads user-supplied image paths.
- Created/refined objects include modeling metadata.
- Visual QA tools are not implemented in this plan.
- Safety annotation smoke is updated for any new tool.
- `dotnet build .\MCP_Rhino.sln -c Debug` passes.
- `dotnet build .\MCP_Rhino.sln -c Release` passes.
- Live smoke creates representative rounded, additive-detail, subtractive-detail, and material-assigned objects.

## Risks And Rollback

- Risk: RhinoCommon fillet/bevel APIs may be fragile on generated Breps.
  - Mitigation: prioritize rounded primitive creation before general edge filleting.
- Risk: too many tools may clutter MCP routing.
  - Mitigation: stage implementation and rely on clear descriptions and runtime logs before adding every candidate.
- Risk: texture support may vary by Rhino material pipeline.
  - Mitigation: ship color/roughness assignment first, then texture mapping.

Rollback removes new tool classes, services, live adapters, DTOs, DI registration, safety smoke entries, and test hooks. Existing general and architectural primitives remain intact.

## Future Extension

- Mesh/SubD approximation tools for highly organic objects.
- Library-based reusable detail parts.
- Procedural texture generation as a separate external connector, if approved.
- Tool consolidation only after runtime logs show repeated long tool chains.
