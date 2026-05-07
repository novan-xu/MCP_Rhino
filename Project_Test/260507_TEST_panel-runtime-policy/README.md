# 260507_TEST_panel-runtime-policy

## Scope

Verifies that the runtime policy bundle is copied to both Server and Companion outputs and is loaded by the Companion Claude/Codex prompts plus the Rhino-hosted fallback panel prompt.

## Command

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- panel-runtime-policy-smoke-test
```

Expected output:

```text
[OK] Runtime policy bundle copied to Server output.
[OK] Runtime policy bundle copied to Companion output.
[OK] Companion and fallback panel prompts load the runtime policy.
[OK] Project workspace and debug-pipe routes do not inject the panel runtime policy.
```

## Recorded Result

```powershell
dotnet build .\MCP_Rhino.sln -c Release
```

- Exit code: 0
- Result: build succeeded with 0 warnings and 0 errors.

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- panel-runtime-policy-smoke-test
```

- Exit code: 0
- Result: all runtime policy copy, prompt-loader, and non-panel route isolation assertions passed.

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-tool-safety-annotations-smoke-test
```

- Exit code: 0
- Result: MCP safety annotations remained valid for 78 tools.
