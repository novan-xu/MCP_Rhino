# Selection Scoped Analysis Recipes Plan

## Background

Several live analysis tools currently require the caller to send explicit object IDs or per-object entries. For broad analysis workflows, the LLM often has to run `FilterObjects`, receive a large GUID list, then send those GUIDs back to metrics, mass properties, frame, curvature, or contour tools. This has the same shape as the bulk attribute bottleneck: the server already has enough information to derive the target set from compact criteria, but the client is forced to shuttle large intermediate payloads.

The better shape is to accept compact filter criteria plus a compact analysis recipe, resolve matching live objects server-side, and call the existing analysis services without exposing the object-id list as model-generated input.

## Goals

- Add read-only selection-scoped analysis tools for common large-set analysis workflows.
- Reuse the existing live object selection criteria and ambiguity handling.
- Preserve the existing explicit object-id tools for precise small sets and backward compatibility.
- Avoid arbitrary Python or C# script execution; keep the recipe expansion in typed C# services/skills.
- Keep result-size protections aligned with the existing analysis limits.

## Architecture Ownership

- `Contracts/Requests`: request DTOs for filter-scoped analysis recipes.
- `Skills/Inspection`: orchestration that resolves live selection criteria and expands recipes into existing analysis requests.
- `Tools/Analysis`: direct MCP wrappers for read-only analysis-by-filter tools.
- `Server/AgentRegistration.cs`: DI registration for the new inspection skill.
- `Project_Test/260508_TEST_selection-scoped-analysis-recipes/`: static smoke coverage and README.

## Key Design

- Add these read-only MCP tools:
  - `GetObjectMetricsByFilter`
  - `GetMassPropertiesByFilter`
  - `GetGeometryFramesByFilter`
  - `GetCurvatureSamplesByFilter`
  - `GetContourCurvesByFilter`
- Reuse the same filter fields used by existing filter/transform/delete/user-text recipe flows:
  - `LayerQueries`
  - `ConfirmedLayerFullPaths`
  - `ObjectTypes`
  - `UserAttributeConditions`
  - `MatchMode`
  - `UserAttributeMatchMode`
- The skill resolves matching objects through `LiveObjectSelectionSkill`, then builds the existing object-id or per-entry request types internally.
- Metrics and mass properties produce one existing analysis result per matched object.
- Frame, curvature, and contour tools use common recipe parameters and generate one entry per matched object.
- The tool response types reuse the existing analysis response DTOs so downstream callers can consume the same result records.
- The operation response message reports that a filter scope was resolved server-side.
- The explicit object-id analysis tools remain available for small confirmed lists and specialized per-object parameter sets.

## Involved Files

- `src/MCP_Rhino.Server/Contracts/Requests/SelectionScopedAnalysisRequests.cs`
- `src/MCP_Rhino.Server/Skills/Inspection/SelectionScopedAnalysisSkill.cs`
- `src/MCP_Rhino.Server/Tools/Analysis/GetObjectMetricsByFilterTool.cs`
- `src/MCP_Rhino.Server/Tools/Analysis/GetMassPropertiesByFilterTool.cs`
- `src/MCP_Rhino.Server/Tools/Analysis/GetGeometryFramesByFilterTool.cs`
- `src/MCP_Rhino.Server/Tools/Analysis/GetCurvatureSamplesByFilterTool.cs`
- `src/MCP_Rhino.Server/Tools/Analysis/GetContourCurvesByFilterTool.cs`
- `src/MCP_Rhino.Server/Server/AgentRegistration.cs`
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs`
- `Project_Test/260506_TEST_mcp-tool-safety-annotations/DeveloperCommandHandler.McpToolSafetyAnnotationsSmokeTest.cs`
- `Project_Test/260508_TEST_mcp-tool-overlap-cleanup/DeveloperCommandHandler.McpToolOverlapCleanupSmokeTest.cs`
- `Project_Test/260508_TEST_selection-scoped-analysis-recipes/`

## Usage

Example intent:

```json
{
  "filePath": "C:\\Models\\project.3dm",
  "confirmedLayerFullPaths": ["Level 01::Walls"],
  "objectTypes": ["Brep"],
  "kind": "Area"
}
```

This calls `GetMassPropertiesByFilter`, resolves the matching Breps on the server, and returns the same mass-property result records as `GetMassPropertiesInLive` without requiring the LLM to send every object ID.

## Acceptance Criteria

- Debug and Release solution builds pass.
- New tool methods have explicit MCP safety annotations and method-level descriptions.
- `selection-scoped-analysis-recipes-smoke-test` passes in Debug and Release.
- `mcp-tool-safety-annotations-smoke-test` passes in Debug and Release with the new read-only tools registered.
- `mcp-tool-overlap-cleanup-smoke-test` passes in Debug and Release with updated tool and analysis-family counts.
- The new tools are read-only, closed-world, and live-only.
- No external script runner or external file read/write path is introduced.

## Risks And Rollback

- Broad filters can produce large response payloads even though request payloads stay compact. Existing object-count and sample-count limits remain the guardrail.
- Frame, curvature, and contour recipes apply common parameters to every matched object; callers needing unique per-object parameters should keep using the existing explicit-entry tools.
- Rollback is a normal git revert of the new source files, smoke registration updates, safety/overlap smoke updates, and this PLAN / TEST / EXET artifact set.

## Future Extensions

- Add aggregate-summary-only analysis variants if response size becomes the next bottleneck.
- Add pair-generation recipes for intersections, distances, closest points, and continuity only after concrete pairing rules are known.
- Add pattern-creation recipes as a separate capability plan.
