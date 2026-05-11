# Rhino Agent Panel Redesign Smoke

This redesign only changes presentation assets in
`src/MCP_Rhino.Companion/wwwroot/` plus the WPF host's window background and the
addition of a `pin` IPC handler. There is no new backend behavior, so no new CLI
slug is registered. The existing companion-ui slug verifies the launch contract,
config generation, Bridge discovery, and Claude Code availability probe.

## Build

Run from the repository root:

```powershell
dotnet build src\MCP_Rhino.Bridge\MCP_Rhino.Bridge.csproj         -c Release --nologo
dotnet build src\MCP_Rhino.Companion\MCP_Rhino.Companion.csproj   -c Release --nologo
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj         -c Release --nologo
```

Expected: all three exit 0 with 0 warnings, 0 errors.

`src\MCP_Rhino.Companion\bin\Release\net8.0-windows\wwwroot\` should contain the
new `index.html`, `styles.css`, and `app.js`.

## CLI smoke

The existing companion-ui smoke still passes against the redesigned UI:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- rhino-claude-code-companion-ui-smoke-test
```

Expected `[OK]` lines for live-accessor disabled, MCP config JSON parsing,
Claude Code availability, and Companion exe argument contract.

The earlier panel smoke also still passes:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- rhino-claude-code-panel-smoke-test
```

## Manual UI smoke

1. Build the three projects above.
2. Start Rhino 8, run `_LoadPlugin`, and pick
   `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.rhp`.
3. Open or save a `.3dm` so the active document has a `Path`.
4. Run `_Mcpchat`. The companion window should open with:
   - dark IDE background (`#0b0b0d`),
   - amber rhombus logo + `rhino·agent` wordmark,
   - MCP pill with a green pulse once the bound MCP server reports `connected`,
   - model selector dropdown showing `Default`, the three Claude models,
   - new-chat (+) and pin icons in the header,
   - status bar with context donut, cost, and model echo.
5. Type `List the current layers.` and press `⏎`. Confirm:
   - Send button morphs to a red stop button while busy.
   - A tool call card appears, collapsed by default, expanding to show input/result.
   - Result event updates the cost in the status bar.
6. Press `⇧⏎` mid-edit to insert a newline without sending.
7. Click the pin icon. The companion should now stay above Rhino. Click again
   to unpin.
8. Click new-chat (+). The transcript clears with a centered system divider.
   Claude Code is **not** killed; sending again continues the same backend
   session.
9. Drag a file onto the panel. The amber-dashed overlay should appear during
   drag, and the dropped file should become a removable chip above the
   composer. (Attachments are not yet uploaded; that is a known follow-up.)
10. Open a second saved `.3dm` and run `_Mcpchat` from it. A second companion
    window opens with its own bound pipe; the two sessions stay independent.
11. Close one document; only its companion exits.
