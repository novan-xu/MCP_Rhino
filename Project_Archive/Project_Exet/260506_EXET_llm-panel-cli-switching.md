# LLM Panel CLI Switching EXET

## Corresponding Plan

- `Project_Plan/260506_PLAN_llm-panel-cli-switching.md`
- Execution date: 2026-05-06

## Related Artifacts

- Test folder: `Project_Test/260506_TEST_llm-panel-cli-switching/`
- Commit / PR: not created in this working tree

## Execution Result / Actual Scope

- Added host-side CLI/model catalog and provider-specific model session state.
- Extended companion session events with `cli` and `models`.
- Updated WebView settings rendering so Codex and Claude expose different model choices.
- Added host handling for frontend `model` messages; changing model restarts the current backend session so `--model` is applied.
- Replaced fragile `.cmd` launch string construction with `cmd.exe /d /c call <script> <args...>` in both companion and server CLI helpers.
- Updated Codex MCP config overrides to use TOML string values without embedded double quotes where possible.
- Added start/exit diagnostics for Claude Code process failures.
- Added a Claude Code restart-on-send path so a stopped or exited persistent process can recover instead of only reporting that input is disabled.

## Deviations From Plan

- The implementation also added a broad host-side WebView message exception diagnostic. This was added because provider/model switching can otherwise fail invisibly inside the async WPF message handler.
- A local `codex debug prompt-input` command was used to verify that installed Codex accepts the single-quoted TOML config overrides without making a model call.

## Issues Found And Fixed During Work

- `dotnet build` initially failed because the new smoke helper method name collided with existing partial smoke helpers. Renamed it to `RequireLlmPanelCli`.
- `src/MCP_Rhino.Companion/CliProcessStartInfo.cs` initially missed `System.IO` for `Path.GetExtension`; added the import.

## Test Record

- `dotnet build .\MCP_Rhino.sln -c Release`
  - Exit code: 0
  - Result: 0 warnings, 0 errors

- `dotnet run --project src\MCP_Rhino.Server -- llm-panel-cli-switching-smoke-test`
  - Exit code: 0
  - Key assertions:
    - `.cmd` script with spaces launches through `cmd.exe call`
    - quoted-path command is not reported as `not recognized`
    - frontend has provider-specific model lists
    - frontend sends model changes to host
    - host sends active CLI and model list
    - Codex config uses TOML values without embedded double-quote command arguments

- `dotnet run --project src\MCP_Rhino.Server -- rhino-claude-code-companion-ui-smoke-test`
  - Exit code: 0
  - Claude Code availability: 2.1.131
  - Companion `--validate-args` contract accepted

- `dotnet run --project src\MCP_Rhino.Server -- rhino-claude-code-panel-smoke-test`
  - Exit code: 0
  - Bridge help and Claude Code availability checks passed

- `dotnet run --project src\MCP_Rhino.Server -- multi-rhino-panel-pipes-smoke-test`
  - Exit code: 0
  - Debug pipe and process-scoped panel pipe checks passed

- `codex debug prompt-input -c "mcp_servers.rhino.command='<REPO_ROOT>\src\MCP_Rhino.Bridge\bin\Release\net8.0\MCP_Rhino.Bridge.exe'" -c "mcp_servers.rhino.args=['--pipe','mcp_rhino_nonexistent']" "ping"`
  - Exit code: 0
  - Result: installed Codex accepted the TOML override shape

## Acceptance Alignment

- Codex `.cmd` path quoting failure is covered by smoke and fixed by the shared process launcher.
- Switching provider now updates the model choices through host-owned session events.
- Model changes now reach the host and are applied to the next backend session.
- Existing Claude Code and named-pipe paths continue to pass their smoke checks.

## Rollback Verification

The change is isolated to companion provider/model state, CLI process start helpers, frontend settings rendering, and one CLI smoke registration. Removing those files/edits returns the panel to Claude-only static model behavior, but would reintroduce the Codex `.cmd` path failure.

## Current Remaining Items

- The model catalog is static. The installed Codex CLI can expose model catalog JSON, but the companion does not dynamically read it yet.
- Custom model IDs still require adding to the catalog or passing `--model` at companion launch.

## Conclusion

The LLM panel now has provider-aware model selection and a Windows-safe CLI launcher for npm `.cmd` shims. Release build and relevant smoke checks passed.

## Follow-up Fix: Codex UTF-8 Stdin (2026-05-06)

### Trigger

Codex still launched but failed with:

`Failed to read prompt from stdin: input is not valid UTF-8 (invalid byte at offset 16).`

The offset matches the first non-ASCII character in the Codex bound-document prompt. The command path was fixed, but redirected stdin was still using the process default Windows encoding instead of UTF-8.

### Additional Execution

- Updated both CLI process launchers to set:
  - `StandardInputEncoding = UTF8Encoding(false)`
  - `StandardOutputEncoding = UTF8Encoding(false)`
  - `StandardErrorEncoding = UTF8Encoding(false)`
