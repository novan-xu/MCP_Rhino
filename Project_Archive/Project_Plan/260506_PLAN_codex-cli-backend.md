# 260506_PLAN_codex-cli-backend

## Background

The settings overlay shipped with `260506_PLAN_rhino-agent-panel-settings.md` exposes a
"Backend CLI" choice between `claude code cli` and `codex cli`, but the host currently
short-circuits codex selection by emitting a yellow diagnostic and staying on Claude
Code. The user has now asked for codex to be a real backend.

Public docs for OpenAI's Codex CLI (consulted during planning):

- [Command line options – Codex CLI](https://developers.openai.com/codex/cli/reference)
- [Non-interactive mode – Codex](https://developers.openai.com/codex/noninteractive)
- [Model Context Protocol – Codex](https://developers.openai.com/codex/mcp)

Key points that shape the implementation:

- `codex exec` is the non-interactive entry point; it is one-shot per process.
- `codex exec --json` emits a JSONL event stream to stdout (`thread.started`,
  `turn.started`, `item.*`, `turn.completed`, `turn.failed`, `error`).
- A bare `-` positional reads the prompt from stdin.
- `-c key=value` overrides any `config.toml` setting inline; `[mcp_servers.<name>]`
  config supports stdio MCP servers with `command`, `args`, `env`, `cwd`.
- `-C <dir>` sets the working directory; `-m <model>` overrides the model;
  `--skip-git-repo-check` lifts the git-repo requirement;
  `-s read-only|workspace-write|danger-full-access` picks the sandbox.
- Conversation continuity across turns is **not** native to `codex exec`; `codex
  resume` is documented as "continue prior interactive session" and is not relied on
  in v1.

## Goals

1. Add a real Codex CLI backend to the Companion that the front-end can switch to via
   the existing `cli` IPC.
2. Per turn, spawn `codex exec --json` with the bound document context prepended to the
   user's prompt, the per-document MCP pipe injected via `-c mcp_servers.…` overrides,
   and the user's chosen model passed through `-m`.
3. Parse the JSONL stream and surface events to the panel using the existing
   `CompanionUiEvent` types (`message`, `thinking`, `tool`, `result`, `diagnostic`,
   `status`, `input`).
4. Keep Claude Code as the default backend and as a swappable alternative — the user
   can flip between them through Settings without restarting the Companion process.

Non-goals for v1:

- Multi-turn conversation continuity through `codex resume` (each codex turn is a
  fresh, independent process; the bound document context is re-prepended each turn).
- Codex login / auth handling (the user is expected to have already run `codex login`).
- Reading codex usage costs into the status-bar `$` field — codex's `turn.completed`
  reports tokens, not USD; the cost field stays at `0.0000` for codex turns.
- Real-time streaming of partial assistant tokens (codex JSONL events arrive as whole
  items; the UI renders each item as it lands, which is the same per-block experience
  it already gives Claude Code).

## Architecture Ownership

This is **Companion-side infrastructure** under `src/MCP_Rhino.Companion/`. No
RhinoCommon, Tools/, Skills/, or Agents/ involvement. The Companion is a sibling
csproj allowed by the architecture guides (`MCP_Rhino Architecture.md` §"演进指南").

New abstraction: `IAgentSession` — common shape that both `ClaudeCodeSession` and the
new `CodexCliSession` implement. The MainWindow holds an `IAgentSession?` and swaps
implementations on `cli` IPC.

## Key Design

### `IAgentSession`

```csharp
public interface IAgentSession : IDisposable
{
    event EventHandler<CompanionUiEvent>? EventReceived;
    Task StartAsync(CancellationToken cancellationToken);
    Task SendUserMessageAsync(string text, CancellationToken cancellationToken);
    void Stop();
}
```

`ClaudeCodeSession` already exposes these methods; the change is just adding the
interface.

### `CodexCliAvailability`

Mirrors `ClaudeCodeAvailability` but probes `codex --version`. Discovery: PATH +
`%USERPROFILE%\.local\bin\codex` (matching the Claude Code pattern). Caches the
result. No minimum version is enforced; codex's exec/JSON contract has been stable.

### `CodexCliSession`

Per-turn invocation pattern:

```text
codex exec
  --json
  --skip-git-repo-check
  -s read-only
  -C <documentDirectory>
  [-m <modelId>]
  -c mcp_servers.rhino.command=<bridgePath>
  -c mcp_servers.rhino.args=["--pipe", "<pipeName>"]
  -
```

stdin: bound-document context (the same bullet list the Claude Code session passes via
`--append-system-prompt`) followed by a blank line and the user's prompt. Codex reads
the entire stdin to EOF as the prompt because of the trailing `-` argument.

stdout: JSONL parser maps events to `CompanionUiEvent`:

| Codex event                      | Companion UI event                                  |
| -------------------------------- | --------------------------------------------------- |
| `thread.started`                 | none (record `thread_id` for diagnostics)            |
| `turn.started`                   | `SessionStatus("codex working")`, `Input(false)`    |
| `item.*` text/message            | `Message("assistant", text)`                        |
| `item.*` reasoning/thinking      | `Thinking(text)`                                    |
| `item.*` tool/command call       | `Tool("running", id, name, input, null)`            |
| `item.*` tool/command result     | `Tool(success/failed, id, name, null, result)`      |
| `turn.completed`                 | `Result(null)`, `Input(true)`, status "codex ready" |
| `turn.failed`                    | `Diagnostic(error)`, `Input(true)`                  |
| `error`                          | `Diagnostic(message)`                                |
| unknown                          | `Diagnostic("codex event: …")` once per type, throttled |

Because the codex `item.*` shapes are not exhaustively documented, the parser
extracts conservatively:

- Message/text items: any `.text` / `.message.text` / `.content[*].text` field.
- Reasoning items: any `.reasoning` / `.thinking` field.
- Tool / command items: `.command` (shell) or `.tool_call` (MCP) sub-objects with
  `.name`, `.arguments`, `.output`. The pair (started/completed) is matched on the
  shared `.id` so the existing tool-card-by-id renderer in `app.js` works unchanged.

stderr → `Diagnostic` events.

`Stop()` kills the in-flight `codex exec` process and marks the session ready for the
next turn.

### MCP injection

Codex has no `--mcp-config <file>` flag; instead, MCP servers are read from
`config.toml`. The cleanest non-invasive path is inline `-c` overrides — codex docs
state "Override any config.toml setting inline". The Companion passes:

```text
-c "mcp_servers.rhino.command=\"<bridgePath>\""
-c "mcp_servers.rhino.args=[\"--pipe\",\"<pipeName>\"]"
```

If a user environment surfaces a TOML-array parsing issue with the inline form, the
fallback is to write `<tempDir>/.codex/config.toml` and pass `-C <tempDir>`. v1 ships
the inline form; the temp-dir fallback can be a follow-up if reports come in.

### MainWindow runtime swap

`MainWindow` holds `private IAgentSession? _session;`. Construction defaults to
`new ClaudeCodeSession(options)`. A new `SwapSessionAsync(string cli)`:

1. Detaches the event handler from the old session, calls `Stop()` and `Dispose()`.
2. Constructs the new session: `cli == "codex cli"` → `CodexCliSession`, else
   `ClaudeCodeSession`.
3. Re-attaches the event handler.
4. Re-emits `Session(_options)` so the front-end refreshes the doc + pipe readout.
5. Calls `StartAsync` on the new session.

The `cli` IPC handler in `OnWebMessageReceived` calls `SwapSessionAsync` in place of
the previous diagnostic. Selecting `claude code cli` while already on Claude (or codex
while already on codex) is a no-op early-return.

## Involved Files

New:

```
src/MCP_Rhino.Companion/IAgentSession.cs
src/MCP_Rhino.Companion/CodexCliAvailability.cs
src/MCP_Rhino.Companion/CodexCliSession.cs
src/MCP_Rhino.Companion/CodexJsonlEvent.cs
Project_Plan/260506_PLAN_codex-cli-backend.md
Project_Test/260506_TEST_codex-cli-backend/README.md
Project_Exet/260506_EXET_codex-cli-backend.md
```

Modified:

```
src/MCP_Rhino.Companion/ClaudeCodeSession.cs   (add : IAgentSession)
src/MCP_Rhino.Companion/MainWindow.xaml.cs     (factory + SwapSessionAsync; cli IPC swap instead of diagnostic)
```

The front-end `wwwroot/app.js` and existing IPC contract are unchanged — the host now
genuinely honors `cli` IPC instead of warning.

## Usage

```powershell
dotnet build src\MCP_Rhino.Companion\MCP_Rhino.Companion.csproj -c Release --nologo
```

In Rhino with a saved `.3dm` and a working `codex login`:

1. `_Mcpchat` → companion opens on Claude Code.
2. Open Settings, pick `codex cli`. Status pill shifts to `codex starting` then `codex ready`.
3. Send `List the Rhino layers in this file.` Codex spawns one `codex exec --json`
   process. The transcript shows tool cards for the rhino MCP calls and an assistant
   reply.
4. Pick `claude code cli` again — Companion swaps back; Claude Code restarts.

## Acceptance Criteria

- All three projects build with 0 warnings, 0 errors.
- The existing `rhino-claude-code-companion-ui-smoke-test` and
  `rhino-claude-code-panel-smoke-test` still pass (the IPC contract is additive).
- `_Mcpchat` defaults to Claude Code and continues to behave identically to before.
- Picking `codex cli` no longer surfaces the "not yet wired" diagnostic; the Companion
  spawns `codex exec --json` per turn, parses JSONL events, and renders them.
- Picking `claude code cli` after codex returns the Companion to Claude Code.
- If `codex` is not on PATH, picking codex emits a clear diagnostic ("Codex CLI was
  not found on PATH; install via `npm install -g @openai/codex` and run `codex
  login`.") and the input is disabled until the user picks Claude Code again.

## Risks And Rollback

- **Risk**: codex's `-c` inline override may not accept TOML arrays uniformly across
  platforms.
  - Mitigation: emit a stderr-style diagnostic if codex fails to start; fall back to
    a `<tempDir>/.codex/config.toml` + `-C <tempDir>` invocation. v1 logs the failure
    so the next iteration can switch the strategy if needed.
- **Risk**: `item.*` shapes evolve — the conservative parser may under-report
  details.
  - Mitigation: every unknown event surfaces once via `Diagnostic` so the user (and
    we) see the gap.
- **Risk**: codex needs an OAuth login that has not been completed.
  - Mitigation: stderr diagnostics surface codex's own auth error verbatim.
- **Rollback**: revert `MainWindow.xaml.cs` to the prior diagnostic-on-codex
  behavior and remove the new files. The IPC contract is unchanged so the front-end
  continues to work either way.

## Future Extensions

- `codex resume` continuity using the `thread_id` captured from `thread.started`.
- Persist the user's chosen CLI/model across launches in
  `%LOCALAPPDATA%\McNeel\Rhinoceros\8.0\Plug-ins\MCP_Rhino\companion.json`.
- Extract token usage from codex's `turn.completed.usage` and render an approximate
  cost in the status bar (will need a simple pricing table per model id).
- Surface codex's `command_started` shell calls as their own block kind (`shell`)
  rather than reusing the tool card.
