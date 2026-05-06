# Background

The current MCP_Rhino modeling surface can create points, lines, arcs, and simple surfaces, then transform, replace, delete, or edit selected geometry in a live Rhino document. That is enough for low-level drafting and surface editing, but it is not enough to reliably build a recognizable building model from a reference image or from a known building precedent such as Villa Savoye.

Rhino itself does not expose dedicated architectural object types such as slab, wall, column, or beam. These capabilities should therefore be modeled as MCP-level preset constructors that create ordinary Rhino geometry, mainly `Brep`, `Extrusion`, curve, and block instance objects, and attach architectural intent through names, layers, colors, and object user text.

Local RhinoCommon API investigation against the installed Rhino 8 XML surface confirmed the main construction paths needed for this capability:

- `Rhino.Geometry.Extrusion.Create(...)`
- `Rhino.Geometry.Extrusion.CreateBoxExtrusion(...)`
- `Rhino.Geometry.Extrusion.CreateCylinderExtrusion(...)`
- `Rhino.Geometry.Extrusion.CreatePipeExtrusion(...)`
- `Rhino.Geometry.Brep.CreateFromBox(...)`
- `Rhino.Geometry.Brep.CreatePlanarBreps(...)`
- `Rhino.Geometry.Brep.CreateBooleanUnion(...)`
- `Rhino.Geometry.Brep.CreateBooleanDifference(...)`
- `Rhino.Geometry.Brep.CreateBooleanIntersection(...)`
- `Rhino.Geometry.Brep.CreatePipe(...)`
- `Rhino.Geometry.Brep.CreateFromSweep(...)`
- `Rhino.Geometry.Brep.CreateFromLoft(...)`
- `Rhino.DocObjects.Tables.ObjectTable.Add(Rhino.Geometry.GeometryBase, ObjectAttributes)`
- `Rhino.DocObjects.Tables.ObjectTable.AddExtrusion(...)`
- `Rhino.DocObjects.Tables.ObjectTable.AddBrep(...)`
- `Rhino.DocObjects.Tables.ObjectTable.AddInstanceObject(...)`
- `Rhino.DocObjects.Tables.InstanceDefinitionTable.Find(...)`
- `Rhino.DocObjects.Tables.InstanceDefinitionTable.Add(...)`

The first construction target should be architectural modeling primitives and a fixed massing workflow. A later image/reference-building agent can then use these primitives instead of chaining dozens of fragile low-level geometry calls.

# Goal

Add a live-only `architectural-modeling-primitives` capability that can:

- Create box and extrusion-based solids for building massing.
- Create planar Breps from closed footprints.
- Create architectural preset geometry for slabs, walls, columns, beams, roof plates, and rectangular/circular openings.
- Perform boolean union, difference, and intersection on supported Brep or extrusion-derived targets.
- Create and insert simple reusable block definitions for repeated facade modules, columns, windows, or pilotis.
- Apply consistent layer, color, name, and object user-text metadata so ordinary Rhino objects can carry architectural intent.
- Add one fixed `BuildingMassingSkill` that composes levels, grids, slabs, columns, walls, openings, and roof/massing volumes from a structured request.

Non-goals for this first capability:

- Full image interpretation or computer vision inside the MCP server.
- A goal-driven famous-building modeling agent.
- Native BIM semantics, IFC property sets, Revit-style object families, or parametric dependency graphs.
- Advanced freeform facade reconstruction, mesh generation, SubD modeling, or material/render pipelines.
- Reference-image placement unless a stable RhinoCommon path is verified during implementation; this remains a follow-up spike.

# Architecture Ownership

- `Tools/Geometry/Architecture/`
  - Expose atomic MCP tools with thin parameter handling only.
  - Tool classes must end in `Tool`.
  - Tools call `ArchitecturalPrimitiveCreationSkill`, `ArchitecturalBooleanSkill`, or application services.