- Extended `llm-panel-cli-switching-smoke-test` with a fake `.cmd` wrapper that reads redirected stdin and strictly decodes it as UTF-8.
- Produced an unlocked Release bundle at `artifacts/llm-panel-cli-utf8/` because the normal Release output was locked by live processes:
  - `MCP_Rhino.Companion (35284)`
  - `Rhino 8 (49432)`

### Additional Test Record

- `dotnet build .\MCP_Rhino.sln -c Release`
  - Exit code: 1
  - Reason: normal Release output locked by live Rhino / companion processes.

- `dotnet build .\MCP_Rhino.sln -c Debug`
  - Exit code: 0
  - Result: 0 warnings, 0 errors

- `dotnet run --project src\MCP_Rhino.Server -- llm-panel-cli-switching-smoke-test`
  - Exit code: 0
  - Added assertions:
    - command script receives redirected prompt stdin as valid UTF-8
    - UTF-8 stdin validation does not report invalid bytes
    - companion CLI launcher pins redirected stdin encoding
    - companion CLI launcher uses UTF-8 for redirected streams

- `dotnet run --project src\MCP_Rhino.Server -- rhino-claude-code-companion-ui-smoke-test`
  - Exit code: 0

- `artifacts\llm-panel-cli-utf8\MCP_Rhino.Server.exe llm-panel-cli-switching-smoke-test`
  - Exit code: 0

### Recompiled Bundle

- `artifacts\llm-panel-cli-utf8\MCP_Rhino.Server.rhp`
  - Size: 917504 bytes
  - Last write: 2026-05-06 14:56:59
- `artifacts\llm-panel-cli-utf8\MCP_Rhino.Companion.exe`
  - Size: 143360 bytes
  - Last write: 2026-05-06 14:56:58
- `artifacts\llm-panel-cli-utf8\MCP_Rhino.Bridge.exe`
  - Size: 142848 bytes

### Follow-up Conclusion

The remaining Codex failure was stdin encoding. The compiled artifact bundle now writes prompt stdin as UTF-8, which matches Codex CLI's requirement.

## Normal Release Rebuild After Closing Rhino (2026-05-06)

### Cleanup

- Removed temporary bundle `artifacts\llm-panel-cli-utf8`.
- Stopped stale bridge process:
  - `MCP_Rhino.Bridge` PID `10676`
- Ran `dotnet clean .\MCP_Rhino.sln -c Release`.
  - First clean had bridge file-delete warnings due to the stale bridge process.
  - Second clean after stopping the bridge completed with 0 warnings and 0 errors.

### Rebuild

- `dotnet build .\MCP_Rhino.sln -c Release`
  - Exit code: 0
  - Result: 0 warnings, 0 errors

### Rebuilt Normal Release Outputs

- `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.rhp`
  - Size: 917504 bytes
- `src\MCP_Rhino.Companion\bin\Release\net8.0-windows\MCP_Rhino.Companion.exe`
  - Size: 143360 bytes
- `src\MCP_Rhino.Bridge\bin\Release\net8.0\MCP_Rhino.Bridge.exe`
  - Size: 142848 bytes

### Verification From Normal Release Output

- `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.exe llm-panel-cli-switching-smoke-test`
  - Exit code: 0
  - UTF-8 stdin, Codex `.cmd`, provider model, and frontend/host IPC assertions passed.
- `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.exe rhino-claude-code-companion-ui-smoke-test`
  - Exit code: 0

## Follow-up Fix: Codex Item Events And Cleanup Noise (2026-05-06)

### Trigger

Codex ran and returned assistant text, but the panel displayed:

- `codex item not rendered: item.started`
- `codex item not rendered: item.completed`
- `codex (non-JSON): SUCCESS: The process with PID ... has been terminated.`

This showed that Codex was running, but the companion parser did not understand Codex's item lifecycle/tool-call shape and was showing expected process cleanup as diagnostics.

### Additional Execution

- Updated `CodexCliSession` to recognize tool-call payloads where the `item` itself is the tool call, including `mcp_tool_call`, `tool`, `function_call`, and command-shaped items.
- Lifecycle-only `item.*` events without renderable text or tool data are now ignored instead of shown as diagnostics.
- Codex cleanup lines and plugin/analytics warning noise are filtered from non-JSON output.
- Added `-a never` to Codex exec arguments so the non-interactive panel cannot hang behind an approval prompt. Sandbox remains `read-only`.

### Additional Test Record

- `dotnet build .\MCP_Rhino.sln -c Release`
  - Exit code: 0
  - Result: 0 warnings, 0 errors

- `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.exe llm-panel-cli-switching-smoke-test`
  - Exit code: 0
  - Added assertions:
    - Codex exec runs with non-interactive approval policy
    - Codex item payloads can be rendered when the item itself is the tool call
    - Codex MCP tool-call item type is recognized
    - Codex non-JSON cleanup noise is filtered

- `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.exe rhino-claude-code-companion-ui-smoke-test`
  - Exit code: 0

### Updated Normal Release Outputs

- `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.rhp`
  - Size: 918016 bytes
- `src\MCP_Rhino.Companion\bin\Release\net8.0-windows\MCP_Rhino.Companion.exe`
  - Size: 143360 bytes
- `src\MCP_Rhino.Bridge\bin\Release\net8.0\MCP_Rhino.Bridge.exe`
  - Size: 142848 bytes

