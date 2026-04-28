# 260423_TEST_geometry-edit-curve-composite

Run the CLI fallback smoke:

```powershell
dotnet run --project src/MCP_Rhino.Server -- geometry-edit-curve-composite-smoke-test Runtime_Test/MCP_rhino_test.3dm
```

Expected CLI mode result: preview/apply curve edit calls return `LIVE_RHINO_REQUIRED`.

Run the live Rhino smoke from Rhino after opening and saving `Runtime_Test/MCP_rhino_test.3dm`:

```text
_McpGeometryEditCurveCompositeSmoke
```
