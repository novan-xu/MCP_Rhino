# 260505_TEST_active-doc-only-architecture

## Purpose

This test folder records the regression checks used for `260505_PLAN_active-doc-only-architecture`.
The plan intentionally does not register a new smoke slug; it reuses existing CLI fallback smokes and existing Rhino live smoke commands.

## Automated Checks

Run from the repository root:

```powershell
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --nologo
dotnet build src\MCP_Rhino.Bridge\MCP_Rhino.Bridge.csproj -c Release --nologo
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- geometry-smoke-test Runtime_Test\MCP_rhino_test.3dm
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- online-mutation-refactor-smoke-test Runtime_Test\MCP_rhino_test.3dm
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- layer-management-smoke-test Runtime_Test\MCP_rhino_test.3dm
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- find-layer-candidates Runtime_Test\MCP_rhino_test.3dm Wall
rg -n "<forbidden disk-backed repository/read symbols>" src Project_Guides README.md Project_Test
rg -n "Offline Allowed|File3dm\.Read|--force-offline|Rhino/Offline|offline|Offline" "Project_Guides\MCP_Rhino Architecture.md"
```

Expected results:

- Both builds exit 0 with 0 warnings.
- The three CLI fallback smokes exit 0 and assert `LIVE_RHINO_REQUIRED`.
- The deleted `find-layer-candidates` CLI command exits non-zero through `Program.cs` fallback.
- Both grep commands return no matches.

## Manual Rhino Regression

When Rhino 8 is available with `Runtime_Test/MCP_rhino_test.3dm` active and saved, run the existing live smoke commands:

- `_McpGeometryAnalysisSmoke`
- `_McpFileImportExportSmoke`
- `_McpGeometryEditMolecularFoundationSmoke`
- `_McpGeometryEditCurveCompositeSmoke`
- `_McpGeometryEditDerivedRoutingSmoke`
- `_McpGeometryEditSurfaceCompositeSmoke`
- `_McpSurfacePointOrderRebuildSmoke`
- `_McpLayerBehaviorProbe`

Record pass/fail details in `Project_Exet/260505_EXET_active-doc-only-architecture.md`.
