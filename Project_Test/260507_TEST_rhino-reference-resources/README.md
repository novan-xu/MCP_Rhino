# Rhino Reference Resources Smoke

CLI smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- rhino-reference-resources-smoke-test
```

The smoke validates curated Rhino reference modules, function lookup, missing-function handling, generated MCP_Rhino tool help, and MCP resource member discovery. It does not access a live Rhino document.