- `Skills/Modeling/`
  - `ArchitecturalPrimitiveCreationSkill`: maps preset requests into domain specs and delegates to services.
  - `ArchitecturalBooleanSkill`: fixed preview/apply wrapper around boolean operations.
  - `BuildingMassingSkill`: fixed workflow for level/grid/massing construction from a structured request.

- `Application/Services/`
  - `RhinoArchitecturalPrimitiveService`: validates specs, creates geometry, applies attributes, and returns object ids.
  - `RhinoArchitecturalBooleanService`: resolves targets, previews/apply boolean operations, preserves metadata where possible.
  - `RhinoBlockDefinitionService`: creates local block definitions and inserts block instances.
  - These services own workflow behavior but do not call RhinoCommon directly.

- `Application/Interfaces/`
  - `ILiveArchitecturalGeometryBuilder`
  - `ILiveBooleanOperator`
  - `ILiveBlockDefinitionOperator`
  - Interfaces isolate RhinoCommon calls behind live adapters.

- `Infrastructure/Rhino/Live/`
  - `LiveArchitecturalGeometryBuilder`
  - `LiveBooleanOperator`
  - `LiveBlockDefinitionOperator`
  - All `RhinoDoc`, `Rhino.Geometry`, object-table, instance-definition, and boolean API calls live here.

- `Domain/Models/`
  - Pure data specs for boxes, extrusions, slabs, walls, columns, beams, openings, levels, grids, blocks, and common architectural attributes.

- `Domain/Enums/`
  - Geometry/preset enums such as `ArchitecturalPrimitiveKind`, `ProfileShapeKind`, `OpeningKind`, `BooleanOperationKind`, `WallAlignmentKind`, and `BlockSourceKind`.

- `Contracts/Requests` and `Contracts/Responses`
  - MCP-facing DTOs only; no RhinoCommon or workflow logic.

- `Server/DependencyInjection.cs`
  - Register new services and live adapters.

- `Server/AgentRegistration.cs`
  - Register new skills.

- `ToolRegistration.cs`
  - No direct changes expected; reflection registration should discover new tools.

All paths must remain live-only. No disk `.3dm` read/write fallback is allowed. Mutation tools must use `ILiveRhinoDocumentAccessor.ExecuteWithUndo`, refresh Rhino views, and produce one Rhino Undo record per apply call.

# Key Design

## 1. Generic solids first, presets second

The implementation should add generic geometry tools before architectural presets:

- `CreateBoxesTool`
- `CreateExtrusionsTool`
- `CreatePlanarBrepsTool`
- `PreviewBooleanObjectsTool`
- `ApplyBooleanObjectsTool`
- `CreateBlockDefinitionsTool`
- `InsertBlockInstancesTool`

Architectural preset tools should build on the same internal specs:

- `CreateSlabsTool`
- `CreateWallsTool`
- `CreateColumnsTool`
- `CreateBeamsTool`
- `CreateOpeningsTool`

This avoids locking the server into fake BIM object types. A slab is an extrusion or Brep plus architectural metadata; a column is a cylinder, box extrusion, or custom profile extrusion plus metadata.

Implementation should be staged inside this capability:

1. **Foundation slice**: `CreateBoxesTool`, `CreateExtrusionsTool`, `CreatePlanarBrepsTool`, `PreviewBooleanObjectsTool`, `ApplyBooleanObjectsTool`, plus shared specs, validation, live builders, and smoke coverage.
2. **Architectural preset slice**: slabs, walls, columns, beams, and openings, implemented on top of the foundation specs instead of creating a second geometry path.
3. **Reuse/workflow slice**: local block definitions, block instances, and `BuildingMassingSkill`.

Do not start the next slice until the previous slice has build and live smoke coverage. If implementation time needs to be constrained, the foundation slice is the minimum useful deliverable; the EXET document must clearly record any deferred slices.

## 2. Architectural metadata is explicit

Every preset request should accept common architectural metadata:

- `Category`
- `LevelName`
- `SystemName`
- `SourceTag`

