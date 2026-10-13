# 260507_PLAN_panel-runtime-policy

## Background

Panel-launched LLM sessions run from an isolated temporary workspace rather than the repository root, so they do not automatically inherit `AGENTS.md`, `Runtime_Workflow/`, or `Project_Guides/`. The user confirmed that chat panels will be used for runtime tasks only and approved an English runtime policy bundle that permits bound Rhino work plus explicit user-approved external runtime files, spreadsheets, and slides.

## Goals

- Add the approved English runtime policy bundle as a prompt artifact.
- Inject the same policy into standalone Companion Claude Code sessions, standalone Companion Codex sessions, and the Rhino-hosted fallback panel.
- Ensure repository-workspace / test-route sessions do not receive the panel runtime policy as injected prompt context.
- Keep the panel isolated from repository construction rules and filesystem authority.
- Preserve the existing bound-document prompt details.

## Architecture Ownership

- Prompt content belongs under `src/MCP_Rhino.Server/Prompts/Runtime/`.
- Companion and fallback panel session launchers load the runtime policy from copied prompt content.
- Existing panel-bound MCP execution semantics remain unchanged.

## Key Design

- Store one canonical Markdown policy file:
  - `src/MCP_Rhino.Server/Prompts/Runtime/McpRhinoRuntimePolicyBundle.md`
- Copy that file to both Server and Companion build outputs.
- Add small prompt-loader helpers in Server fallback panel and Companion code.
- Append the policy to each generated bound-document system prompt.
- Do not grant repository workspace access or add construction instructions to panel sessions.

## Involved Files

- `src/MCP_Rhino.Server/Prompts/Runtime/McpRhinoRuntimePolicyBundle.md`
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`
- `src/MCP_Rhino.Companion/MCP_Rhino.Companion.csproj`
- `src/MCP_Rhino.Companion/ClaudeCodeSession.cs`
- `src/MCP_Rhino.Companion/CodexCliSession.cs`
- `src/MCP_Rhino.Companion/RuntimePolicyPrompt.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/Panel/PanelChatSessionService.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/Panel/RuntimePolicyPrompt.cs`

## Usage

When `_Mcpchat` launches the standalone Companion or the fallback Rhino-hosted panel, the launched LLM receives bound-document context followed by the runtime policy bundle. Runtime tasks remain routed through available tools; missing external spreadsheet, slide, or connector capability is reported as a runtime gap.

## Acceptance Criteria

- The runtime policy bundle is English-only.
- The policy appears in generated Claude Code and Codex panel prompts.
- The policy file is copied to both Server and Companion output directories.
- Solution build passes.
- A smoke check verifies prompt loading and key policy text.
- A smoke check verifies that `Program.cs`, `DeveloperCommandHandler`, and `MCP_Rhino.Bridge` do not load or inject the panel runtime policy.

## Risks And Rollback

- Risk: missing copied prompt file could silently remove panel policy context.
- Mitigation: loaders return a clear fallback marker if the file is missing, and smoke verifies the copied policy exists.
- Rollback: remove the policy file, loader helpers, csproj content includes, and appended prompt calls.

## Future Extensions

- Add dedicated spreadsheet / slide connectors to panel MCP configuration if those workflows become common.
- Surface available external-file connectors in panel status so the model can distinguish capability gaps earlier.
