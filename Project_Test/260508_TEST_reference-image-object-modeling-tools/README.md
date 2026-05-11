# Reference Image Object Modeling Tools Smoke

## Purpose

Validate the first tools slice for reference-image object modeling:

- soft primitive creation: rounded box, ellipsoid, capsule, torus
- additive detail creation: raised strip
- material creation and direct object material assignment
- CLI fallback remains live-only

## Commands

CLI fallback:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- reference-image-object-modeling-tools-smoke-test
```

Live Rhino smoke:

```text
_McpReferenceImageObjectModelingToolsSmoke
```

The live command requires a saved active Rhino document and a loaded MCP_Rhino plugin.
