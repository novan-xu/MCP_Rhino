# General Primitive Creation Tools Plan

## Background

MCP_Rhino currently has live creation tools for points, lines, arcs, and simple surfaces, plus architectural primitives such as boxes, slabs, walls, columns, beams, planar breps, and extrusions. The reference `rhinomcp` project exposes a broader general-purpose `create_object` surface that covers point, line, polyline, circle, arc, ellipse, curve, box, sphere, cone, cylinder, and surface.

MCP_Rhino should not copy the single stringly typed `create_object(type, params)` shape. This repository already uses typed request/response DTOs, domain models, live adapters, and batch creation services. The better fit is to add typed, batch-oriented general primitive tools that extend the existing `GeometryCreationSkill` and `LiveRhinoGeometryBuilder` patterns.

## Goal

Add missing general primitive creation tools in a typed, live-only, batch-friendly form.

Target primitive coverage:

- circles
- ellipses
- polylines
- NURBS/control-point curves
- spheres
- cones
- cylinders
- optional general boxes if the architectural `CreateBoxes` tool is too metadata-heavy for generic use

## Architecture Ownership

- `Tools/Geometry`: new atomic batch creation tools.
- `Contracts/Requests`: typed item requests for each primitive family.
- `Domain/Enums`: add focused primitive enums only where request DTOs need option sets.
- `Domain/Models`: add focused primitive specs; do not extend the existing `GeometryCreationSpec` for this capability.
- `Application/Services/RhinoGeometryCreationService.cs`: coordinate validation and live creation.
- `Infrastructure/Rhino/Live/LiveRhinoGeometryBuilder.cs`: RhinoCommon geometry construction.
- `Skills/Modeling/GeometryCreationSkill.cs`: expose typed create methods if useful.

## Key Design

### 1. Keep Typed Tools

Do not introduce one broad `CreateObjectTool` with a free-form `type` string and untyped `params`. That approach is compact but weak for validation, tool descriptions, and MCP schema quality.

The first execution will use typed narrow tools:

- `CreateCirclesTool`
- `CreateEllipsesTool`
- `CreatePolylinesTool`
- `CreateNurbsCurvesTool`
- `CreateSpheresTool`
- `CreateConesTool`
- `CreateCylindersTool`

Do not merge these into `CreatePlanarCurvesTool` or `CreatePrimitiveSolidsTool` in the first implementation. A later consolidation can be planned only after usage shows MCP surface clutter is a real problem.

### 2. Batch First

Each creation tool should accept `List<TItemRequest>` and common object options, matching the existing create points/lines/arcs/surfaces pattern.

Common options:

- target layer full path
- color
- name prefix or explicit item name
- user text

### 3. Metadata And Layer Behavior

Use `GeometryCreationCommonOptions` for generic primitives. Do not require architectural metadata unless the user chooses architectural tools.

### 4. Geometry Builder Extension

Do not overload `GeometryCreationSpec`; it is already shaped around point/line/arc/surface creation. Add a focused builder path:

- `GeneralPrimitiveCreationSpecs.cs` for domain specs
- `ILiveGeneralPrimitiveBuilder`
- `LiveGeneralPrimitiveBuilder`

`RhinoGeometryCreationService` may coordinate the new builder, but construction details should stay in the live Rhino adapter.

### 5. Safety

Creation tools mutate the live document but do not replace or delete existing geometry.

Safety:

- `ReadOnly = false`
- `Destructive = false`
- `OpenWorld = false`

### 6. Scope Exclusions

This plan does not include:

- loft
- sweep
- pipe
- offset
- project
- split
- arbitrary RhinoScript/C# execution
- mesh creation

Those belong to separate capability plans.

## Involved Files

Likely production files:

- `src/MCP_Rhino.Server/Tools/Geometry/CreateCirclesTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/CreateEllipsesTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/CreatePolylinesTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/CreateNurbsCurvesTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/CreateSpheresTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/CreateConesTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/CreateCylindersTool.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/CreateCirclesRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/CreateEllipsesRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/CreatePolylinesRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/CreateNurbsCurvesRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/CreateSpheresRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/CreateConesRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/CreateCylindersRequest.cs`
- `src/MCP_Rhino.Server/Domain/Enums/GeneralPrimitiveCreationEnums.cs`
- `src/MCP_Rhino.Server/Domain/Models/GeneralPrimitiveCreationSpecs.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveGeneralPrimitiveBuilder.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoGeometryCreationService.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveGeneralPrimitiveBuilder.cs`

Required test artifacts if executed:

- `Project_Test/260507_TEST_general-primitive-creation-tools/`

Required safety update:

- `Project_Test/260506_TEST_mcp-tool-safety-annotations/DeveloperCommandHandler.McpToolSafetyAnnotationsSmokeTest.cs`

## Usage

Examples after execution:

- create circles for profiles before pipe/sweep operations
- create ellipses or polylines for layout curves
- create NURBS curves from control points for freeform paths
- create spheres/cones/cylinders as generic geometry without architectural metadata

## Acceptance Criteria

- All tools are live-only.
- All tools use typed DTOs, not free-form parameter dictionaries.
- All tools support batch creation.
- Created objects return ids, type names, layer paths, bounding boxes, and warnings.
- Invalid geometry inputs fail with clear messages before partial creation where possible.
- Object attributes preserve existing `GeometryCreationCommonOptions` behavior.
- Safety annotation smoke is updated.
- `dotnet build .\MCP_Rhino.sln -c Debug` passes.
- `dotnet build .\MCP_Rhino.sln -c Release` passes.
- `dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-tool-safety-annotations-smoke-test` passes.
- `dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-tool-safety-annotations-smoke-test` passes.
- Live smoke creates at least one object of every new primitive type and verifies count, layer, name/color where provided, and object ids.

## Risks And Rollback

- Risk: many small tools may clutter the MCP surface.
  - Mitigation: use narrow tools for schema clarity now; evaluate consolidation only with runtime usage evidence.
- Risk: new primitive specs duplicate some existing creation plumbing.
  - Mitigation: reuse `GeometryCreationCommonOptions` and response formatting while keeping RhinoCommon construction in the new live builder.
- Risk: generic and architectural box/cylinder-like tools may overlap.
  - Mitigation: descriptions must distinguish generic primitives from architectural primitives with metadata.

Rollback removes the new tools, request DTOs, focused enum/spec files, live builder additions, DI changes if any, and smoke registrations. Existing point/line/arc/surface and architectural tools remain intact.

## Future Extension

- Add mesh primitives if runtime usage demands it.
- Add preview-only bounding box validation for very large batches.
- Add a higher-level shape creation Skill only after repeated multi-tool usage appears in `Runtime_Log`.