### Follow-up Conclusion

The remaining visible Codex issue was event parsing and diagnostic filtering, not CLI startup. The rebuilt Release panel should now render Codex MCP tool events and suppress expected Codex process cleanup lines.

## Follow-up Fix: Codex Approval Flag Compatibility (2026-05-06)

### Trigger

The installed Codex CLI reported:

- `codex: error: unexpected argument '-a' found`
- `Codex exited with code 2`

Local `codex exec --help` showed `codex-cli 0.128.0` supports `-c/--config`, `-s/--sandbox`, and `--skip-git-repo-check`, but does not support the `-a` shorthand.

### Additional Execution

- Replaced `-a never` with the Codex config override `approval_policy='never'`.
- Kept `-s read-only` so shell execution stays constrained while the panel remains non-interactive.
- Changed the bound Codex system-context header to ASCII text.
- Added suppression for Codex cleanup noise shaped as `ERROR: The process "..." not found.`

### Additional Test Record

- `codex debug prompt-input -c "approval_policy='never'" ...`
  - Exit code: 0
  - Result: installed Codex accepted the approval config override and MCP config overrides.

- `dotnet build .\MCP_Rhino.sln -c Release`
  - Exit code: 0
  - Result: 0 warnings, 0 errors

- `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.exe llm-panel-cli-switching-smoke-test`
  - Exit code: 0
  - Added assertions:
    - Codex exec uses `approval_policy='never'`
    - Codex exec does not emit unsupported `-a`
    - Codex missing-process cleanup noise is filtered

- `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.exe rhino-claude-code-companion-ui-smoke-test`
  - Exit code: 0

### Updated Normal Release Outputs

- `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.rhp`
  - Size: 918528 bytes
  - Last write: 2026-05-06 15:15:20
- `src\MCP_Rhino.Companion\bin\Release\net8.0-windows\MCP_Rhino.Companion.exe`
  - Size: 143360 bytes
  - Last write: 2026-05-06 15:15:19
- `src\MCP_Rhino.Bridge\bin\Release\net8.0\MCP_Rhino.Bridge.exe`
  - Size: 142848 bytes
  - Last write: 2026-05-06 15:01:24

### Follow-up Conclusion

The immediate failure was caused by using a Codex approval shorthand not present in the installed CLI. The rebuilt Release panel now passes the approval policy through Codex config instead, so `codex exec` should get past argument parsing and reach the Rhino MCP query.

## Follow-up Fix: Codex MCP Approval Metadata And Status (2026-05-06)

### Trigger

Codex could see and invoke the Rhino MCP tool, but read-only Rhino queries returned:

`{ "message": "user cancelled MCP tool call" }`

The panel also kept the MCP indicator non-green in Codex mode and later reported:

`Codex stream error: The requested operation requires an element of type 'Object', but the target element has type 'String'.`

### Additional Execution

- Marked `FilterObjectsByUserAttributesTool` with MCP safety annotations:
  - `ReadOnly = true`
  - `Destructive = false`
  - `OpenWorld = false`
- Added object-shape guards to `CodexCliSession` so Codex JSONL events with string-shaped payloads do not throw while being rendered.
- Updated the Codex prompt context to forbid local shell or direct `.3dm` file fallbacks after Rhino MCP failures.
- Updated the frontend MCP status handling so Codex `ready` / `configured` status keeps the MCP indicator green. Codex uses one-shot `exec` sessions and does not emit the same persistent connected stream shape as Claude Code.

### Additional Test Record

- `dotnet build .\MCP_Rhino.sln -c Release`
  - Exit code: 0
  - Result: 0 warnings, 0 errors

- `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.exe llm-panel-cli-switching-smoke-test`
  - Exit code: 0
  - Added assertions:
    - frontend preserves MCP connected state across provider status updates
    - frontend treats provider-ready status as MCP available for Codex
    - Codex parser guards object-only JSON access
    - Codex prompt blocks shell fallback after MCP failures
    - user-attribute filter advertises read-only, non-destructive, closed-world MCP safety

- `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.exe rhino-claude-code-companion-ui-smoke-test`
  - Exit code: 0

- `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.exe rhino-claude-code-panel-smoke-test`
  - Exit code: 0

### Updated Normal Release Outputs

- `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.rhp`
  - Size: 920064 bytes
  - Last write: 2026-05-06 15:26:32
- `src\MCP_Rhino.Companion\bin\Release\net8.0-windows\MCP_Rhino.Companion.exe`
  - Size: 143360 bytes
  - Last write: 2026-05-06 15:26:31
- `src\MCP_Rhino.Bridge\bin\Release\net8.0\MCP_Rhino.Bridge.exe`
  - Size: 142848 bytes
  - Last write: 2026-05-06 15:01:24

### Follow-up Conclusion

The non-green MCP flag in Codex mode was a panel status interpretation issue, not proof that Codex lacked MCP access. The log showed Codex reached the Rhino MCP tool. The failing query path was caused by missing MCP safety metadata on a read-only tool plus a Codex JSONL parser assumption. The rebuilt Release output addresses both.
