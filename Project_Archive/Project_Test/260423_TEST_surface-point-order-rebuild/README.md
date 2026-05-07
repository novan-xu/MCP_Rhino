# 260423_TEST_surface-point-order-rebuild

## CLI fallback

```powershell
dotnet run --project src/MCP_Rhino.Server -- surface-point-order-rebuild-smoke-test Runtime_Test/MCP_rhino_test.3dm
```

Expected outside Rhino plugin mode: inspect, preview, and apply return `LIVE_RHINO_REQUIRED`.

## Rhino live smoke

Open and save `Runtime_Test/MCP_rhino_test.3dm` in Rhino, then run:

```text
_McpSurfacePointOrderRebuildSmoke
```

The live command creates temporary quad and planar polygon objects, exercises Inspect, quad-only Preview/Apply behavior, one point-order undo name for successful quad targets, explicit edge override, non-quad polygon skip behavior, and cleanup.
