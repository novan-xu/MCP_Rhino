# MCP Tool Overlap Cleanup Smoke

Run after changing duplicate MCP tool exposure:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-tool-overlap-cleanup-smoke-test
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-tool-overlap-cleanup-smoke-test
```

The smoke verifies the canonical tools remain exposed, known duplicate wrappers are absent, and touched tool-family counts match the cleaned surface.
