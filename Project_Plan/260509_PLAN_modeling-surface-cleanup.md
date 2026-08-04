# Modeling Surface Cleanup Plan

## Background

The repository now has a reference-image object modeling agent that performs object-level modeling by decomposing supplied reference context into general primitive geometry, visual QA checkpoints, details, and materials. That makes the older dedicated `BuildingMassingSkill` workflow redundant and potentially misleading: it presents a fixed architectural-building workflow alongside the newer general object-modeling route.

The useful lower-level geometry surface should remain available. Atomic creation and boolean tools are still needed by agents and direct runtime tasks.

## Goal

Remove the composite building massing workflow while preserving atomic geometry and boolean capabilities.

Remove:

- `BuildingMassingSkill`
- `CreateBuildingMassingTool`
- `CreateBuildingMassing` service/interface/live-adapter methods
- building-massing-only request, response, and domain model types
- compiled smoke/audit references to `CreateBuildingMassing` / `create_building_massing`

Keep:

- `ArchitecturalPrimitiveCreationSkill`
- `ArchitecturalBooleanSkill`
- `CreateBoxes`
- `CreateExtrusions`
- `CreatePlanarBreps`
- `CreateSlabs`
- `CreateWalls`
- `CreateColumns`
- `CreateBeams`
- `PreviewBooleanObjects`
- `ApplyBooleanObjects`
- `PreviewOpenings`
- `ApplyOpenings`
- reference-image object modeling agent and skills

## Architecture Ownership

- `Skills/Modeling/`
  - Delete only the composite `BuildingMassingSkill`.
  - Keep atomic primitive and boolean skills.

- `Tools/Geometry/Architecture/`
  - Delete only `CreateBuildingMassingTool`.
  - Keep the remaining single-operation primitive and boolean tools.

- `Application/Services`, `Application/Interfaces`, `Infrastructure/Rhino/Live`
  - Remove only the building massing method chain.
  - Keep the lower-level architectural geometry builder and boolean operator because they still back atomic tools.

- `Contracts/Requests`, `Contracts/Responses`, `Domain/Models`
  - Remove building-massing-only DTO/spec classes.
  - Preserve DTO/spec classes used by atomic tools.

- `Project_Test/`
  - Add a focused cleanup smoke test that asserts `CreateBuildingMassing` is absent and atomic tools remain present.
  - Update older compiled smoke tests so the project builds after the intentional removal.

## Acceptance Criteria

- The MCP tool surface no longer exposes `CreateBuildingMassing`.
- `BuildingMassingSkill` and `CreateBuildingMassingTool` no longer exist.
- Atomic primitive and boolean tools remain registered and annotated.
- Reference-image object modeling code does not depend on the removed building massing workflow.
- Runtime tool audit no longer calls `create_building_massing`.
- MCP safety annotation smoke passes with the reduced tool count.
- New modeling surface cleanup smoke passes.
- Debug build passes or, if Rhino locks Debug output, an alternate Debug output build passes.
- Release build passes.

## Test Plan

Run:

```powershell
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug -p:OutputPath=<REPO_ROOT>\.validation\modeling-surface-cleanup-debug\
dotnet <REPO_ROOT>\.validation\modeling-surface-cleanup-debug\MCP_Rhino.Server.dll modeling-surface-cleanup-smoke-test
dotnet <REPO_ROOT>\.validation\modeling-surface-cleanup-debug\MCP_Rhino.Server.dll mcp-tool-safety-annotations-smoke-test
dotnet build .\MCP_Rhino.sln -c Release
dotnet src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.dll modeling-surface-cleanup-smoke-test
dotnet src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.dll mcp-tool-safety-annotations-smoke-test
```

Live Rhino verification after plugin rebuild:

- tool list does not contain `create_building_massing`
- tool list still contains atomic primitive and boolean tools

## Risks And Rollback

- Risk: archived compiled smoke tests still reference deleted types.
  - Mitigation: update compiled smoke code to test only retained atomic tools.

- Risk: a hidden reference-image dependency exists.
  - Mitigation: search and build validation must confirm no references to removed types remain in active source.

Rollback restores the deleted skill/tool files, re-adds the service/interface/live-adapter methods, restores the request/response/spec classes, and puts `CreateBuildingMassing` back into smoke expectations and runtime audit.
