# Curve Derived Geometry Tools Plan

## Background

The strongest geometry gaps found in the `rhinomcp` reference project are curve-derived construction tools:

- loft
- extrude existing curve
- sweep one rail
- offset curve
- pipe along curve
- project curve onto surfaces or meshes
- split curve

MCP_Rhino already has analysis tools, mass properties, intersections, contour previews, editable geometry descriptors, and geometry replacement/editing tools. It does not currently expose these common Rhino construction operations as direct atomic tools.

This plan covers curve-derived construction and segmentation tools in the MCP_Rhino style: typed DTOs, live-only execution, clear safety annotations, and preview/apply where destructive behavior is possible.

## Goal

Add direct live Rhino tools for common curve-derived geometry workflows:

- create lofts from ordered curve ids
- create extrusions from existing curve ids and vectors
- create sweep-one-rail geometry from a rail and profile curve ids
- create curve offsets
- create pipe Breps along curve ids
- project curves onto Breps/surfaces/meshes
- preview curve splitting
- create split curve segments while keeping source curves
- replace source curves with split segments when explicitly requested

## Architecture Ownership

- `Tools/Geometry/CurveOps`: proposed subfolder for curve-derived operations if accepted.
- `Contracts/Requests`: typed request DTOs for each operation.
- `Contracts/Responses`: creation and preview/apply result DTOs.
- `Domain/Enums`: loft type, offset corner style, pipe cap style, split behavior, projection target behavior.
- `Domain/Models`: operation specs and previews.
- `Application/Services`: orchestration and validation.
- `Application/Interfaces`: live curve operation interface.
- `Infrastructure/Rhino/Live`: RhinoCommon implementation.

## Key Design

### 1. Separate Creation From Destructive Editing

Apply-only creation tools:

- `CreateLoftsTool`
- `CreateCurveExtrusionsTool`
- `CreateSweepOneRailTool`
- `CreateCurveOffsetsTool`
- `CreatePipesTool`
- `ProjectCurvesTool`

Split tools:

- `PreviewSplitCurvesTool`
- `CreateSplitCurveSegmentsTool`
- `ReplaceSplitCurvesTool`

Splitting has two apply paths because method-level MCP safety cannot depend on request parameters. `CreateSplitCurveSegmentsTool` creates new segments and keeps source curves. `ReplaceSplitCurvesTool` creates segments and deletes/replaces source curves, so it is always destructive.

### 2. Typed Request Contracts

Do not use `object_ids` plus arbitrary option dictionaries. Each operation gets a typed request:

- `LoftEntryRequest`
- `CurveExtrusionEntryRequest`
- `SweepOneRailEntryRequest`
- `CurveOffsetEntryRequest`
- `PipeEntryRequest`
- `CurveProjectionEntryRequest`
- `CurveSplitEntryRequest`

All requests include `FilePath` and batch `Entries`.

### 3. Object Resolution

Inputs should use confirmed object ids, not loose name matching. The user can resolve names/layers through existing filtering tools before calling these operations.

### 4. Attribute Behavior

Creation tools should support:

- layer full path
- object name or name prefix
- color
- user text

Source object metadata should not be copied unless explicitly requested and clearly defined.

### 5. Preview Behavior

For splitting:

- preview returns resolved curve ids, split parameters, point-derived parameters, expected segment count, source deletion/replacement behavior, and warnings
- create-segments apply performs one Rhino undo record for the whole batch and keeps sources
- replace apply performs one Rhino undo record for the whole batch and deletes/replaces sources

For other tools, apply-only is acceptable because they create new geometry without deleting sources.

### 6. Safety

Creation-only tools:

- `ReadOnly = false`
- `Destructive = false`
- `OpenWorld = false`

Split preview:

- `ReadOnly = true`
- `Destructive = false`
- `OpenWorld = false`

Split create segments:

