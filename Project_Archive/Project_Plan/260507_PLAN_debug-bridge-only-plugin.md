# 260507_PLAN_debug-bridge-only-plugin

## Background

The Server project already copies `MCP_Rhino.Server.dll` to `MCP_Rhino.Server.rhp` for each build configuration, but Debug and Release currently build the same chat-capable plugin behavior. The user wants the Debug plugin in `bin/Debug` to remain the old bridge-pipe-only Rhino plugin used by external MCP clients, while Release remains the chat-panel plugin. Both builds must remain compatible at the MCP tool/protocol level.

## Goals

- Ensure `src/MCP_Rhino.Server/bin/Debug/net8.0/MCP_Rhino.Server.rhp` is produced whenever Debug is built.
- Make the Debug plugin bridge-pipe-only:
  - start the Developer Debug Control Path pipe
  - do not open Companion
  - do not open Rhino-hosted chat panel
  - do not start panel-bound per-document pipes
- Keep the Release plugin chat-capable.
- Verify Debug and Release expose the same MCP tool count/safety annotations.

## Architecture Ownership

- Build configuration lives in `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`.
- Rhino plugin runtime gating lives in `src/MCP_Rhino.Server/Infrastructure/Plugin/`.
- Verification lives in `Project_Test/260507_TEST_debug-bridge-only-plugin/`.

## Key Design

- Add a Debug-only compile symbol: `MCP_RHINO_BRIDGE_PIPE_ONLY`.
- Expose a small internal build-mode constant on `McpRhinoPlugin`.
- In Debug bridge-only mode:
  - block `_Mcpchat` through `TryShowChatPanel`
  - block `StartBoundPipeServer`
  - skip companion document-close lifecycle subscription
  - log that the Debug plugin is bridge-only
- Leave Release behavior unchanged.
- Add a smoke test that builds both configurations and checks output files plus source/build-mode guards.

## Involved Files

- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/MCP_Rhino.RhinoPlugin.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpChatCommand.cs`
- `Project_Test/260507_TEST_debug-bridge-only-plugin/`

## Usage

- Load `src/MCP_Rhino.Server/bin/Debug/net8.0/MCP_Rhino.Server.rhp` in Rhino for bridge-pipe-only MCP testing through `MCP_Rhino.Bridge.exe` and `\\.\pipe\mcp_rhino`.
- Load `src/MCP_Rhino.Server/bin/Release/net8.0/MCP_Rhino.Server.rhp` in Rhino for chat-panel / Companion runtime use.

## Acceptance Criteria

- Debug build produces `MCP_Rhino.Server.rhp`.
- Release build produces `MCP_Rhino.Server.rhp`.
- Debug build has `MCP_RHINO_BRIDGE_PIPE_ONLY` defined.
- Debug plugin blocks chat panel and panel-bound pipe startup.
- Release plugin does not define `MCP_RHINO_BRIDGE_PIPE_ONLY` and remains chat-capable.
- Debug and Release MCP tool surfaces remain compatible.

## Risks And Rollback

- Risk: Debug-only preprocessor behavior could drift from Release tool registrations.
- Mitigation: keep only plugin UI/panel launch behavior conditional; keep service/tool registration shared.
- Rollback: remove the compile symbol and conditional runtime guards.

## Future Extensions

- Add a packaged output convention if users need side-by-side named plugin folders for Debug and Release deployment.
