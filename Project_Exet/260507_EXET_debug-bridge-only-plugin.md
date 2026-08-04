# 260507_EXET_debug-bridge-only-plugin

## Corresponding Plan

- Plan: `Project_Plan/260507_PLAN_debug-bridge-only-plugin.md`
- Execution date: 2026-05-07

## Associated Artifacts

- Test folder: `Project_Test/260507_TEST_debug-bridge-only-plugin/`
- Smoke source: `Project_Test/260507_TEST_debug-bridge-only-plugin/DeveloperCommandHandler.DebugBridgeOnlyPluginSmokeTest.cs`
- Rhino smoke command: `src/MCP_Rhino.Server/Infrastructure/Plugin/McpDebugBridgeOnlyPluginSmokeCommand.cs`
- Commit / PR: not created in this workspace session.

## Execution Result / Actual Scope

- Added a Debug-only `MCP_RHINO_BRIDGE_PIPE_ONLY` compile symbol in `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`.
- Kept the existing `CopyRhinoPluginAssembly` target so both Debug and Release builds copy `MCP_Rhino.Server.dll` to `MCP_Rhino.Server.rhp` under their own `bin/<Configuration>/net8.0/` folders.
- Updated `McpRhinoPlugin` so Debug remains bridge-pipe-only:
  - starts the Developer Debug Control Path pipe
  - rejects panel-bound pipe startup
  - rejects `_Mcpchat` / chat panel startup
  - skips Companion document-close lifecycle wiring
- Left Release chat-capable while preserving the same Developer Debug Control Path.
- Added a CLI smoke test and dedicated Rhino command to verify Debug / Release plugin mode markers and output `.rhp` files.
- Documented the build-mode contract in `Project_Guides/MCP_Rhino Architecture.md`, `Runtime_Workflow/MCP_Rhino Workflow.md`, and `README.md`.

## Deviations From Plan

- No direct code change was needed in `McpChatCommand`; it already delegates to `McpRhinoPlugin.TryShowChatPanel`, so the bridge-only guard belongs in the plugin.
- The build-mode flag is exposed as a static property instead of a `const` to avoid compile-time unreachable-code warnings with `TreatWarningsAsErrors`.

## Issues Found And Fixed During Execution

- Debug and Release had previously built the same chat-capable `.rhp`. The Debug compile symbol and plugin guards now make the Debug output explicitly bridge-pipe-only.
- Future drift risk was addressed by adding a smoke test that checks both output assemblies and verifies both configurations still retain the Developer Debug Control Path marker.

## Test Record

```powershell
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- debug-bridge-only-plugin-smoke-test
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-tool-safety-annotations-smoke-test
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-tool-safety-annotations-smoke-test
dotnet build .\MCP_Rhino.sln -c Debug
dotnet build .\MCP_Rhino.sln -c Release
```

Results:

- Debug Server build: passed, 0 warnings, 0 errors.
- Release Server build: passed, 0 warnings, 0 errors.
- `debug-bridge-only-plugin-smoke-test`: passed.
- Debug tool safety smoke: passed, 78 MCP tools checked.
- Release tool safety smoke: passed, 78 MCP tools checked.
- Debug solution build: passed, 0 warnings, 0 errors.
- Release solution build: passed, 0 warnings, 0 errors.

Key smoke output:

```text
[OK] Debug .rhp is built and marked bridge-pipe-only.
[OK] Release .rhp is built and remains chat-capable.
[OK] Debug and Release both retain the Developer Debug Control Path marker.
```

## Acceptance Criteria Alignment

- Debug build produces `src/MCP_Rhino.Server/bin/Debug/net8.0/MCP_Rhino.Server.rhp`.
- Release build produces `src/MCP_Rhino.Server/bin/Release/net8.0/MCP_Rhino.Server.rhp`.
- Debug defines `MCP_RHINO_BRIDGE_PIPE_ONLY` and blocks chat / panel-bound startup.
- Release remains chat-capable and keeps the debug bridge pipe.
- Both configurations pass the MCP tool safety annotation smoke with the same 78-tool count.

## Rollback Verification

Rollback is limited to removing the Debug-only compile symbol and the `IsBridgePipeOnlyBuild` guards from `McpRhinoPlugin`, then rebuilding Debug and Release. The shared tool registration surface is unchanged.

## Current Remaining Items

- No blocking remaining items.
- Manual Rhino verification can load the Debug `.rhp` and run external MCP through `MCP_Rhino.Bridge.exe`, then load the Release `.rhp` and run `_Mcpchat`.

## Conclusion

The Debug output now preserves the old bridge-pipe-only Rhino plugin behavior, the Release output remains the chat-panel plugin, and both remain compatible at the MCP bridge/tool surface.
