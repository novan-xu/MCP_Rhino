# Codex CLI Backend Smoke

This adds a real OpenAI Codex CLI backend to the Companion. The Settings overlay's
`codex cli` choice now spawns `codex exec --json` per turn instead of warning.

## Build

```powershell
dotnet build src\MCP_Rhino.Bridge\MCP_Rhino.Bridge.csproj         -c Release --nologo
dotnet build src\MCP_Rhino.Companion\MCP_Rhino.Companion.csproj   -c Release --nologo
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj         -c Release --nologo
```

If Rhino has the plugin loaded, the Server `.rhp` copy step will fail with a
file-locked error. Close Rhino (or unload the plugin) before rebuilding the Server.
The Companion is a separate `.exe` and rebuilds independently.

## CLI smoke

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- rhino-claude-code-companion-ui-smoke-test
```

Expected: all four `[OK]` lines pass. The IPC contract is unchanged, so the existing
smoke continues to validate the launch contract.

## Manual UI smoke

### Prerequisites

- `claude --version` ≥ 2.1.119 (existing requirement).
- `codex --version` works on PATH. Install via `npm install -g @openai/codex` if
  needed; run `codex login` once.

### Steps

1. Build the three projects above.
2. Launch Rhino 8, run `_LoadPlugin` and pick
   `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.rhp`.
3. Open or save a `.3dm`.
4. Run `_Mcpchat`. The companion opens at 420×900, defaults to Claude Code.
5. Send a Claude turn (e.g. `List the layers in this file.`) and confirm the
   existing tool-card flow still works.
6. Open Settings (⚙). Pick `codex cli`. The status pill should briefly read
   `Codex starting`, then `Codex ready`. The composer becomes enabled.
7. Send `List the layers in this file using the rhino MCP tool.` Confirm:
   - The user message echoes once.
   - The status pill shows `Codex working`, then `Codex ready`.
   - At least one assistant message appears.
   - At least one tool card renders for the rhino MCP call.
   - The send button morphs to a red stop button while busy.
8. Click the stop button mid-turn. Confirm the codex exec process is killed and
   the input becomes enabled again.
9. Switch back to `claude code cli` from Settings. Confirm Claude Code restarts
   and a normal Claude turn works.
10. Disable codex (e.g. rename `codex.cmd` temporarily) and pick `codex cli` from
    Settings. Confirm a clear diagnostic appears:
    `Codex CLI was not found on PATH. Install via 'npm install -g @openai/codex' and run 'codex login'.`
11. Restore codex on PATH and switch back; confirm it picks up.

### Expected event coverage

For a typical codex turn, the following `CompanionUiEvent`s are emitted:

- `status: "Codex starting"` → `status: "Codex ready"` (start)
- `status: "Codex working"` (per turn)
- `message(role=user, text)` (echo)
- `message(role=assistant, text)` and/or `thinking(text)` for codex's reasoning items
- `tool(running)` then `tool(success|failed)` for every rhino MCP call
- `result(totalCostUsd=null)` on `turn.completed`
- `status: "Codex ready"` after the turn

### Known limitations (v1)

- Each codex turn is a fresh `codex exec` process. The bound document context is
  re-prepended each turn; conversation continuity across turns is not yet wired
  through `codex resume`.
- Token usage from codex's `turn.completed.usage` is not yet converted to USD; the
  status-bar `$` value stays at `0.0000` for codex turns.
- Unknown JSONL `item.*` event shapes are surfaced once via Diagnostic so we can
  iterate on rendering them.

## Rollback

Revert `MainWindow.xaml.cs` to the prior diagnostic-on-codex behavior and remove
the four new files (`IAgentSession.cs`, `CodexCliAvailability.cs`,
`CodexCliSession.cs`, plus this folder). The IPC contract is unchanged.
