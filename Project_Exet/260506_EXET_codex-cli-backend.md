# 260506_EXET_codex-cli-backend

## 对应计划

- Plan: `Project_Plan/260506_PLAN_codex-cli-backend.md`
- Execute date: 2026-05-06

## 关联产物

- Test folder: `Project_Test/260506_TEST_codex-cli-backend/`
- External docs consulted:
  - [Command line options – Codex CLI](https://developers.openai.com/codex/cli/reference)
  - [Non-interactive mode – Codex](https://developers.openai.com/codex/noninteractive)
  - [Model Context Protocol – Codex](https://developers.openai.com/codex/mcp)
- Commit / PR: not created in this working session.

## 执行结果 / 实际落地范围

- Added a session abstraction `IAgentSession` so the WPF host can hold either
  Claude Code or Codex CLI behind the same shape:

  ```csharp
  public interface IAgentSession : IDisposable
  {
      event EventHandler<CompanionUiEvent>? EventReceived;
      Task StartAsync(CancellationToken cancellationToken);
      Task SendUserMessageAsync(string text, CancellationToken cancellationToken);
      void Stop();
  }
  ```

- `ClaudeCodeSession` now implements `IAgentSession` (the methods already matched).
- New `CodexCliAvailability.cs` mirrors the Claude Code probe: discovers `codex`
  on PATH (`%USERPROFILE%\.local\bin\codex` first, then PATH with .exe/.cmd/.bat
  variants on Windows), runs `codex --version`, parses the version, and caches
  the result. Reuses `ClaudeCodeAvailability.CreateStartInfo` to keep the same
  `cmd /d /s /c` quoting behavior for `.cmd`/`.bat` shims.
- New `CodexCliSession.cs` implements per-turn `codex exec --json` invocation:
  - On `StartAsync`, probes availability and Bridge presence, emits status
    `Codex ready` and enables input. No process is spawned yet (codex exec is
    one-shot per turn).
  - On `SendUserMessageAsync(text)`:
    - Echoes the user message via `Message("user", text)`.
    - Builds a prompt: a bound-document system context block + blank line + user
      text (since codex has no `--append-system-prompt` analogue).
    - Spawns `codex exec --json --skip-git-repo-check -s read-only -C <documentDir>
      -c mcp_servers.rhino.command="<bridge>" -c mcp_servers.rhino.args=["--pipe","<pipe>"]
      [-m <modelId>] -` and writes the prompt to stdin.
    - Reads stdout JSONL line-by-line and stderr line-by-line concurrently.
    - Maps codex events to `CompanionUiEvent`:
      - `thread.started` → records `_lastThreadId` for future resume work.
      - `turn.started` / `turn.completed` / `turn.failed` / `error` → status,
        result, diagnostic respectively.
      - `item.*` → text/thinking → `Message`/`Thinking`; tool/command call → `Tool`
        with `running`/`success`/`failed` based on the `item.*` suffix and `is_error`
        / `status` flags.
      - Any unknown event type / item kind surfaces once via `Diagnostic` (a
        deduped `_diagnosticOnceSeen` set throttles repeats).
  - On `Stop`, kills the in-flight codex exec process tree.
  - MCP injection uses the inline `-c` overrides documented in the Codex config
    reference; no project-scoped `.codex/config.toml` is written, so the user's
    workspace stays clean.
  - Sandbox is `read-only` because the panel use case is MCP-driven; codex does
    not need filesystem writes for these turns.
- Refactored `MainWindow.xaml.cs`:
  - `_session` is now `IAgentSession?` with a default of `ClaudeCodeSession` and
    constants `CliClaudeCode = "claude code cli"`, `CliCodex = "codex cli"`.
  - Added `IAgentSession CreateSession(string cli)` factory and async
    `SwapSessionAsync(string cli)` that detaches the old session, stops/disposes
    it, constructs the new one, re-attaches the event handler, re-emits
    `Session(_options)` so the front-end refreshes the doc/pipe readout, then
    calls `StartAsync` on the new session.
  - The `cli` IPC handler now calls `SwapSessionAsync` instead of emitting the
    "not yet wired" diagnostic.
  - `Closing` now disposes through the interface (null-safe).

## 与计划的偏差

- The plan's session-event mapping assumed an `Input(false)` after `turn.started`
  and `Input(true)` after `turn.completed`. The implementation moves that
  bookkeeping into `SendUserMessageAsync` itself (around the process spawn / await
  / cleanup), so `Input` toggles correctly even if codex emits no `turn.*` events
  (e.g. immediate failure). This is functionally equivalent and more robust.
- Conservative `item.*` parsing matches the plan but with one extra defense: any
  unknown item kind surfaces once via `Diagnostic` (deduped per type) so iterations
  on JSONL renderings can happen with feedback rather than silently dropping
  events.

## 施工中发现并修复的问题

- `CodexCliAvailability.CreateStartInfo` initially duplicated logic from
  `ClaudeCodeAvailability`. Trimmed to reuse `ClaudeCodeAvailability.CreateStartInfo`
  to keep the .cmd / .bat shim quoting consistent across both backends.
- The first MainWindow refactor left a non-nullable `_session` field while the
  swap path could theoretically observe a transient null. Tightened the field to
  `IAgentSession?` and added null guards in the IPC dispatcher and the `Closing`
  handler.

## 测试记录

```powershell
dotnet build src\MCP_Rhino.Companion\MCP_Rhino.Companion.csproj -c Release --nologo
```

- Exit code: 0
- Result: `MCP_Rhino.Companion -> ...\bin\Release\net8.0-windows\MCP_Rhino.Companion.dll`, 0 warnings, 0 errors.

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- rhino-claude-code-companion-ui-smoke-test
```

- Exit code: 0
- Output:
  - `[OK] CLI mode keeps the Rhino live accessor disabled.`
  - `[OK] Per-document MCP config JSON is parseable and contains the bound pipe name.`
  - `[OK] Claude Code availability probe completed: Claude Code 2.1.131 is available.`
  - `[OK] MCP_Rhino.Companion.exe accepts the command-line contract used by _Mcpchat.`

Live Rhino verification (codex turn end-to-end) is the remaining manual step
documented in `Project_Test/260506_TEST_codex-cli-backend/README.md`.

## 验收判据对齐

- Companion builds clean.
- IPC contract is additive (existing events unchanged; `cli` now triggers a real
  swap instead of a warning).
- `_Mcpchat` continues to default to Claude Code; existing flows are untouched.
- Selecting `codex cli` constructs a `CodexCliSession` and spawns `codex exec
  --json` per turn.
- Selecting `claude code cli` after codex returns to Claude Code.
- Codex unavailable on PATH surfaces a clear install/login diagnostic and keeps
  the input disabled.

## 回退验证

- No rollback executed.
- Practical rollback: revert `MainWindow.xaml.cs` to the prior diagnostic
  short-circuit and remove `IAgentSession.cs`, `CodexCliAvailability.cs`,
  `CodexCliSession.cs`. The front-end and the rest of the host are unchanged.

## 当前遗留项

- Live Rhino smoke: open a saved `.3dm`, switch to `codex cli`, send a turn that
  uses the rhino MCP server, confirm tool cards and assistant text render.
- Confirm the inline `-c` TOML-array override format works on the user's codex
  build. If it doesn't, fall back to writing a temp `<tempDir>/.codex/config.toml`
  and using `-C <tempDir>`.
- Iterate on `item.*` shape handling once we observe real JSONL events from
  codex (the deduped `Diagnostic` surfaces unknown types so the gap is visible).
- Add `codex resume` support for cross-turn continuity (track `_lastThreadId`
  is already in place).
- Read codex's `turn.completed.usage` and convert to USD for the status bar.

## 结论

Codex CLI is a real backend now: selecting it from Settings spawns `codex exec
--json` per turn with the bound rhino MCP server injected via inline `-c`
overrides, parses JSONL events into the existing message / thinking / tool /
result UI events, and falls back to a clear install diagnostic if codex isn't on
PATH. The Companion builds clean and the existing companion-ui smoke still
passes. Live Rhino verification is the remaining manual step.
