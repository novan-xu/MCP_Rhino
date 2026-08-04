# LLM Panel CLI Switching Plan

## Background

The companion LLM panel can switch between Claude Code CLI and Codex CLI, but three related issues were observed:

- Codex startup fails on Windows when the discovered executable is an npm `.cmd` shim such as `<CODEX_CLI_PATH>`.
- Claude Code can leave the panel input disabled after the backing process is not running.
- The WebView settings panel keeps showing Claude model choices after switching to Codex, and model selections are not handled by the host.

## Goal

Make the companion panel provider switch reliable:

- Windows `.cmd` CLI launch paths must execute through `cmd.exe` without treating a quoted path as the literal command name.
- Backend CLI and selected model must be host-owned state, not frontend-only state.
- Model choices must update when the active backend switches.
- Selected models must be passed to newly started Claude Code or Codex sessions.

## Architecture Ownership

- Companion WPF host: provider/model session state and session restart behavior.
- Companion CLI infrastructure: process start info construction for `.exe` and `.cmd` CLIs.
- Companion WebView assets: provider-specific model rendering and IPC messages.
- Server CLI test hook: smoke validation for process-launch command script handling and static UI contract checks.

## Key Design

- Add a provider catalog for supported CLI IDs and default model lists.
- Extend `CompanionUiEvent` session events with active CLI and available models.
- Recreate the active session when either CLI or model changes, because Claude Code is a persistent process and receives `--model` only at process start.
- Replace the fragile `.cmd` command string wrapper with `cmd.exe /d /c call <script> <args...>` using `ArgumentList`.
- Keep Codex MCP config overrides free of embedded double quotes when possible by using TOML literal strings.

## Involved Files

- `src/MCP_Rhino.Companion/*`
- `src/MCP_Rhino.Companion/wwwroot/app.js`
- `src/MCP_Rhino.Server/Infrastructure/ClaudeCode/ClaudeCodeAvailability.cs`
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs`
- `Project_Test/260506_TEST_llm-panel-cli-switching/`

## Usage

The user opens the companion LLM panel, chooses `claude code cli` or `codex cli`, then selects a model from the provider-specific list. `Default` means the provider's own configured default model.

## Acceptance Criteria

- Codex `.cmd` availability/startup no longer reports the quoted path as an unrecognized command.
- Switching to Codex updates the model picker to Codex/OpenAI model IDs.
- Switching back to Claude updates the model picker to Claude model IDs.
- Model changes are sent to the host and applied to the next backend session.
- Existing Claude Code mode and named pipe MCP mode remain available.
- `dotnet build .\MCP_Rhino.sln -c Release` passes.
- `llm-panel-cli-switching-smoke-test` passes.

## Risks And Rollback

- Restarting a Claude Code session on model change resets that provider process. This is expected because `--model` is process-start configuration.
- If a user depends on a custom model ID not in the built-in list, the catalog may need a future editable model field.
- Rollback is limited to companion provider/model state and the CLI process helper.

## Future Extensions

- Read Codex model suggestions from local Codex config/profile data.
- Add a custom model text input.
- Expose provider health diagnostics in the settings panel.
