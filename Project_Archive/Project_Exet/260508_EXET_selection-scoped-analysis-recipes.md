# Selection Scoped Analysis Recipes Execution

## Corresponding Plan

- Plan: `Project_Plan/260508_PLAN_selection-scoped-analysis-recipes.md`
- Execution date: 2026-05-08

## Associated Artifacts

- Test folder: `Project_Test/260508_TEST_selection-scoped-analysis-recipes/`
- New smoke source: `Project_Test/260508_TEST_selection-scoped-analysis-recipes/DeveloperCommandHandler.SelectionScopedAnalysisRecipesSmokeTest.cs`
- README: `Project_Test/260508_TEST_selection-scoped-analysis-recipes/README.md`
- Commit / PR: not created in this workspace session

## Execution Result / Actual Scope

- Added shared filter-scoped analysis request DTOs.
- Added `SelectionScopedAnalysisSkill` under `Skills/Inspection`.
- Added five read-only MCP tools under `Tools/Analysis`:
  - `GetObjectMetricsByFilter`
  - `GetMassPropertiesByFilter`
  - `GetGeometryFramesByFilter`
  - `GetCurvatureSamplesByFilter`
  - `GetContourCurvesByFilter`
- Registered the new inspection skill in DI.
- Added and registered `selection-scoped-analysis-recipes-smoke-test`.
- Updated MCP safety and overlap cleanup smokes for the new 128-tool surface and 18-tool Analysis family.

## Differences From Plan

- No separate application service was added. The orchestration fits cleanly as an inspection skill because it composes existing live selection plus existing analysis services without new RhinoCommon infrastructure.
- No live Rhino command wrapper was added. The new smoke is static/CLI-oriented and verifies the MCP surface, request contracts, registration, and source-level architecture contract.

## Issues Found And Fixed During Execution

- No implementation defects were found during validation.
- The overlap and safety smokes needed expected-count updates because the MCP surface increased from 123 to 128 tools.

## Test Record

```powershell
dotnet build .\MCP_Rhino.sln -c Debug --no-restore
```

Result: passed, 0 warnings, 0 errors.

```powershell
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- selection-scoped-analysis-recipes-smoke-test
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-tool-safety-annotations-smoke-test
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-tool-overlap-cleanup-smoke-test
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-surface-structure-governance-smoke-test
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- runtime-text-normalization-smoke-test
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- review-findings-fix-smoke-test
```

Result: all passed. Safety smoke verified 128 MCP tools. Surface governance reported Analysis family count 18.

```powershell
dotnet build .\MCP_Rhino.sln -c Release --no-restore
```

Result: passed, 0 warnings, 0 errors.

```powershell
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- selection-scoped-analysis-recipes-smoke-test
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-tool-safety-annotations-smoke-test
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-tool-overlap-cleanup-smoke-test
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-surface-structure-governance-smoke-test
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- runtime-text-normalization-smoke-test
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- review-findings-fix-smoke-test
```

Result: all passed. Safety smoke verified 128 MCP tools. Surface governance reported Analysis family count 18.

```powershell
git diff --check
```

Result: passed; only existing line-ending conversion warnings were reported.

## Acceptance Criteria Alignment

- Debug and Release solution builds pass.
- New tool methods have explicit MCP safety annotations and method-level descriptions.
- `selection-scoped-analysis-recipes-smoke-test` passes in Debug and Release.
- `mcp-tool-safety-annotations-smoke-test` passes in Debug and Release with 128 tools.
- `mcp-tool-overlap-cleanup-smoke-test` passes in Debug and Release with 128 tools and 18 Analysis tools.
- New tools are read-only, closed-world, and live-only.
- No external script runner or external file read/write path was introduced.

## Rollback Verification

- No git rollback was performed.
- Rollback remains a normal git revert of the new source files, smoke updates, and this plan's PLAN / EXET / TEST artifacts.

## Current Remaining Items

- Add a true live Rhino smoke later if we want runtime assertions against a saved document fixture.
- Consider aggregate-summary-only variants if large analysis responses become the next bottleneck.
- Keep pair-generation recipes for intersections/distances/continuity as a separate plan because they need explicit pairing semantics.

## Conclusion

Selection-scoped analysis recipes are implemented as five read-only MCP tools. They move broad object resolution from the LLM payload into the Rhino server by composing existing live selection with existing analysis services, reducing the need to shuttle large object-id lists through MCP calls.
