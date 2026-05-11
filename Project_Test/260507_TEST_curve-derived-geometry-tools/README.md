# Curve Derived Geometry Tools Smoke

CLI fallback:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- curve-derived-geometry-tools-smoke-test
```

Live Rhino smoke:

```text
_McpCurveDerivedGeometryToolsSmoke
```

The live command requires a saved active Rhino document. It creates source curves and a target surface, then exercises loft, curve extrusion, one-rail sweep, offset, pipe, projection, split preview, non-destructive split segment creation, and destructive split replacement.
