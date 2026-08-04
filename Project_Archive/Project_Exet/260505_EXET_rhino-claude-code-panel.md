# 260505_EXET_rhino-claude-code-panel

## 对应计划

- Plan: `Project_Plan/260505_PLAN_rhino-claude-code-panel.md`
- Execute date: 2026-05-05

## 关联产物

- Test folder: `Project_Test/260505_TEST_rhino-claude-code-panel/`
- Commit / PR: not created in this working session.

## 执行结果 / 实际落地范围

- Added panel-bound live document access:
  - `BoundLiveRhinoDocumentAccessor`
  - `LiveRhinoDocumentAccessorBase`
  - `ILiveRhinoDocumentAccessorFactory`
  - `LiveRhinoDocumentAccessorFactory`
- Added `BoundHostFactory` and `ServerBootstrap.StartBoundPipeServer(...)` /
  `StopBoundPipeServer(...)` so isolated ALC can host per-doc named pipe MCP servers.
- Updated `MCP_Rhino.Bridge` with `--pipe <name>` while preserving default `mcp_rhino`
  behavior for Developer Debug Control Path.
- Added default-ALC panel / Claude Code skeleton:
  - `Infrastructure/Plugin/Panel/*`
  - `Infrastructure/ClaudeCode/*`
  - chat domain enums/models
- Updated plugin startup to:
  - keep the global `\\.\pipe\mcp_rhino` server
  - register the Claude Code panel
  - start `PerDocumentPanelDispatcher`
  - clean up dispatcher before isolated ALC shutdown
- Added `rhino-claude-code-panel-smoke-test` CLI slug and `_McpRhinoClaudeCodePanelSmoke`
  Rhino command.
- Added test README and sample MCP config JSON.
- Updated `Project_Guides/MCP_Rhino Architecture.md` with:
  - corrected panel-bound `FilePath` semantics in §通用契约
  - `Developer Debug Control Path（Global Pipe）`
  - `Panel-bound execution mode（Per-Document）`

## 与计划的偏差

- Rhino-hosted `PanelType.PerDoc` proof was not executed in this shell. The code compiles
  against Rhino 8 APIs, but the required two-document panel instance proof must still be run
  inside Rhino.
- The panel UI is a v1 skeleton: text transcript, input box, model dropdown, and simplified
  tool-call line rendering. The planned custom `Drawable` tool-call card and full model-switch
  confirmation UX are not complete in this pass.
- The live smoke command currently verifies bound accessor path rewriting and start/stop of a
  temporary bound pipe. It does not yet run a full MCP `initialize` + `tools/list` handshake or
  programmatically close the bound document to assert `DOCUMENT_CLOSED`.
- True Claude Code multi-turn stream-json behavior was not exercised here; the wrapper and parser
  are implemented, while manual Rhino + Claude Code verification remains required.

## 施工中发现并修复的问题

- `Eto.Forms.Panel` conflicted with the project namespace `Infrastructure.Plugin.Panel`.
  Fixed by fully qualifying Eto panel base classes.
- The plan self-review found that Architecture §通用契约 still had the old panel-bound
  `FilePath` matching rule. The guide now matches bound accessor rewrite semantics.
- Bridge option parsing initially needed the default pipe constant to live inside the options
  record rather than top-level program scope.

## 测试记录

```powershell
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --nologo
```

- Exit code: 0
- Result: build succeeded, 0 warnings, 0 errors.
- Artifact verified by build output: `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.dll`
  and `.rhp` copy target.

```powershell
dotnet build src\MCP_Rhino.Bridge\MCP_Rhino.Bridge.csproj -c Release --nologo
```

- Exit code: 0
- Result: build succeeded, 0 warnings, 0 errors.
- Artifact verified by build output: `src\MCP_Rhino.Bridge\bin\Release\net8.0\MCP_Rhino.Bridge.dll`
  and exe output.

```powershell
dotnet run --project src\MCP_Rhino.Server -- rhino-claude-code-panel-smoke-test
```

- Exit code: 0
- Key output:
  - `[OK] CLI mode does not instantiate a live or bound Rhino accessor.`
  - `[OK] McpConfigBuilder generated parseable per-doc MCP config JSON.`
  - `[OK] ClaudeCodeAvailability probe completed: Claude Code 2.1.119 is available.`
  - `[OK] MCP_Rhino.Bridge.exe --pipe other_name --help returns usage with exit code 0.`

```powershell
src\MCP_Rhino.Bridge\bin\Release\net8.0\MCP_Rhino.Bridge.exe --pipe foo --help
```

- Exit code: 0
- Output includes:
  - `Usage: MCP_Rhino.Bridge.exe [--pipe <name>] [--help]`
  - `Developer Debug Control Path`
  - `panel-bound per-document server`

```powershell
JsonDocument.Parse(Project_Test\260505_TEST_rhino-claude-code-panel\samples\test_mcp_config.json)
```

- Exit code: 0
- Parsed `mcpServers.rhino.args[1]` as `mcp_rhino_12345`.

## 验收判据对齐

- Build and static checks passed for Server and Bridge.
- Bridge default behavior is preserved, with optional `--pipe` added.
- CLI fallback smoke passed and does not require Rhino.
- MCP config sample is parseable.
- Architecture guide now documents both debug/global and panel-bound/per-doc paths.
- Rhino-hosted live proof and true Claude Code manual verification remain open.

## 回退验证

- No rollback command was executed.
- Rollback remains scoped:
  - remove plugin dispatcher startup to disable panel generation
  - keep or remove bound accessor / bound host code independently
  - remove Bridge `--pipe` parsing to return to hard-coded `mcp_rhino`

## 当前遗留项

- Run `_McpRhinoClaudeCodePanelSmoke` inside Rhino 8 with a saved active document.
- Run the two-document `PanelType.PerDoc` proof. If it fails, revise plan/design to single system
  panel + per-doc tabs/session list.
- Extend live smoke to run MCP `initialize` + `tools/list` through the temporary bound pipe.
- Exercise a real Claude Code panel session and record:
  - MCP connected status in `init`
  - tool call visible in panel
  - document mutation and Undo behavior
  - document close cleanup of `claude.exe`

## 结论

The first implementation pass is in place and passes build plus CLI fallback verification. The
remaining risk is Rhino-hosted behavior, especially real `PanelType.PerDoc` materialization and
Claude Code stream-json interaction inside Rhino.
