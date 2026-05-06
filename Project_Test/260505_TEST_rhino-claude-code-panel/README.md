# Rhino Claude Code Panel Smoke

## CLI Fallback

Run from the repository root after building Bridge:

```powershell
dotnet build src\MCP_Rhino.Bridge\MCP_Rhino.Bridge.csproj -c Release --nologo
dotnet run --project src\MCP_Rhino.Server -- rhino-claude-code-panel-smoke-test
```

Expected output includes `[OK]` lines for CLI live-accessor fallback, MCP config JSON parsing,
Claude Code availability probing, and Bridge `--pipe --help` parsing.

## Rhino Live

1. Build `src\MCP_Rhino.Server`.
2. Load `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.rhp` in Rhino.
3. Open a saved `.3dm` file.
4. Run `_McpRhinoClaudeCodePanelSmoke`.

The command verifies the bound accessor ignores a wrong `FilePath`, starts a temporary
`mcp_rhino_smoke_*` pipe, then stops it.

## Manual Claude Code Panel Check

1. Confirm `claude --version` is at least `2.1.119`.
2. Open a saved `.3dm`; the `Claude Code Chat` panel should appear.
   If it does not appear, run `_Mcpchat` to open it manually for the active saved document.
3. Send `List the current layers.` and confirm a tool call card appears.
4. Open a second saved `.3dm`; confirm its session is independent.
5. Close the second document and confirm no orphan `claude.exe` remains.
6. Temporarily remove Claude Code from PATH and confirm the panel disables input with an install prompt.
7. Temporarily remove `MCP_Rhino.Bridge.exe` and confirm the panel disables input with `Bridge not found`.
