# Document Visual State Tools Test

This folder contains the smoke hook for `260507_PLAN_document-visual-state-tools`.

CLI fallback:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- document-visual-state-tools-smoke-test
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- document-visual-state-tools-smoke-test
```

Live Rhino smoke:

```text
_McpDocumentVisualStateToolsSmoke
```

The live smoke requires a saved active Rhino document. It creates smoke points on
a smoke layer, switches current layer, sets and reads selection, captures the
active viewport as a base64 PNG, then clears selection and restores the original
current layer.
