# General Primitive Creation Tools EXET

## Corresponding Plan

- Plan: `Project_Plan/260507_PLAN_general-primitive-creation-tools.md`
- Execution date: 2026-05-08

## Related Artifacts

- Test folder: `Project_Test/260507_TEST_general-primitive-creation-tools/`
- Commit / PR: not created in this execution pass

## Execution Result / Actual Scope

Implemented seven typed, batch-oriented general primitive creation tools under `Tools/Geometry`:

- `CreateCircles`
- `CreateEllipses`
- `CreatePolylines`
- `CreateNurbsCurves`
- `CreateSpheres`
- `CreateCones`
- `CreateCylinders`

Added the supporting live-only creation path:

- `Domain/Enums/GeneralPrimitiveCreationEnums.cs`
- `Domain/Models/GeneralPrimitiveCreationSpecs.cs`
- `Contracts/Requests/GeneralPrimitiveCreationRequests.cs`
- `Contracts/Responses/GeneralPrimitiveCreationResponses.cs`
- `Application/Interfaces/ILiveGeneralPrimitiveBuilder.cs`
- `Application/Services/RhinoGeneralPrimitiveCreationService.cs`
- `Infrastructure/Rhino/Live/LiveGeneralPrimitiveBuilder.cs`

Extended `GeometryCreationSkill` as the facade for the new typed create methods, registered the builder/service in DI, and added a dedicated Rhino smoke command:

- `_McpGeneralPrimitiveCreationToolsSmoke`
- CLI slug: `general-primitive-creation-tools-smoke-test`

The MCP safety expectation map was updated. The reflected MCP surface now contains 119 tools.

## Deviations From Plan

- The seven request DTO groups were implemented in one file, `GeneralPrimitiveCreationRequests.cs`, instead of one file per request. This follows the local pattern used by the architectural and block capability request groups.
- A dedicated `RhinoGeneralPrimitiveCreationService` was added instead of expanding `RhinoGeometryCreationService`. This keeps the existing point / line / arc / surface path stable while still routing through `GeometryCreationSkill`.
- General boxes were not added. The plan marked them optional, and the architectural `CreateBoxes` tool already covers that primitive with richer metadata.

## Issues Found And Fixed During Construction

- Confirmed the RhinoCommon APIs for circle, ellipse, polyline, NURBS curve, sphere, cone, and cylinder construction through Debug and Release builds.
- Adjusted large-batch warning aggregation so the warning appears once rather than once per item.
- Tightened polyline validation so closed polylines require at least three distinct points.

## Test Record

Debug build:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug
```

Result: passed, 0 warnings, 0 errors.

Release build:

```powershell
dotnet build .\MCP_Rhino.sln -c Release
```

Result: passed, 0 warnings, 0 errors.

Debug general primitive CLI fallback smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- general-primitive-creation-tools-smoke-test
```

Result: passed. Output confirmed all new live-only paths returned `LIVE_RHINO_REQUIRED` in CLI fallback mode.

Release general primitive CLI fallback smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- general-primitive-creation-tools-smoke-test
```

Result: passed. Output confirmed all new live-only paths returned `LIVE_RHINO_REQUIRED` in CLI fallback mode.

Debug MCP safety smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-tool-safety-annotations-smoke-test
```

Result: passed. Output confirmed safety annotations for 119 tools and no bare method-level `[McpServerTool]` attributes.

Release MCP safety smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-tool-safety-annotations-smoke-test
```

Result: passed. Output confirmed safety annotations for 119 tools and no bare method-level `[McpServerTool]` attributes.

Debug MCP surface governance smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-surface-structure-governance-smoke-test
```

Result: passed. Output discovered 119 tools; `Geometry` family count is 19.

Release MCP surface governance smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-surface-structure-governance-smoke-test
```

Result: passed. Output discovered 119 tools; `Geometry` family count is 19.

Live Rhino smoke:

```text
_McpGeneralPrimitiveCreationToolsSmoke
```

Result: not run in this CLI-only execution environment. The command is implemented and will create one object for each new primitive type on a timestamped smoke layer in a saved active Rhino document.

## Acceptance Alignment

- Live-only path: satisfied through `ILiveRhinoDocumentAccessor`; CLI fallback returns `LIVE_RHINO_REQUIRED`.
- Typed DTOs: satisfied; no free-form `type` / `params` tool was introduced.
- Batch creation: satisfied; every tool accepts `List<TItemRequest>`.
- Response contents: satisfied; each created object returns object id, primitive kind, geometry type name, layer path, and bounding box.
- Safety annotations: satisfied and covered by Debug / Release safety smoke.
- Debug / Release builds: passed.
- Live creation smoke: command implemented; manual Rhino run remains pending.

## Rollback Verification

Rollback is localized to the new general primitive DTOs, domain models/enums, builder, service, tools, DI registration, `GeometryCreationSkill` additions, safety smoke entries, smoke test folder, EXET, and the Rhino smoke command. Existing point / line / arc / surface and architectural creation paths remain separate.

## Current Remaining Items

- Run `_McpGeneralPrimitiveCreationToolsSmoke` inside Rhino with a saved active document to verify actual live object creation, layer assignment, object ids, type names, colors, and bounding boxes.

## Conclusion

The general primitive creation tool plan is implemented and CLI-validated in Debug and Release. The only remaining verification is the live Rhino smoke command, which requires an active Rhino session.
