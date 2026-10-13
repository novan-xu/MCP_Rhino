# 260507_TEST_debug-bridge-only-plugin

## Scope

Verifies that Debug produces the bridge-pipe-only Rhino plugin and Release remains the chat-capable plugin, while both retain the Developer Debug Control Path marker.

## Commands

```powershell
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- debug-bridge-only-plugin-smoke-test
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-tool-safety-annotations-smoke-test
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-tool-safety-annotations-smoke-test
```

Expected smoke output:

```text
[OK] Debug .rhp is built and marked bridge-pipe-only.
[OK] Release .rhp is built and remains chat-capable.
[OK] Debug and Release both retain the Developer Debug Control Path marker.
```

## Recorded Results

Recorded on 2026-05-07:

```text
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug
Result: passed, 0 warnings, 0 errors.

dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release
Result: passed, 0 warnings, 0 errors.

dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- debug-bridge-only-plugin-smoke-test
Result: passed.
[OK] Debug .rhp is built and marked bridge-pipe-only.
[OK] Release .rhp is built and remains chat-capable.
[OK] Debug and Release both retain the Developer Debug Control Path marker.

dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-tool-safety-annotations-smoke-test
Result: passed, 78 MCP tools checked.

dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-tool-safety-annotations-smoke-test
Result: passed, 78 MCP tools checked.

dotnet build .\MCP_Rhino.sln -c Debug
Result: passed, 0 warnings, 0 errors.

dotnet build .\MCP_Rhino.sln -c Release
Result: passed, 0 warnings, 0 errors.
```
