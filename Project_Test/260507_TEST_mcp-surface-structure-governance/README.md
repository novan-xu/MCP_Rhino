# MCP Surface Structure Governance Test

This folder contains the CLI smoke for `260507_PLAN_mcp-surface-structure-governance`.

Run after building:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-surface-structure-governance-smoke-test
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-surface-structure-governance-smoke-test
```

The smoke enumerates MCP tools and resources from the server assembly, fails on
duplicate tool method names, fails on missing method descriptions, and verifies
that source-level method `[McpServerTool(...)]` attributes explicitly declare
`ReadOnly`, `Destructive`, and `OpenWorld`.
