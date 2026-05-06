# Rhino Claude Code Companion UI Smoke

## CLI Fallback

Run from the repository root after building the companion:

```powershell
dotnet build src\MCP_Rhino.Companion\MCP_Rhino.Companion.csproj -c Release --nologo
dotnet run --project src\MCP_Rhino.Server -- rhino-claude-code-companion-ui-smoke-test
```

Expected output includes `[OK]` lines for CLI live-accessor fallback, per-document MCP
config JSON parsing, Claude Code availability probing, and companion `--validate-args`
argument parsing.

## Rhino Live

1. Build `src\MCP_Rhino.Server`, `src\MCP_Rhino.Bridge`, and `src\MCP_Rhino.Companion`.
2. Load `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.rhp` in Rhino.
3. Open a saved `.3dm` file.
4. Run `_McpRhinoClaudeCodeCompanionUiSmoke`.

The command verifies the bound accessor ignores a wrong `FilePath`, confirms the
companion executable can be resolved from the plugin location, starts a temporary
`mcp_rhino_companion_smoke_*` pipe, then stops it.

## Manual Companion Check

1. Confirm `claude --version` is available.
2. Build `src\MCP_Rhino.Bridge` and `src\MCP_Rhino.Companion` in `Release`.
3. Open a saved `.3dm` in Rhino.
4. Run `_Mcpchat`.
5. Confirm the standalone MCP_Rhino Companion window opens and shows the active file name.
6. Ask `List the current layers.` and confirm the tool activity renders as colored tool/status cards.
7. Open a second saved `.3dm` and run `_Mcpchat` again; confirm a separate companion session starts for that document.
8. Close one document and confirm only that document's companion session and bound pipe stop.
