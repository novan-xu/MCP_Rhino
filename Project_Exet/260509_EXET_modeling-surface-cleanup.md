# Modeling Surface Cleanup EXET

## Corresponding Plan

- Plan: `Project_Plan/260509_PLAN_modeling-surface-cleanup.md`
- Execution date: 2026-05-09

## Associated Artifacts

- Test folder: `Project_Test/260509_TEST_modeling-surface-cleanup/`
- Commit / PR: not created in this execution session

## Execution Result / Actual Scope

Removed the composite building massing workflow while preserving the atomic primitive and boolean tool surface.

Deleted:

- `src/MCP_Rhino.Server/Skills/Modeling/BuildingMassingSkill.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Architecture/CreateBuildingMassingTool.cs`

Removed:

- `BuildingMassingSkill` DI registration
- `BuildingMassingSkill` CLI constructor injection and field storage
- `RhinoArchitecturalPrimitiveService.CreateBuildingMassing`
- `ILiveArchitecturalGeometryBuilder.CreateBuildingMassing`
- `LiveArchitecturalGeometryBuilder.CreateBuildingMassing`
- building-massing-only request/response/spec classes
- `CreateBuildingMassing` from the MCP safety annotation expectation list
- `create_building_massing` from the runtime tool audit script
- building massing calls from the compiled architectural primitive smoke

Kept:

- `ArchitecturalPrimitiveCreationSkill`
- `ArchitecturalBooleanSkill`
- `CreateBoxes`, `CreateExtrusions`, `CreatePlanarBreps`
- `CreateSlabs`, `CreateWalls`, `CreateColumns`, `CreateBeams`
- `PreviewBooleanObjects`, `ApplyBooleanObjects`
- `PreviewOpenings`, `ApplyOpenings`
- `ReferenceImageObjectModelingAgent` and reference-image modeling skills

## Test Artifact

Added `Project_Test/260509_TEST_modeling-surface-cleanup/`.

The new CLI smoke verifies:

- `BuildingMassingSkill` is absent
- `CreateBuildingMassingTool` is absent
- building-massing-only contracts/specs are absent
- `CreateBuildingMassing` is not exposed as an MCP tool
- retained atomic primitive and boolean tools are still exposed
- `ReferenceImageObjectModelingAgent` remains present

Smoke slug:

```text
modeling-surface-cleanup-smoke-test
```

## Validation

Alternate Debug server build:

```powershell
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug -p:OutputPath=C:\01_Projects\MCP_Rhino\.validation\modeling-surface-cleanup-debug\
```

Result:

- exit code: 0
- warnings: 0
- errors: 0

Debug cleanup smoke:

```powershell
dotnet C:\01_Projects\MCP_Rhino\.validation\modeling-surface-cleanup-debug\MCP_Rhino.Server.dll modeling-surface-cleanup-smoke-test
```

Result:

- `[OK] modeling surface cleanup removed CreateBuildingMassing and preserved atomic primitive/boolean tools.`

Debug safety smoke:

```powershell
dotnet C:\01_Projects\MCP_Rhino\.validation\modeling-surface-cleanup-debug\MCP_Rhino.Server.dll mcp-tool-safety-annotations-smoke-test
```

Result:

- `[OK] MCP safety annotations verified for 135 tools.`
- `[OK] No bare method-level [McpServerTool] attributes remain.`

Debug retained primitive smoke:

```powershell
dotnet C:\01_Projects\MCP_Rhino\.validation\modeling-surface-cleanup-debug\MCP_Rhino.Server.dll architectural-modeling-primitives-smoke-test
```

Result:

- `[OK] architectural-modeling-primitives CLI fallback returned LIVE_RHINO_REQUIRED for live-only paths.`

Release solution build:

```powershell
dotnet build .\MCP_Rhino.sln -c Release
```

Result:

- exit code: 0
- warnings: 0
- errors: 0

Release cleanup smoke:

```powershell
dotnet src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.dll modeling-surface-cleanup-smoke-test
```

Result:

- `[OK] modeling surface cleanup removed CreateBuildingMassing and preserved atomic primitive/boolean tools.`

Release safety smoke:

```powershell
dotnet src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.dll mcp-tool-safety-annotations-smoke-test
```

Result:

- `[OK] MCP safety annotations verified for 135 tools.`
- `[OK] No bare method-level [McpServerTool] attributes remain.`

Release retained primitive smoke:

```powershell
dotnet src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.dll architectural-modeling-primitives-smoke-test
```

Result:

- `[OK] architectural-modeling-primitives CLI fallback returned LIVE_RHINO_REQUIRED for live-only paths.`

Active source reference check:

```powershell
rg -n "BuildingMassingSkill|CreateBuildingMassing|CreateBuildingMassingRequest|BuildingMassingResponse|BuildingMassingSpec|BuildingLevelRequest|BuildingLevelSpec|BuildingGridAxisRequest|create_building_massing" src -g "!bin" -g "!obj"
```

Result:

- exit code: 1
- no active source references found

Diff whitespace check:

```powershell
git diff --check
```

Result:

- exit code: 0
- only existing LF-to-CRLF warnings

## Debug Build Note

Standard Debug solution build:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug
```

Result:

- exit code: 1
- cause: `src\MCP_Rhino.Server\bin\Debug\net8.0\MCP_Rhino.Server.dll` is locked by `Rhino 8 (4892)`
- Bridge and Companion Debug projects built before the Server copy failed

This is expected while Rhino has the Debug plugin loaded. The alternate Debug output build passed and validates the server code without overwriting the loaded plugin DLL.

## Live Rhino Status

The currently loaded Debug plugin in Rhino still has the old locked assembly until Rhino unloads/reloads the plugin. Live tool-list verification for the changed surface should be run after reloading the rebuilt plugin.

## Rollback

Rollback restores:

- `BuildingMassingSkill`
- `CreateBuildingMassingTool`
- service/interface/live-adapter `CreateBuildingMassing` methods
- building-massing request/response/spec classes
- MCP safety expectation entry
- runtime audit `create_building_massing` call
- architectural primitive smoke massing assertions
- this PLAN/EXET/TEST cleanup artifact set

## Conclusion

The composite building massing workflow has been removed from active source and the MCP tool surface. Atomic primitive and boolean capabilities remain present and validated by the new cleanup smoke, safety smoke, and retained primitive smoke.