Do not duplicate the already existing `GeometryCreationCommonOptions` contract for `LayerFullPath`, `Color`, `Name`, and generic `UserText`. New request contracts should reuse that shape where possible and add an `ArchitecturalMetadataRequest` only for architectural intent fields such as category, level, system, and source tag.

The service should write canonical user text keys, for example:

- `mcp.category=slab`
- `mcp.level=Level 02`
- `mcp.source=reference-image`
- `mcp.capability=architectural-modeling-primitives`

This keeps filtering, export, and future agents reliable without introducing unsupported Rhino object types.

## 3. Geometry representation choices

Default representation:

- Rectangular boxes and slabs: `Brep.CreateFromBox(...)` or `Extrusion.CreateBoxExtrusion(...)`, selected by a `Representation` option with default `Brep`.
- Vertical columns: `Extrusion.CreateCylinderExtrusion(...)` for circular columns; box/extrusion for rectangular columns.
- Beams: oriented box Breps or extrusions along a baseline with explicit profile width/depth and optional up-vector/profile rotation.
- Walls: closed-profile extrusion from a planar baseline plus thickness/height/alignment; implementation may create a planar footprint curve then use `Extrusion.Create(...)`.
- Openings: boolean cutters represented as box Breps, then subtracted from wall/slab targets through `Brep.CreateBooleanDifference(...)`.
- Planar footprints: `Brep.CreatePlanarBreps(...)` from closed planar curve loops.

The first version should prioritize orthogonal architectural massing and simple planar/cylindrical forms. Slab footprints should default to WorldXY + elevation; non-horizontal custom planes can be a later extension unless implementation cost is low. Wall baselines should be planar and non-self-intersecting. Sweep/loft/pipe APIs are confirmed as available but should be held for a second phase unless a simple, well-scoped preset needs them.

## 4. Boolean operations need preview and apply

Boolean operations are risky and can fail on tolerances, non-solids, open Breps, bad intersections, or mixed object types. Provide both:

- `PreviewBooleanObjectsTool`
- `ApplyBooleanObjectsTool`

Preview should:

- Resolve target and cutter object ids.
- Convert `Extrusion` to Brep where needed.
- Validate supported types.
- Run the boolean operation in memory.
- Return output count, bounding boxes, warnings, and failure reasons without changing the document.

Apply should:

- Use the same live document state as preview.
- Compute and validate all result geometry before deleting, replacing, or adding document objects.
- Replace/delete source objects according to request policy.
- Add resulting Breps with preserved or explicit attributes.
- Use one Undo record.

Default policy:

- Boolean difference keeps target metadata on result objects.
- Cutters are deleted only when `DeleteCutters=true`.
- Failed boolean entries do not partially mutate the document.

## 5. Blocks are local reusable model components

Blocks should be a local document capability, not linked block management. Existing linked-block refresh tools already handle reference updates separately.

Add:

- `CreateBlockDefinitionsTool`
- `InsertBlockInstancesTool`

Initial block definition scope:

- Create from existing object ids only.
- Insert by definition name with transform.
- Optionally hide/delete source objects after definition creation.

Do not make block definition creation accept every primitive spec in v1. That would duplicate the primitive constructors and make the schema harder to stabilize. Callers that need primitive-based blocks should first create the source geometry with the primitive tools, then create a block definition from those object ids.

This gives repeated columns, window modules, facade panels, or furniture placeholders a compact representation that a future modeling agent can reuse.

## 6. Fixed massing skill

`BuildingMassingSkill` should accept a structured request rather than natural-language or image input. Example data:

- Levels with elevation and height.
- Grid axes.
- Slab footprints.
- Column grid locations.
- Wall baseline segments.
- Opening specs.
- Roof or massing volumes.

The skill runs a deterministic sequence:

1. Ensure required layers exist.
2. Create levels and optional grid guide geometry.
3. Create slabs.
4. Create columns and beams.
5. Create walls and roof/massing solids.
6. Apply opening boolean differences.
7. Tag all created objects.
8. Return grouped object ids by category and level.

