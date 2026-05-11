# Material Texture Capability EXET

## Corresponding Plan

- Plan: `Project_Plan/260509_PLAN_material-texture-capability.md`
- Execution date: 2026-05-09

## Associated Artifacts

- Test folder: `Project_Test/260509_TEST_material-texture-capability/`
- Commit / PR: not created in this execution session

## Execution Result / Actual Scope

Implemented the material texture capability slice needed by reference-image object modeling:

- Added deterministic procedural PNG texture generation through `GenerateProceduralTextureImage`.
- Added textured Rhino material creation/update through `CreateTexturedRenderMaterials`.
- Added material texture inspection through `InspectRenderMaterialTextures`.
- Added texture mapping preview/apply tools for explicit object ids.
- Extended `RhinoMaterialService` and `ILiveRhinoMaterialOperator`.
- Implemented RhinoCommon live material texture and texture mapping operations in `LiveRhinoMaterialOperator`.
- Preserved and validated the material-vs-geometry policy for reference-image detail planning and agent execution.
- Registered `ProceduralTextureImageService` in application DI.

Implemented tools:

- `src/MCP_Rhino.Server/Tools/Materials/GenerateProceduralTextureImageTool.cs`
- `src/MCP_Rhino.Server/Tools/Materials/CreateTexturedRenderMaterialsTool.cs`
- `src/MCP_Rhino.Server/Tools/Materials/InspectRenderMaterialTexturesTool.cs`
- `src/MCP_Rhino.Server/Tools/Materials/PreviewTextureMappingTool.cs`
- `src/MCP_Rhino.Server/Tools/Materials/ApplyTextureMappingTool.cs`

Deferred:

- bump/normal/displacement texture channels
- AI-generated texture assets
- broad filter-based texture mapping; initial mapping requires explicit object ids
- live Rhino manual smoke execution in this session

## Deviations From Plan

- `UpdateRenderMaterialTextures` was not added as a separate tool. `CreateTexturedRenderMaterials` updates existing materials when `ReuseExistingByName` is true, which covers the initial update use case without adding another tool.
- Texture mapping supports explicit object ids only. Filter expansion was deferred to avoid broad mutation risk.
- The Rhino `Texture.MappingChannelId` property is read-only, so material creation records the requested mapping channel in the response while actual object mapping is controlled by `ApplyTextureMapping`.
- Live Rhino smoke was not run in this terminal session; CLI fallback smoke verifies validation and that live document mutations reach the live-Rhino boundary.

## Issues Found And Fixed During Construction

- `System.Drawing` APIs triggered CA1416 under `TreatWarningsAsErrors`; `ProceduralTextureImageService` now checks `OperatingSystem.IsWindows()` before calling a Windows-only PNG writer.
- RhinoCommon texture channel assignment on `Texture` is read-only; the implementation uses document object texture mapping channels instead.
- Existing texture-like detail suppression was too narrow; previous work in this plan now covers weave, grain, highlight, lighting, and shadow cues.

## Test Record

Debug build:

```powershell
dotnet build .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug -p:OutputPath="c:\01_Projects\MCP_Rhino\.validation\exec-debug\"
```

Result:

- exit code: 0
- 0 warnings, 0 errors

Release build:

```powershell
dotnet build .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release -p:OutputPath="c:\01_Projects\MCP_Rhino\.validation\exec-release\"
```

Result:

- exit code: 0
- 0 warnings, 0 errors

Material texture smoke:

```powershell
dotnet .\.validation\exec-debug\MCP_Rhino.Server.dll material-texture-capability-smoke-test
dotnet .\.validation\exec-release\MCP_Rhino.Server.dll material-texture-capability-smoke-test
```

Result for both Debug and Release:

- `[OK] material texture capability smoke kept material, texture, lighting, and shadow cues out of geometry.`
- `[OK] ReferenceImageObjectModelingAgent raised-strip policy rejects non-geometric visual cues.`
- `[OK] generated procedural woven texture image: C:\01_Projects\MCP_Rhino\.validation\material-texture-smoke\woven-smoke.png (2151 bytes).`
- `[OK] textured material creation and texture mapping route to live Rhino after validation.`

MCP surface governance smoke:

```powershell
dotnet .\.validation\exec-debug\MCP_Rhino.Server.dll mcp-surface-structure-governance-smoke-test
dotnet .\.validation\exec-release\MCP_Rhino.Server.dll mcp-surface-structure-governance-smoke-test
```

Result:

- exit code: 0 in both configurations
- discovered 142 MCP tools
- `Materials` family count: 7
- `Modeling` family count: 1
- all reflected MCP tool names were unique
- all MCP tools had descriptions and explicit safety metadata

## Acceptance Criteria Alignment

- Procedural woven texture generation: satisfied.
- Textured material creation/update with explicit texture paths: satisfied.
- Texture mapping preview/apply for explicit object ids: satisfied.
- Material texture inspection: satisfied.
- Material/shadow cues are not geometry: satisfied.
- MCP safety annotations explicit: satisfied by surface governance smoke.
- Debug and Release validation: satisfied by Server project builds and governance smoke.

## Rollback Validation

Rollback removes:

- new material texture tools
- new material texture DTOs/enums/specs
- `ProceduralTextureImageService`
- new `RhinoMaterialService` methods
- new `ILiveRhinoMaterialOperator` methods
- new `LiveRhinoMaterialOperator` implementations
- material texture smoke additions

Existing color/roughness material creation and assignment remain separate and can be left intact.

## Current Residual Items

- Run a live Rhino smoke through the Debug bridge or Release panel-bound pipe against a saved active document.
- Add bump/normal/displacement texture channels after one diffuse-channel workflow is validated in Rhino UI.
- Decide whether external AI texture generation should be a separate connector or a user-provided asset workflow.

## Conclusion

The material texture capability is now implemented for deterministic texture generation, diffuse bitmap material creation/update, explicit-object texture mapping, and material-vs-geometry policy enforcement. Live document mutation paths compile and are exposed as MCP tools; this session validated CLI fallback and MCP surface behavior, with live Rhino execution left as the next manual smoke.
