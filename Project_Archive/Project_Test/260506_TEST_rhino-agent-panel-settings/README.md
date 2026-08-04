# Rhino Agent Panel Settings Smoke

Second-pass UI redesign of the Companion. Adds a panel-scoped Settings overlay
(reachable via a gear icon), a Backend CLI selector (`claude code cli` /
`codex cli`), an MCP-server readout, scales chrome ~1.2×, and switches the
default WPF window to the 420×900 narrow side-panel form factor.

Like the first redesign, all changes live in `src/MCP_Rhino.Companion/wwwroot/`
plus the WPF host's `MainWindow.xaml(.cs)`. No new backend behavior, no new CLI
slug. The existing companion-ui smoke verifies the launch contract and is
unchanged.

## Build

```powershell
dotnet build src\MCP_Rhino.Bridge\MCP_Rhino.Bridge.csproj         -c Release --nologo
dotnet build src\MCP_Rhino.Companion\MCP_Rhino.Companion.csproj   -c Release --nologo
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj         -c Release --nologo
```

If Rhino has the plugin loaded, the Server `.rhp` copy step will fail with a
file-locked error. Close Rhino (or unload the plugin) before rebuilding the
Server. The Companion is a separate `.exe` and rebuilds independently.

`src\MCP_Rhino.Companion\bin\Release\net8.0-windows\wwwroot\` should contain the
updated `index.html`, `styles.css`, and `app.js` (no `zoom` rule, has
`settings-overlay`, has `buildSettingsChoices`).

## CLI smoke

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- rhino-claude-code-companion-ui-smoke-test
```

Expected `[OK]` lines for live-accessor disabled, MCP config JSON parsing,
Claude Code availability, and Companion exe argument contract.

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- rhino-claude-code-panel-smoke-test
```

The panel smoke from the first redesign continues to pass.

## Manual UI smoke

1. Build and load the plugin in Rhino 8 (`_LoadPlugin` →
   `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.rhp`).
2. Open or save a `.3dm` so the active document has a `Path`.
3. Run `_Mcpchat`. The companion window should open at **420×900** with:
   - 40px header containing logo, MCP pill, +, ⚙, and pin icons (no model dropdown).
   - 28px status bar with a 20px context donut, cost, and model echo.
   - Composer with paperclip + 15px textarea + 28×28 send/stop button.
4. Click the gear icon ⚙. The Settings overlay appears with:
   - **Backend CLI** card list with `claude code cli` (active) and `codex cli`.
   - **Model** card list with `Default`, `claude-haiku-4-5`, `claude-sonnet-4-6`, `claude-opus-4-7`.
   - **MCP server** readout showing `rhino-mcp · mcp_rhino_<runtimeSerial> · connected` once the bound MCP reports connected.
5. Pick `codex cli`. The transcript should display a yellow warning block:
   `Backend CLI 'codex cli' is not yet wired in this build; staying on Claude Code.`
6. Pick a non-default model. The status bar's model echo updates to the new value;
   Claude Code restarts on the next prompt with the new `--model` arg (existing behavior).
7. Close the overlay via the X, the Done button, or by clicking the dim backdrop.
8. Send `List the current layers.` Confirm tool cards still render and the send
   button morphs to a red stop button while busy.
9. Click pin (📌). Confirm the window stays above Rhino.
10. Drop a file. Confirm the amber-dashed overlay appears and a chip shows above
    the composer.
11. Open a second saved `.3dm` and run `_Mcpchat`. Two independent companion
    windows; each shows its own bound pipe in the MCP readout.
