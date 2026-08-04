# Curve Derived Geometry Tools EXET

## Corresponding Plan

- Plan: `Project_Plan/260507_PLAN_curve-derived-geometry-tools.md`
- Execution date: 2026-05-08

## Related Artifacts

- Test folder: `Project_Test/260507_TEST_curve-derived-geometry-tools/`
- Commit / PR: not created in this execution pass

## Execution Result / Actual Scope

Implemented a new `Tools/Geometry/CurveOps` MCP tool family with nine live-only tools:

- `CreateLofts`
- `CreateCurveExtrusions`
- `CreateSweepOneRail`
- `CreateCurveOffsets`
- `CreatePipes`
- `ProjectCurves`
- `PreviewSplitCurves`
- `CreateSplitCurveSegments`
- `ReplaceSplitCurves`

Added the supporting typed contracts, domain specs/enums, application service, live operator, DI registration, smoke registration, and Rhino command:

- `Contracts/Requests/CurveDerivedGeometryRequests.cs`
- `Contracts/Responses/CurveDerivedGeometryResponses.cs`
- `Domain/Enums/CurveDerivedGeometryEnums.cs`
- `Domain/Models/CurveDerivedGeometrySpecs.cs`
- `Application/Interfaces/ILiveCurveDerivedGeometryOperator.cs`
- `Application/Services/RhinoCurveDerivedGeometryService.cs`
- `Infrastructure/Rhino/Live/LiveCurveDerivedGeometryOperator.cs`
- `_McpCurveDerivedGeometryToolsSmoke`
- CLI slug: `curve-derived-geometry-tools-smoke-test`

The MCP surface now contains 128 tools. The new `Geometry/CurveOps` family contains 9 tools.

## Deviations From Plan

- Request/response DTOs were grouped into `CurveDerivedGeometryRequests.cs` and `CurveDerivedGeometryResponses.cs`, matching the repository's grouped DTO pattern for broader capability families.
- The live operator reports per-entry failures and continues with valid entries in the same batch. This satisfies the plan's partial-result requirement while keeping one undo record per apply call.
- Projection supports Brep/surface/extrusion and mesh targets through RhinoCommon projection APIs.

## Issues Found And Fixed During Construction

- Verified RhinoCommon API usage for loft, vector extrusion, one-rail sweep, planar offset, pipe, projection, and curve split through Debug and Release builds.
- Added separate non-destructive and destructive split apply tools because method-level MCP safety annotations cannot depend on request parameters.
- Kept source curves preserved for all creation-only operations and for `CreateSplitCurveSegments`; only `ReplaceSplitCurves` deletes source curves.

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

Debug curve-derived CLI fallback smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- curve-derived-geometry-tools-smoke-test
```

Result: passed. Output confirmed all new live-only paths returned `LIVE_RHINO_REQUIRED` in CLI fallback mode.

Release curve-derived CLI fallback smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- curve-derived-geometry-tools-smoke-test
```

Result: passed. Output confirmed all new live-only paths returned `LIVE_RHINO_REQUIRED` in CLI fallback mode.

Debug MCP safety smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-tool-safety-annotations-smoke-test
```

Result: passed. Output confirmed safety annotations for 128 tools and no bare method-level `[McpServerTool]` attributes.

Release MCP safety smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-tool-safety-annotations-smoke-test
```

Result: passed. Output confirmed safety annotations for 128 tools and no bare method-level `[McpServerTool]` attributes.

Debug MCP surface governance smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-surface-structure-governance-smoke-test
```

Result: passed. Output discovered 128 tools, including `Geometry/CurveOps: 9`.

Release MCP surface governance smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-surface-structure-governance-smoke-test
```

Result: passed. Output discovered 128 tools, including `Geometry/CurveOps: 9`.

Whitespace check:

```powershell
git diff --check
```

Result: passed. Output only contained existing LF-to-CRLF warnings.

Live Rhino smoke:

```text
_McpCurveDerivedGeometryToolsSmoke
```

Result: not run in this CLI-only execution environment. The command is implemented and creates source curves plus a target surface, then exercises loft, curve extrusion, sweep, offset, pipe, projection, split preview, non-destructive split segment creation, and destructive split replacement.

## Acceptance Alignment

- Live-only: satisfied through `ILiveRhinoDocumentAccessor`; CLI fallback returns `LIVE_RHINO_REQUIRED`.
- One undo record per apply call: satisfied through `ExecuteWithUndo` in the live operator.
- Source object id resolution: satisfied through direct live `RhinoDoc.Objects.FindId` resolution.
- Creation tools preserve sources: satisfied.
- `CreateSplitCurveSegments` preserves source curves: satisfied.
- `ReplaceSplitCurves` deletes source curves and is marked destructive: satisfied.
- Batch per-entry results: satisfied through `CurveDerivedGeometryEntryResponse`.
- Safety annotations: satisfied in Debug and Release.
- Debug and Release builds: passed.
- Live smoke: command implemented; manual Rhino run remains pending.

## Rollback Verification

Rollback is localized to the new CurveOps tools, DTOs, domain specs/enums, `RhinoCurveDerivedGeometryService`, `ILiveCurveDerivedGeometryOperator`, `LiveCurveDerivedGeometryOperator`, DI additions, safety smoke entries, smoke test folder, EXET, and the Rhino smoke command. Existing geometry creation, editing, analysis, and architecture tools remain separate.

## Current Remaining Items

- Run `_McpCurveDerivedGeometryToolsSmoke` inside Rhino with a saved active document to verify actual live object creation, projection, and destructive split replacement behavior.

## Conclusion

The curve-derived geometry tools plan is implemented and CLI-validated in Debug and Release. The only remaining verification is the live Rhino smoke command, which requires an active Rhino session.
