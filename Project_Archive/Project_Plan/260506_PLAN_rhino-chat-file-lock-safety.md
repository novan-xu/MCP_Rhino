# 260506_PLAN_rhino-chat-file-lock-safety

## Background

After Claude Code modified a Rhino document from the MCP_Rhino panel, Rhino could not save
`<LOCAL_TEST_MODEL_PATH>`.
The file was not marked read-only, but an exclusive open check failed. Windows Restart Manager
reported the blocking process tree as `MCP_Rhino.Companion.exe`, `claude.exe`, and
`MCP_Rhino.Bridge.exe`.

The panel currently starts Claude/Codex sessions with the Rhino model folder as the process
working directory. That makes the folder containing the active `.3dm` a CLI project workspace and
allows child tools/session infrastructure to interact with the model folder while Rhino is trying
to save.

## Goal

- Stop Claude, Codex, and MCP bridge child processes from using the `.3dm` folder as their working
  directory.
- Prevent the Rhino-to-companion process launch from inheriting Rhino's open `.3dm` file handle.
- Keep the bound document path available only as MCP prompt context and tool input, not as process
  workspace.
- Harden Claude panel sessions so local file/shell/project tools are disabled and Rhino work must
  go through MCP tools.
- Fix the object edit applier to use the bound `RhinoDoc` supplied by the live accessor instead of
  `RhinoDoc.ActiveDoc`.
- Add a regression smoke that catches these save-lock hazards at source level.

## Architecture Ownership

- `src/MCP_Rhino.Companion`: owns current companion CLI process launch for Claude and Codex.
- `src/MCP_Rhino.Server/Infrastructure/Plugin/Panel`: owns the older in-process panel session
  launch path.
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live`: owns RhinoCommon live document mutations.
- `Project_Test/260506_TEST_rhino-chat-file-lock-safety`: owns the focused regression smoke.

## Key Design

- Add isolated per-pipe working directories under the existing MCP_Rhino temp root.
- Start Claude and Codex from those temp directories instead of `Path.GetDirectoryName(documentPath)`.
- Start the companion from Rhino with `UseShellExecute = true` so Windows does not inherit Rhino's
  document file handles into the companion process tree.
- Keep MCP config files under per-pipe temp directories as already intended.
- Add Claude flags `--no-session-persistence`, `--setting-sources user`, and `--tools ""` while
  preserving the MCP config. Do not use `--bare` by default because it disables OAuth/keychain auth
  and can break subscription-based Claude Code installs.
- Expand Claude system prompts to explicitly forbid local shell/file `.3dm` fallbacks.
- Pass the resolved `RhinoDoc` into `IObjectEditOperationApplier.Apply`.

## Involved Files

- `src/MCP_Rhino.Companion/ClaudeCodeSession.cs`
- `src/MCP_Rhino.Companion/CodexCliSession.cs`
- `src/MCP_Rhino.Companion/CompanionWorkspace.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/Companion/CompanionProcessLauncher.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/Panel/PanelChatSessionService.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/Panel/PanelChatWorkspace.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IObjectEditOperationApplier.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IGeometryMutator.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoObjectEditingService.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoGeometryModificationService.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoObjectEditOperationApplier.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoGeometryMutator.cs`
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs`
- `Project_Guides/MCP_Rhino Architecture.md`
- `Project_Test/260506_TEST_rhino-chat-file-lock-safety/`
- `Project_Exet/260506_EXET_rhino-chat-file-lock-safety.md`

## Usage

After rebuilding and reloading the `.rhp`, `_Mcpchat` starts the companion normally. Claude/Codex
still receive the bound document path in the prompt and MCP tool calls, but their process working
directory is isolated from the model folder.

## Acceptance Criteria

- The active desktop test file is shown as not read-only, and the lock owner diagnosis identifies
  the companion/Claude/bridge process tree as the save blocker.
- `dotnet build .\MCP_Rhino.sln -c Release` succeeds.
- `rhino-chat-file-lock-safety-smoke-test` succeeds.
- `rhino-chat-save-safety-smoke-test` still succeeds.
- Source inspection confirms Claude/Codex/panel sessions no longer use the document folder as
  working directory.
- Source inspection confirms `LiveRhinoObjectEditOperationApplier` no longer references
  `RhinoDoc.ActiveDoc`.

## Risks And Rollback

- Claude project history will no longer be grouped under the model folder. This is intentional for
  save safety.
- If `--tools ""` blocks an expected local Claude helper, the panel should still function through
  MCP tools. Roll back only that flag if a specific required MCP behavior is proven blocked.
- Rollback for the workspace isolation is limited to restoring document-folder working directories,
  but that reintroduces the save-lock class of failures.

## Future Work

- Add a Rhino live command that checks document save state and lock owners from inside Rhino.
- Add a panel command to stop/restart only the companion child process tree without closing Rhino.
