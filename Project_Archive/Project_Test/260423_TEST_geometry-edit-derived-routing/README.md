# 260423_TEST_geometry-edit-derived-routing

Run the CLI fallback smoke:

```powershell
dotnet run --project src/MCP_Rhino.Server -- geometry-edit-derived-routing-smoke-test Runtime_Test/MCP_rhino_test.3dm
```

Expected CLI mode result: derived preview/apply calls return `LIVE_RHINO_REQUIRED`.

Run the live Rhino smoke from Rhino after opening and saving `Runtime_Test/MCP_rhino_test.3dm`:

```text
_McpGeometryEditDerivedRoutingSmoke
```
