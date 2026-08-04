# Reference Image Object Modeling Tools EXET

## Corresponding Plan

- Plan: `Project_Plan/260508_PLAN_reference-image-object-modeling-tools.md`
- Execution date: 2026-05-08

## Associated Artifacts

- Test folder: `Project_Test/260508_TEST_reference-image-object-modeling-tools/`
- Commit / PR: not created in this execution session

## Execution Result / Actual Scope

Implemented the first staged tool slice for reference-image object modeling:

- Added soft primitive creation tools:
  - `CreateRoundedBoxes`
  - `CreateEllipsoids`
  - `CreateCapsules`
  - `CreateTori`
- Added additive detail creation:
  - `CreateRaisedStrips`
- Added material tools:
  - `CreateRenderMaterials`
  - `ApplyObjectMaterials`
- Added reference-image object-modeling metadata defaults on new soft/detail primitives:
  - `mcp.capability=reference-image-object-modeling`
  - `mcp.modeling.stage=massing|detail`
  - `mcp.primitive.kind=<kind>`
  - `mcp.object.role=primary|detail`
- Added material assignment metadata:
  - `mcp.capability=reference-image-object-modeling`
  - `mcp.modeling.stage=material`
  - `mcp.material.name=<name>`
- Added CLI smoke slug:
  - `reference-image-object-modeling-tools-smoke-test`
- Added Rhino live smoke command:
  - `_McpReferenceImageObjectModelingToolsSmoke`

## Deviations From Plan

This is a staged first execution, not the full tools plan.

Deferred:

- general edge bevel / fillet preview-apply mutators
- offset / thicken surface tools
- taper / sub-object scale tools
- general subtractive detail cutout preview-apply tools
- repeated detail instance tool
- decal planes
- texture image material creation
- texture mapping controls
- reference image plane placement

Material support was implemented as Rhino document material-table creation plus object-level material assignment. Texture and full mapping support remain future slices.

## Issues Found And Fixed During Construction

- Avoided a broad arbitrary primitive dictionary endpoint and extended the existing typed general-primitive path instead.
- Used `Brep.CreatePipe(..., PipeCapMode.Round, ...)` for capsules to avoid hand-building hemisphere joins.
- Used `Brep.CreateFilletEdges` only inside `CreateRoundedBoxes`, where the source Brep is a controlled simple box. General fillet mutation remains deferred because arbitrary existing Breps are riskier.

## Test Record

Debug server build with alternate output:

```powershell
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug -p:OutputPath=<REPO_ROOT>\.validation\server-debug\
```

Result:

- exit code: 0
- warnings: 0
- errors: 0

Debug safety smoke from alternate output:

```powershell
dotnet <REPO_ROOT>\.validation\server-debug\MCP_Rhino.Server.dll mcp-tool-safety-annotations-smoke-test
```

Result:

- `[OK] MCP safety annotations verified for 136 tools.`
- `[OK] No bare method-level [McpServerTool] attributes remain.`

Debug CLI fallback smoke from alternate output:

```powershell
dotnet <REPO_ROOT>\.validation\server-debug\MCP_Rhino.Server.dll reference-image-object-modeling-tools-smoke-test
```

Result:

- `[OK] reference-image-object-modeling-tools CLI fallback returned LIVE_RHINO_REQUIRED for live-only paths.`

Standard Debug solution build:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug
```

Result:

- exit code: 1
- cause: `src\MCP_Rhino.Server\bin\Debug\net8.0\MCP_Rhino.Server.dll` locked by `Rhino 8 (51408)`
- Bridge and Companion Debug projects built before the Server copy failed.

Release solution build:

```powershell
dotnet build .\MCP_Rhino.sln -c Release
```

Result:

- exit code: 0
- warnings: 0
- errors: 0

Release safety smoke:

```powershell
dotnet src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.dll mcp-tool-safety-annotations-smoke-test
```

Result:

- `[OK] MCP safety annotations verified for 136 tools.`
- `[OK] No bare method-level [McpServerTool] attributes remain.`

Release CLI fallback smoke:

```powershell
dotnet src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.dll reference-image-object-modeling-tools-smoke-test
```

Result:

- `[OK] reference-image-object-modeling-tools CLI fallback returned LIVE_RHINO_REQUIRED for live-only paths.`

Live Rhino smoke:

- command added: `_McpReferenceImageObjectModelingToolsSmoke`
- not executed in this terminal session
- expected behavior: creates one rounded box, one ellipsoid, one capsule, one torus, one raised strip, one material, and assigns that material to all created objects on a timestamped smoke layer

## Acceptance Alignment

- New tools are typed and live-only: satisfied for the first staged slice.
- Creation tools add geometry without deleting or replacing existing objects: satisfied.
- Material tools create Rhino document materials and assign them directly to objects: satisfied for color / roughness / transparency material-table slice.
- Created objects include modeling metadata: satisfied for the new soft/detail primitives and material assignment.
- Visual QA tools were not implemented in this plan: satisfied.
- Safety annotation smoke updated and passing: satisfied.
- Debug validation: alternate Debug server build and smokes passed; standard Debug solution output is blocked by the open Rhino lock.
- Release validation: solution build and smokes passed.
- Live smoke coverage: command added; pending user-side Rhino execution.

## Rollback Verification

Rollback would remove:

- new primitive request/spec fields and enum values
- new primitive tool classes
- new material request/response/spec/service/operator/tool classes
- DI material registrations
- DeveloperCommandHandler smoke hook
- Rhino smoke command
- TEST folder
- safety expectation entries

Existing point/curve/surface/general primitive, architectural primitive, boolean, and visual QA tools remain independent.

## Current Remaining Items

- Run `_McpReferenceImageObjectModelingToolsSmoke` inside Rhino after loading the rebuilt plugin.
- Add a later subtractive-detail slice using preview/apply general cutout tools.
- Add texture image creation and mapping controls.
- Add reference image placement as a viewport/modeling aid.

## Conclusion

The first reference-image object-modeling tools slice is implemented and compiled. It gives the future agent stable typed calls for soft massing, additive detail, and material assignment, while leaving the higher-risk cutout, texture, and generic edge-refinement tools for later executions.
