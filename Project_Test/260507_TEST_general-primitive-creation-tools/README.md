# General Primitive Creation Tools Smoke

CLI fallback:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- general-primitive-creation-tools-smoke-test
```

Live Rhino smoke:

```text
_McpGeneralPrimitiveCreationToolsSmoke
```

The live command requires a saved active Rhino document. It creates one circle, ellipse, polyline, NURBS curve, sphere, cone, and cylinder on a timestamped smoke layer.
