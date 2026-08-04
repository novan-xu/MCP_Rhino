# 260423_TEST_geometry-edit-surface-composite

## CLI fallback

```powershell
dotnet run --project src/MCP_Rhino.Server -- geometry-edit-surface-composite-smoke-test Runtime_Test/MCP_rhino_test.3dm
```

Expected outside Rhino plugin mode: both surface preview/apply tools return `LIVE_RHINO_REQUIRED`.

## Rhino live smoke

Open and save `Runtime_Test/MCP_rhino_test.3dm` in Rhino, then run:

```text
_McpGeometryEditSurfaceCompositeSmoke
```

The live command creates temporary `PlaneSurface`, `NurbsSurface`, and untrimmed single-face `Brep` objects, exercises direct grid reconstruction, exact-transform routing, UV-range normal offsets, selector guards, and deletes the temporary objects.