This skill is the first reliable building workflow. A later `ReferenceBuildingModelingAgent` can generate this structured request from an image or famous-building brief.

Layer behavior should be different for atomic tools and the massing skill:

- Atomic creation tools should reject missing layers by default, matching the existing geometry creation behavior.
- Atomic tools may expose an explicit `AutoCreateLayers` option only if validation and response warnings make that behavior clear.
- `BuildingMassingSkill` may create its required layer set up front because it owns the fixed workflow and can report created layer paths.

## 7. Reference image handling is deferred unless API is verified

The current confirmed API investigation did not establish a stable picture/reference-image creation path. Do not block the primitive capability on this.

For image-based modeling v1, the external chat model can interpret the image and produce a structured massing request. A later spike can evaluate one of:

- RhinoCommon picture frame APIs if available and stable.
- Render material texture on a plane.
- A controlled `RhinoApp.RunScript("_Picture ...")` path, only if command macros can be made non-interactive and smoke-tested.

If implemented later, it should be a separate `PlaceReferenceImageTool` or a separate `reference-image-placement` capability.

# Proposed Public Tools

## Generic Primitive Tools

- `CreateBoxesTool`
  - Inputs: box items with origin/corners or plane + size, common attributes.
  - Output: created object ids and warnings.

- `CreateExtrusionsTool`
  - Inputs: profile points/closed polyline or object id profile, vector/height/path, cap flag, common attributes.
  - Output: extrusion/Brep object ids.

- `CreatePlanarBrepsTool`
  - Inputs: closed planar loops by point list or curve object ids, tolerance, common attributes.
  - Output: created Brep ids.

## Architectural Preset Tools

- `CreateSlabsTool`
  - Inputs: footprint, elevation, thickness, representation, common architectural attributes.

- `CreateWallsTool`
  - Inputs: baseline points or curve ids, height, thickness, alignment, base elevation, common architectural attributes.

- `CreateColumnsTool`
  - Inputs: center points, profile shape, radius/width/depth, base elevation, height, common architectural attributes.

- `CreateBeamsTool`
  - Inputs: baseline segments, profile shape, width/depth or radius, alignment, common architectural attributes.

- `CreateOpeningsTool`
  - Inputs: target object ids, rectangular/circular opening specs, delete cutters flag, common attributes for optional cutter retention.

## Boolean And Block Tools

- `PreviewBooleanObjectsTool`
- `ApplyBooleanObjectsTool`
- `CreateBlockDefinitionsTool`
- `InsertBlockInstancesTool`

## Workflow Tool

- `CreateBuildingMassingTool`
  - MCP-facing wrapper over `BuildingMassingSkill`.
  - Use this when the user or future agent already has a structured building massing specification.

# Request And Response Contracts

Likely new request DTOs:

- `CreateBoxesRequest`
- `CreateExtrusionsRequest`
- `CreatePlanarBrepsRequest`
- `CreateSlabsRequest`
- `CreateWallsRequest`
- `CreateColumnsRequest`
- `CreateBeamsRequest`
- `CreateOpeningsRequest`
- `PreviewBooleanObjectsRequest`
- `ApplyBooleanObjectsRequest`
- `CreateBlockDefinitionsRequest`
- `InsertBlockInstancesRequest`
- `CreateBuildingMassingRequest`

Likely shared request DTOs:

- `ArchitecturalMetadataRequest`
- `BoxItemRequest`
- `ExtrusionItemRequest`
- `PlanarLoopRequest`
- `SlabItemRequest`
- `WallItemRequest`
- `ColumnItemRequest`
- `BeamItemRequest`
- `OpeningItemRequest`
- `BooleanOperationEntryRequest`
- `BlockDefinitionItemRequest`
- `BlockInstanceItemRequest`
- `BuildingLevelRequest`
- `BuildingGridAxisRequest`

