# SubD Modeling Tools PLAN

## Background

The accurate product-modeling plan focuses on scale, topology, orientation,
profiles, taper, and agent process. After that foundation exists, SubD can
improve soft product forms such as cushions, pillows, rounded upholstery,
inflated pads, molded shells, and organic product housings.

SubD should be introduced as a focused geometry capability, not as a fallback for
poorly understood reference images. It should be used when the desired shape is
smooth, continuous, and difficult to express as simple Breps, lofts, or rounded
boxes.

## Goals

- Add bounded SubD creation tools for soft product and furniture forms.
- Keep SubD operations live-only and Undo-safe.
- Add preview/inspection support so callers can understand SubD cage topology
  before mutation.
- Allow the reference-image modeling agent to choose SubD only when the brief
  asks for soft organic geometry and supplies enough control structure.
- Preserve material/shadow rules: fabric, grain, highlights, and shadows are not
  geometry.

## Non-Goals

- Do not replace Brep/profile/taper tools with SubD.
- Do not use SubD for hard planar wood frames, dimension labels, room context, or
  shadows.
- Do not add arbitrary free-form mesh sculpting without validation.
- Do not require SubD for the first accurate product-modeling milestone.

## Architecture Ownership

- `Tools/Geometry/SubD` or `Tools/Geometry`: SubD MCP tools, depending on final
  number of tools. If there are multiple SubD tools, add a stable `SubD`
  subfolder under `Tools/Geometry`.
- `Contracts/Requests` and `Contracts/Responses`: SubD cage, face, edge,
  smoothing, crease, and conversion DTOs.
- `Domain/Models` and `Domain/Enums`: SubD specifications independent of
  RhinoCommon.
- `Application/Services`: SubD validation and orchestration.
- `Application/Interfaces`: live SubD operator abstraction.
- `Infrastructure/Rhino/Live`: RhinoCommon SubD implementation.
- `Agents/Modeling`: optional routing from reference-image product plans to SubD
  creation strategies.
- `Project_Test`: API probe, tool-surface smoke, and live fallback smoke.

## Key Design

### 1. Start With An API Probe

Execution should begin by probing the installed Rhino 8 RhinoCommon SubD API in
the local build environment. The plan should not assume exact constructor or
method names until verified.

The probe should answer:

- how to create a SubD from vertices/faces
- how to create SubD boxes or control cages if RhinoCommon provides helpers
- how to set creases/sharp edges
- how to add SubD objects to `RhinoDoc`
- whether conversion to Brep/Mesh is needed for downstream inspection
- what methods are available under the current referenced RhinoCommon assembly

### 2. Atomic SubD Tools

Add the initial tools as a small safe slice:

- `PreviewSubDCage`
  - Read-only validation of vertices, faces, non-manifold edges, duplicate
    vertices, face winding, and expected bounding box.

- `CreateSubDCage`
  - Create a SubD from explicit vertices and face indices.
  - Use for controlled organic forms where the caller owns topology.

- `CreateSubDBox`
  - Create a SubD box-like control cage with subdivisions and optional rounded
    proportions.
  - Use for soft rectangular objects such as cushions and pillows.

- `CreateSubDCushions`
  - Higher-level cushion tool with width/depth/height, crown, edge softness,
    side bulge, front roll, back tilt, and subdivision settings.

- `InspectSubDObjects`
  - Read object id, SubD type, vertex/face/edge counts, bounding box, crease
    counts, and material assignment.

Optional after first slice:

- `ApplySubDCreases`
- `ConvertSubDToBrep`
- `ConvertSubDToMesh`
- `ReplaceSubDCage`

### 3. Request Model

Use DTOs that are explicit enough for safe model generation:

- `SubDVertexRequest`: x, y, z
- `SubDFaceRequest`: vertex indices
- `SubDCreaseRequest`: edge vertex pair or edge index
- `SubDBoxRequest`: center, dimensions, local frame, subdivisions, softness
- `SubDCushionRequest`: dimensions, local frame, crown, bulge, edge compression,
  seam-safe edge flags

