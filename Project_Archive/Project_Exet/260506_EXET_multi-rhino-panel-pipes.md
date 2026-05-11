# 260506_EXET_multi-rhino-panel-pipes

## Corresponding Plan

- Plan: `Project_Plan/260506_PLAN_multi-rhino-panel-pipes.md`
- Execution date: 2026-05-06

## Associated Artifacts

- Test folder: `Project_Test/260506_TEST_multi-rhino-panel-pipes/`
- Commit / PR: not created in this pass.

## Execution Result / Actual Scope

- Added `McpPipeNames` as the single transport naming helper:
  - Developer debug pipe remains `mcp_rhino`.
  - Panel-bound pipes now use `mcp_rhino_<ProcessId>_<RuntimeSerialNumber>`.
- Updated standalone companion launch and Rhino-hosted fallback panel launch to use
  process-scoped panel-bound pipes.
- Updated both server-side and companion-side temporary MCP config directories to be pipe-name
  scoped, avoiding `%TEMP%\MCP_Rhino\<runtimeSerial>` collisions across Rhino processes.
- Kept `MCP_Rhino.Bridge.exe` default behavior intact: no `--pipe` still connects to
  `mcp_rhino`.
- Updated Bridge help text, README, and the architecture guide.
- Changed pipe creation failure handling so a busy fixed debug pipe logs once and stops that pipe
  server instead of entering an endless retry loop. The Rhino process can still start process-scoped
  panel pipes.

## Differences From Plan

- No material differences.
- Added README updates in addition to the required architecture guide update because the startup
  message and pipe model changed.

## Issues Found And Fixed

- Initial Release build failed because `MCP_Rhino.Bridge.exe` was locked by a running
  `MCP_Rhino.Bridge` process. After that process exited / was stopped, the Release build passed.
- The first busy-pipe failure hint was debug-pipe-specific even for bound pipes. It was refined so
  debug and panel-bound pipe creation failures have separate messages.

## Test Record

```powershell
dotnet build .\MCP_Rhino.sln -c Release
```

- Exit code: 0
- Result: `Build succeeded. 0 Warning(s), 0 Error(s).`

```powershell
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- multi-rhino-panel-pipes-smoke-test
```

- Exit code: 0
- Key output:
  - `[OK] Developer debug pipe remains mcp_rhino.`
  - `[OK] Panel-bound pipe is process-scoped: mcp_rhino_50180_12345.`
  - `[OK] Temporary MCP config directories are pipe-name scoped.`
  - `[OK] Project guide documents multi-Rhino pipe isolation.`

```powershell
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- rhino-chat-save-safety-smoke-test
```

- Exit code: 0
- Key output:
  - `[OK] EndSaveDocument does not start the Claude Code panel/session.`
  - `[OK] Explicit _Mcpchat panel startup path remains intact.`

```powershell
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- rhino-claude-code-panel-smoke-test
```

- Exit code: 0
- Key output:
  - `[OK] CLI mode does not instantiate a live or bound Rhino accessor.`
  - `[OK] McpConfigBuilder generated parseable per-doc MCP config JSON.`
  - `[OK] ClaudeCodeAvailability probe completed: Claude Code 2.1.131 is available.`
  - `[OK] MCP_Rhino.Bridge.exe --pipe other_name --help returns usage with exit code 0.`

```powershell
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- rhino-claude-code-companion-ui-smoke-test
```

- Exit code: 0
- Key output:
  - `[OK] CLI mode keeps the Rhino live accessor disabled.`
  - `[OK] Per-document MCP config JSON is parseable and contains the bound pipe name.`
  - `[OK] Claude Code availability probe completed: Claude Code 2.1.131 is available.`
  - `[OK] MCP_Rhino.Companion.exe accepts the command-line contract used by _Mcpchat.`

## Acceptance Alignment

- Multiple Rhino process panel/companion sessions now use process-scoped pipes.
- Original fixed `mcp_rhino` debug/test mode remains available for one owner process.
- A busy `mcp_rhino` debug pipe no longer prevents the second Rhino process from using panel-bound
  pipes.
- Project guidelines now explicitly require this split.

## Rollback Verification

- Reverting `McpPipeNames.ForPanelBoundDocument` usage in companion/panel launch paths would restore
  `mcp_rhino_<RuntimeSerialNumber>` and reintroduce cross-process collisions.
- Reverting `McpNamedPipeServer` create-failure handling would restore repeated `All pipe instances
  are busy` logging when a second Rhino process cannot bind `mcp_rhino`.

## Current Remaining Items

- Manual verification still required with two live Rhino 8 processes:
  - load the rebuilt `.rhp` in both
  - run `_Mcpchat` in both
  - confirm each session reports a distinct `mcp_rhino_<ProcessId>_<RuntimeSerialNumber>` pipe
  - confirm the second process does not spam `All pipe instances are busy`
- The working tree contains unrelated companion/Codex panel changes and artifacts that were not
  authored or validated by this pass.

## Conclusion

The transport naming model now supports parallel Rhino instances for LLM chat panel usage while
preserving `\\.\pipe\mcp_rhino` as the original single-owner debug/test connection mode.