Likely response DTOs:

- `ArchitecturalCreationResponse`
- `ArchitecturalCreatedObjectResponse`
- `ArchitecturalBooleanPreviewResponse`
- `ArchitecturalBooleanApplyResponse`
- `BlockDefinitionMutationResponse`
- `BlockInstanceCreationResponse`
- `BuildingMassingResponse`

Responses should include:

- `FilePath`
- requested / created / failed counts
- created object ids grouped by category
- layer full paths
- warnings
- per-item result details
- bounding box summaries where useful

`ArchitecturalCommonOptionsRequest` should be introduced only if `GeometryCreationCommonOptions` cannot be reused cleanly. The preferred contract shape is:

- existing `GeometryCreationCommonOptions` for layer/color/name/generic user text
- new `ArchitecturalMetadataRequest` for architectural intent fields
- domain-level `ArchitecturalObjectAttributesSpec` created in the skill/service layer

# Involved Files

New files:

- `src/MCP_Rhino.Server/Tools/Geometry/Architecture/CreateBoxesTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Architecture/CreateExtrusionsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Architecture/CreatePlanarBrepsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Architecture/CreateSlabsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Architecture/CreateWallsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Architecture/CreateColumnsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Architecture/CreateBeamsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Architecture/CreateOpeningsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Architecture/PreviewBooleanObjectsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Architecture/ApplyBooleanObjectsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Architecture/CreateBlockDefinitionsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Architecture/InsertBlockInstancesTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Architecture/CreateBuildingMassingTool.cs`
- `src/MCP_Rhino.Server/Skills/Modeling/ArchitecturalPrimitiveCreationSkill.cs`
- `src/MCP_Rhino.Server/Skills/Modeling/ArchitecturalBooleanSkill.cs`
- `src/MCP_Rhino.Server/Skills/Modeling/BuildingMassingSkill.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoArchitecturalPrimitiveService.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoArchitecturalBooleanService.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoBlockDefinitionService.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveArchitecturalGeometryBuilder.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveBooleanOperator.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveBlockDefinitionOperator.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveArchitecturalGeometryBuilder.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveBooleanOperator.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveBlockDefinitionOperator.cs`
- new domain models/enums under `src/MCP_Rhino.Server/Domain/`
- new request/response DTOs under `src/MCP_Rhino.Server/Contracts/`
- `Project_Test/260506_TEST_architectural-modeling-primitives/DeveloperCommandHandler.ArchitecturalModelingPrimitivesSmokeTest.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpArchitecturalModelingPrimitivesSmokeCommand.cs`

Modified files:

- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`
- `src/MCP_Rhino.Server/Server/AgentRegistration.cs`
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs`

Expected CLI smoke slug:

- `architectural-modeling-primitives-smoke-test`

Expected Rhino command:

- `_McpArchitecturalModelingPrimitivesSmoke`

# Usage

Example primitive creation:

```json
{
  "filePath": "C:/model/site.3dm",
  "items": [
    {
      "origin": { "x": 0, "y": 0, "z": 0 },
      "sizeX": 20,
      "sizeY": 10,
      "sizeZ": 3
    }
  ],
  "common": {
    "layerFullPath": "A-MASS",
    "name": "main mass",
    "category": "massing"
  }
}
```

Example slab:

```json
{
  "filePath": "C:/model/site.3dm",
  "items": [
    {
      "footprint": [[0,0,0], [20,0,0], [20,10,0], [0,10,0]],
      "elevation": 3.0,
      "thickness": 0.3,
      "levelName": "Level 02"
    }
  ],
  "common": {
    "layerFullPath": "A-SLAB",
    "category": "slab"
  }
}
```

Example building massing workflow:

```json
{
  "filePath": "C:/model/villa-savoye-study.3dm",
  "levels": [
    { "name": "Ground", "elevation": 0.0, "height": 3.2 },
    { "name": "Main", "elevation": 3.2, "height": 3.0 },
    { "name": "Roof", "elevation": 6.2, "height": 1.2 }
  ],
  "slabs": [],
  "columns": [],
  "walls": [],
  "openings": []
}
```