- `ReadOnly = false`
- `Destructive = false`
- `OpenWorld = false`

Split replace sources:

- `ReadOnly = false`
- `Destructive = true`
- `OpenWorld = false`

### 7. Scope Exclusions

This plan does not include arbitrary trim, extend, blend, fillet, network surface, two-rail sweep, or SubD tools. Those should be separate plans after these core operations are stable.

## Involved Files

Likely production files:

- `src/MCP_Rhino.Server/Tools/Geometry/CurveOps/CreateLoftsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/CurveOps/CreateCurveExtrusionsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/CurveOps/CreateSweepOneRailTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/CurveOps/CreateCurveOffsetsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/CurveOps/CreatePipesTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/CurveOps/ProjectCurvesTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/CurveOps/PreviewSplitCurvesTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/CurveOps/CreateSplitCurveSegmentsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/CurveOps/ReplaceSplitCurvesTool.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/CurveDerivedGeometryRequests.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/CurveDerivedGeometryResponses.cs`
- `src/MCP_Rhino.Server/Domain/Enums/CurveDerivedGeometryEnums.cs`
- `src/MCP_Rhino.Server/Domain/Models/CurveDerivedGeometrySpecs.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveCurveDerivedGeometryOperator.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoCurveDerivedGeometryService.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveCurveDerivedGeometryOperator.cs`
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`

Required test artifacts if executed:

- `Project_Test/260507_TEST_curve-derived-geometry-tools/`

Required safety update:

- `Project_Test/260506_TEST_mcp-tool-safety-annotations/DeveloperCommandHandler.McpToolSafetyAnnotationsSmokeTest.cs`

## Usage

Expected runtime examples:

- create profile circles, then `CreateLofts` through them
- create rail/profile curves, then `CreateSweepOneRail`
- create path curve, then `CreatePipes`
- offset plan curves for layout work
- project curves to facade/surface geometry
- preview curve splits before segment creation or source replacement
- create split curve segments while preserving sources when the user wants non-destructive output
- replace source curves with split segments only when the user explicitly asks for source replacement

## Acceptance Criteria

- All tools are live-only and use `ILiveRhinoDocumentAccessor`.
- Each apply call uses one Rhino undo record.
- Source object ids are resolved and validated before mutation.
- Creation tools do not delete or replace source geometry.
- `CreateSplitCurveSegmentsTool` keeps source curves and is non-destructive.
- `ReplaceSplitCurvesTool` deletes/replaces source curves and is always marked destructive.
- Batch behavior reports per-entry success/failure without hiding partial results.
- Safety smoke is updated for every new tool.
- `dotnet build .\MCP_Rhino.sln -c Debug` passes.
- `dotnet build .\MCP_Rhino.sln -c Release` passes.
- `dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-tool-safety-annotations-smoke-test` passes.
- `dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-tool-safety-annotations-smoke-test` passes.
- Live smoke creates valid examples for loft, extrusion, sweep, offset, pipe, projection, and split.

## Risks And Rollback

- Risk: RhinoCommon operations such as loft/sweep/pipe can return multiple Breps or fail based on curve compatibility.
  - Mitigation: response must report counts, warnings, and per-entry failures.
- Risk: project curve support for meshes may require extra validation.
  - Mitigation: begin with Brep/surface targets if mesh support is unstable, and record the gap.
- Risk: callers may choose destructive split replacement accidentally.
  - Mitigation: expose non-destructive segment creation as the default apply path and reserve `ReplaceSplitCurvesTool` for explicit source replacement.

Rollback removes the new curve operation tools, service/operator classes, DTOs, DI entries, and smoke registrations. Existing geometry edit and analysis tools remain unaffected.

## Future Extension

- Add two-rail sweep.
- Add curve trim/extend/fillet tools.
- Add blend curves and surface-from-network tools.
- Add a fixed workflow Skill for "make pipe from described path" if runtime logs show repeated multi-step usage.