Do not accept unbounded raw mesh payload sizes. Add maximum vertex/face counts
and return clear validation failures.

### 4. Agent Integration

The reference-image agent may choose SubD only when:

- the part is marked soft/organic/upholstered
- the brief supplies dimensions and orientation
- the part's identity depends on continuous curvature or bulge
- a Brep/profile/taper strategy would be visibly wrong

The agent should not choose SubD for:

- wooden frames
- metal rods
- planar panels
- dimension labels
- fabric weave or wood grain
- shadows/highlights/rug lines

The product geometry preview should show `Strategy = SubD` before any SubD object
is created.

## Involved Files

Likely additions:

- `src/MCP_Rhino.Server/Domain/Enums/SubDModelingEnums.cs`
- `src/MCP_Rhino.Server/Domain/Models/SubDModelingSpecs.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/SubDModelingRequests.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/SubDModelingResponses.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveSubDModelingOperator.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoSubDModelingService.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveSubDModelingOperator.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/SubD/PreviewSubDCageTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/SubD/CreateSubDCageTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/SubD/CreateSubDBoxTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/SubD/CreateSubDCushionsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/SubD/InspectSubDObjectsTool.cs`
- `Project_Test/260509_TEST_subd-modeling-tools/`

Likely modifications:

- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`
- `src/MCP_Rhino.Server/Agents/Modeling/ReferenceImageObjectModelingAgent.cs`
- `src/MCP_Rhino.Server/Application/Services/ReferenceImageProductGeometryPlanningService.cs`
- `src/MCP_Rhino.Server/Domain/Enums/ReferenceImageObjectModelingEnums.cs`
- `Project_Guides/MCP_Rhino Architecture.md` if a `Tools/Geometry/SubD` family
  needs explicit ownership text.

## Usage

Direct tool usage:

1. Call `PreviewSubDCage` for explicit cage data, or use `CreateSubDBox` /
   `CreateSubDCushions` for higher-level forms.
2. If preview passes, call the create tool.
3. Assign materials and texture mappings through existing material tools.
4. Inspect SubD object counts and bounding boxes.

Agent usage:

1. Reference-image product plan identifies a soft cushion or pillow.
2. Geometry preview reports the part will use SubD.
3. Execution creates the SubD object.
4. Materials and texture mapping remain separate.

## Acceptance Criteria

- SubD tool methods are exposed through MCP with explicit safety annotations.
- `PreviewSubDCage` rejects invalid topology without mutating Rhino.
- `CreateSubDCage` creates a valid SubD object from bounded explicit topology.
- `CreateSubDBox` creates an oriented soft box-like SubD object.
- `CreateSubDCushions` creates a visibly crowned/bulged cushion form from
  dimensions and local frame.
- `InspectSubDObjects` can report SubD object metadata.
- Agent integration can select SubD for an upholstered cushion only after the
  accurate product geometry plan supplies dimensions and orientation.
- Debug and Release plugin builds pass.
- MCP surface governance and safety annotation smokes pass.

## Risks And Rollback

- RhinoCommon SubD API shape may differ from expectation. Mitigation: begin
  execution with an API probe and keep the first implementation slice small.
- SubD topology can become invalid or non-manifold. Mitigation: preview validates
  face indices, duplicate vertices, and edge usage before mutation.
- SubD objects may not support all existing inspection or material pathways.
  Mitigation: inspect object type support and assign materials through object
  attributes where possible.
- Conversion to Brep may produce heavy geometry. Mitigation: keep conversion as
  a separate optional tool with explicit destructive/performance warnings.
- Rollback removes the SubD DTOs, service, live operator, tools, DI registration,
  test folder, and agent routing branch. Product modeling Brep/profile tools
  remain intact.

## Follow-Up Extensions

- SubD crease editing and soft/hard edge control.
- SubD cage replacement and localized vertex edits.
- SubD-to-Brep conversion for downstream boolean workflows.
- Higher-level upholstered furniture generators: seat cushion, back cushion,
  bolster, pillow, mattress, and rounded appliance casing.
- Visual QA metrics for silhouette and curvature after SubD creation.
