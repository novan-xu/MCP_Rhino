# Modeling Surface Cleanup Smoke

## Purpose

Validate that the composite building massing workflow has been removed while atomic primitive and boolean tools remain available.

The smoke checks:

- `BuildingMassingSkill` is absent
- `CreateBuildingMassingTool` is absent
- building-massing-only contracts/specs are absent
- `CreateBuildingMassing` is absent from the MCP tool surface
- atomic primitive and boolean tools still exist
- `ReferenceImageObjectModelingAgent` remains present

## Commands

CLI fallback:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- modeling-surface-cleanup-smoke-test
```

No live Rhino document is required for this smoke.
