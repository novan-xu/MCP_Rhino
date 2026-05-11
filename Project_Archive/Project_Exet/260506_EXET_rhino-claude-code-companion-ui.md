# 260506_EXET_rhino-claude-code-companion-ui

## 对应计划

- Plan: `Project_Plan/260506_PLAN_rhino-claude-code-companion-ui.md`
- Execute date: 2026-05-06

## 关联产物

- Test folder: `Project_Test/260506_TEST_rhino-claude-code-companion-ui/`
- Commit / PR: not created in this working session.

## 执行结果 / 实际落地范围

- Added standalone WPF + WebView2 companion app:
  - `src/MCP_Rhino.Companion/`
  - `wwwroot/index.html`, `styles.css`, `app.js`
  - Claude Code stream-json session wrapper, event mapper, MCP config builder, and CLI arg parser.
- Added Rhino plugin launcher path:
  - `_Mcpchat` first tries to open/focus the standalone companion for the saved active document.
  - Each companion is bound to `mcp_rhino_<RuntimeSerialNumber>`.
  - Existing Rhino-hosted Eto panel remains as fallback when companion/bridge is unavailable.
- Added companion process lifecycle support:
  - `CompanionLaunchSpec`
  - `CompanionProcessLauncher`
  - `CompanionSessionHandle`
- Added test and command entry points:
  - CLI slug: `rhino-claude-code-companion-ui-smoke-test`
  - Rhino command: `_McpRhinoClaudeCodeCompanionUiSmoke`
- Added `MCP_Rhino.Companion` to `MCP_Rhino.sln`.

## 与计划的偏差

- The standalone companion app was implemented as a repo-owned WPF/WebView2 shell, not by embedding any existing VS Code extension UI.
- The smoke verifies launch contracts, executable discovery, config generation, bound accessor behavior, and bound pipe start/stop. It does not automate a real Claude Code conversation through WebView2.
- Rhino-hosted live smoke was not executed in this shell; it must be run inside Rhino with a saved active document.

## 施工中发现并修复的问题

- Fixed missing explicit `System.IO` imports in the WPF project where `TreatWarningsAsErrors` surfaced unresolved BCL types.
- Renamed the `CompanionUiEvent.Status(...)` factory to `SessionStatus(...)` to avoid a record member name collision with the `Status` property.
- Tightened nullable flow in `App.xaml.cs` after argument parsing.

## 测试记录

```powershell
dotnet build src\MCP_Rhino.Bridge\MCP_Rhino.Bridge.csproj -c Release --nologo
```

- Exit code: 0
- Result: build succeeded, 0 warnings, 0 errors.

```powershell
dotnet build src\MCP_Rhino.Companion\MCP_Rhino.Companion.csproj -c Release --nologo
```

- Exit code: 0
- Result: build succeeded, 0 warnings, 0 errors.

```powershell
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --nologo
```

- Exit code: 0
- Result: build succeeded, 0 warnings, 0 errors.

```powershell
dotnet build MCP_Rhino.sln -c Release --nologo
```

- Exit code: 0
- Result: Bridge, Companion, and Server all built successfully with 0 warnings and 0 errors.

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- rhino-claude-code-companion-ui-smoke-test
```

- Exit code: 0
- Key output:
  - `[OK] CLI mode keeps the Rhino live accessor disabled.`
  - `[OK] Per-document MCP config JSON is parseable and contains the bound pipe name.`
  - `[OK] Claude Code availability probe completed: Claude Code 2.1.119 is available.`
  - `[OK] MCP_Rhino.Companion.exe accepts the command-line contract used by _Mcpchat.`

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- rhino-claude-code-panel-smoke-test
```

- Exit code: 0
- Existing panel smoke still passes, including Bridge `--pipe` help parsing.

## 验收判据对齐

- `_Mcpchat` now has a standalone companion path before the Rhino-hosted fallback panel.
- Companion launch arguments carry the exact document path, runtime serial, per-doc pipe, and bridge executable path.
- Bound-mode instructions tell Claude Code not to ask for a `.3dm` path and to treat the companion window as bound to exactly one saved Rhino document.
- CLI smoke confirms the companion process accepts the `_Mcpchat` launch contract.

## 回退验证

- No rollback command was executed.
- Practical rollback is scoped to making `TryShowChatPanel` call `TryShowEtoPanel` directly again, leaving the new companion project unused.

## 当前遗留项

- Run `_McpRhinoClaudeCodeCompanionUiSmoke` inside Rhino 8 with a saved active document.
- Manually run `_Mcpchat`, send a real prompt such as `List the current layers.`, and confirm stream-json messages/tool cards render as intended.
- Confirm document-close cleanup with two saved Rhino documents open in parallel.

## 结论

The companion UI implementation is in place and passes solution build plus CLI smoke verification.
The remaining verification is live Rhino behavior and real Claude Code streaming inside the
standalone companion window.
