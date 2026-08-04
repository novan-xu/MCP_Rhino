# 260506_PLAN_multi-rhino-panel-pipes

## Background

Two Rhino processes can load the MCP_Rhino plug-in at the same time, but the current transport
names are not process-scoped:

- the Developer Debug Control Path always uses `\\.\pipe\mcp_rhino`
- panel-bound chat pipes use `mcp_rhino_<RuntimeSerialNumber>`

`RuntimeSerialNumber` is only safe inside one Rhino process. A second Rhino instance can collide
with the same global debug pipe, and potentially with the same panel-bound runtime serial pipe.
The observed error is:

```text
System.IO.IOException: All pipe instances are busy.
```

## Goal

- Allow multiple Rhino instances to run in parallel.
- Allow each Rhino instance to use its own LLM chat panel / companion session.
- Keep the original `\\.\pipe\mcp_rhino` connection mode as the Developer Debug Control Path for
  test and smoke workflows.
- Update project guidelines so future work preserves this split.

## Architecture Ownership

- `Infrastructure/Plugin`: owns pipe naming, server lifetime, and Rhino plug-in startup.
- `Infrastructure/Plugin/Panel`: owns Rhino-hosted panel session pipe selection.
- `Infrastructure/Plugin/Companion`: owns standalone companion launch pipe selection.
- `Infrastructure/ClaudeCode` and `MCP_Rhino.Companion`: own temporary MCP config paths.
- `Project_Guides`: owns durable execution-mode requirements.

## Key Design

- Introduce one pipe naming helper:
  - debug/test pipe: `mcp_rhino`
  - panel-bound pipe: `mcp_rhino_<ProcessId>_<RuntimeSerialNumber>`
- Keep `MCP_Rhino.Bridge.exe` behavior unchanged:
  - no `--pipe` means connect to `mcp_rhino`
  - `--pipe <name>` means connect to the explicit panel-bound pipe
- Treat `mcp_rhino` as a single-owner debug path. If a second Rhino instance cannot bind it, log a
  clear message once and continue so process-scoped panel pipes still work.
- Make temporary `.mcp.json` directories pipe-name-scoped instead of runtime-serial-only to avoid
  cross-process companion/panel config collisions.

## Involved Files

- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpPipeNames.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpNamedPipeServer.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/ServerBootstrap.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/MCP_Rhino.RhinoPlugin.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/Panel/RhinoChatPanelHost.cs`
- `src/MCP_Rhino.Server/Infrastructure/ClaudeCode/McpConfigBuilder.cs`
- `src/MCP_Rhino.Companion/McpConfigBuilder.cs`
- `src/MCP_Rhino.Companion/ClaudeCodeSession.cs`
- `src/MCP_Rhino.Bridge/Program.cs`
- `Project_Guides/MCP_Rhino Architecture.md`
- `Project_Test/260506_TEST_multi-rhino-panel-pipes/`
- `Project_Exet/260506_EXET_multi-rhino-panel-pipes.md`

## Usage

- Test/debug mode remains:

```powershell
MCP_Rhino.Bridge.exe
```

- Panel/companion mode uses a generated process-scoped pipe:

```text
mcp_rhino_<ProcessId>_<RuntimeSerialNumber>
```

Users still open chat from Rhino with:

```text
_Mcpchat
```

## Acceptance Criteria

- `dotnet build .\MCP_Rhino.sln -c Release` succeeds.
- A new CLI smoke proves generated panel-bound pipe names include the current process id and do
  not equal `mcp_rhino`.
- Existing Claude Code panel/companion and save-safety CLI smokes still pass.
- Architecture guide documents that parallel Rhino instances use process-scoped panel-bound pipes
  while `mcp_rhino` remains a single-owner debug/test pipe.

## Risks And Rollback

- Existing external clients that manually connect to old panel pipe names
  `mcp_rhino_<RuntimeSerialNumber>` must update to the generated config. The companion/panel path
  generates this config automatically, so normal UI use is unaffected.
- Rollback means restoring runtime-serial-only panel pipe names, which reintroduces cross-process
  collisions.

## Future Work

- Add a true live Rhino two-process smoke when automated Rhino orchestration is available.
- Consider a discovery command that lists active process-scoped panel pipes for diagnostics.