A future image/reference-building agent would first convert a user image or known-building brief into this structured request, then call `CreateBuildingMassingTool`.

# Acceptance Criteria

Build:

```powershell
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj --nologo
```

CLI fallback smoke:

- `architectural-modeling-primitives-smoke-test` runs outside Rhino plugin context.
- All live-only tools return `LIVE_RHINO_REQUIRED` or the existing equivalent live-only error.
- No business mutation is attempted in CLI fallback mode.

Live smoke:

- Opens a saved active `.3dm` in Rhino.
- Creates required architectural layers.
- Creates at least one box/massing Brep.
- Creates at least one slab from a closed footprint.
- Creates rectangular and circular columns.
- Creates a wall from a baseline, height, and thickness.
- Creates a beam along a baseline.
- Applies a rectangular opening to a wall through boolean difference.
- Creates one block definition and inserts at least two instances.
- Runs the building massing workflow and returns grouped object ids by category.
- Verifies all created objects have expected names, layers, and `mcp.category` user text.
- Verifies object counts and bounding boxes are plausible.
- Verifies every apply tool produces one Undo record and Rhino `Undo` removes the created/modified objects.

Boundary tests:

- Empty item list returns a hard validation error.
- Non-planar footprint returns a hard validation error.
- Open footprint for slab/planar Brep returns a hard validation error.
- Zero or negative thickness/height returns a hard validation error.
- Boolean target id not found returns a hard validation error.
- Boolean on unsupported geometry type returns a hard validation error.
- Boolean failure returns no partial document mutation.
- Missing layer returns a clear error or auto-create behavior, depending on the final request option selected during implementation.
- Duplicate block definition name obeys the selected policy: reject, replace, or versioned name. The first implementation should reject by default.
- Block definition creation from primitive specs is rejected or omitted in v1; block definitions are created from existing object ids.
- Atomic tools reject missing target layers unless `AutoCreateLayers=true` is explicitly supplied.

# Risks And Rollback

Risks:

- Rhino boolean operations can fail depending on tolerance, solids, intersections, and geometry quality.
- Extrusion vs Brep representation affects editability, display, export, and boolean reliability.
- Wall and opening presets can become too broad if they try to cover all architectural cases in v1.
- Block definition APIs need live smoke verification for exact overloads and undo behavior.
- A large batch of architectural elements may exceed the main-thread timeout if built in one call.
- Automatic layer creation can hide user mistakes if layer names are misspelled.

Mitigations:

- Keep v1 orthogonal and explicit.
- Provide preview for booleans.
- Use document tolerance by default, with optional override.
- Return per-item failures and warnings.
- Keep one apply call as one undo record.
- Prefer explicit layer creation or a clear `AutoCreateLayers` option.
- Verify overloads in the capability-owned live smoke command before marking EXET complete.

Rollback:

- Remove new tools under `Tools/Geometry/Architecture/`.
- Remove new skills, services, interfaces, live adapters, contracts, domain models/enums, smoke command, and test folder.
- Revert DI and agent-registration changes.
- Existing geometry, analysis, layer, export, and drawing capabilities should remain untouched because this capability is additive.

# Follow-Up Extensions

- `reference-image-placement`: place a bitmap as a scaled Rhino reference plane if a stable API path is verified.
- `reference-building-modeling-agent`: goal-driven agent that interprets image/famous-building briefs and calls `CreateBuildingMassingTool`.
- `facade-module-skill`: repeated window/ribbon facade modules using block instances and boolean openings.
- `stair-ramp-primitives`: ramps, stairs, railings, and pilotis-specific helper presets.
- `roof-and-freeform-tools`: loft/sweep/pipe-based architectural forms.
- `ifc-export-metadata`: map `mcp.category` and architectural user text into export-oriented metadata conventions.
