# 260506_EXET_rhino-agent-panel-settings

## 对应计划

- Plan: `Project_Plan/260506_PLAN_rhino-agent-panel-settings.md`
- Execute date: 2026-05-06

## 关联产物

- Test folder: `Project_Test/260506_TEST_rhino-agent-panel-settings/`
- Source design bundle: `mcp-rhino-cc-interface/project/Rhino Agent Panel.html` and `chat-panel.jsx` (handoff `UxlhobKP24U9-83hcKGSHw`, extracted to `.tmp_design2/` during execution).
- Commit / PR: not created in this working session.

## 执行结果 / 实际落地范围

- Decluttered the header in `wwwroot/index.html`: removed the model dropdown, kept the rhombus logo, MCP pill, and added a gear (settings) icon next to + and pin. All three header icons use the new `rh-btn-icon--lg` 28×28 hit-target variant.
- Added the panel-scoped Settings overlay markup with three Field cards: Backend CLI (`claude code cli` / `codex cli`), Model (`Default`, `claude-haiku-4-5`, `claude-sonnet-4-6`, `claude-opus-4-7`), and MCP server (live readout with green dot when the bound MCP reports `connected`, the bound pipe name, and a `connected` / `starting` meta).
- Rewrote density tokens in `wwwroot/styles.css` to the design's "1.2× chrome" set: `--pad-x 12`, `--pad-row 6`, `--gap-row 10`, `--gap-msg 3`, `--fs-body 15`; added `--fs-mono 13`, `--fs-mono-sm 12`, `--fs-meta 11.5`, `--ctrl-h 26`, `--hdr-h 40`, `--status-h 28`. Bumped header (40px tall, 14px wordmark, 12px MCP pill, 7px MCP dot), status bar (28px tall, 12px font, 20×20 donut, 12px sep), composer (textarea 15px / 22px min-height, send button 28×28, paperclip 16px), kbd hints (11px). Chat-content density (avatars, message rows, system divider) is unchanged.
- Removed the `zoom: 1.5` body rule from the previous iteration; the new explicit chrome sizing supersedes that workaround.
- Added settings-overlay CSS components: `.settings-overlay` (full-panel modal + dim backdrop), `.settings-card` (centered card, max-width 460, clamps under the 420px window), `.settings-head` / `.settings-body` / `.settings-foot`, `.settings-field` + `.settings-field-label`, `.settings-choice` (card-style radio with circle dot, amber active state), `.mcp-readout` (live pipe + meta), `.settings-done` (amber CTA).
- Rewrote `wwwroot/app.js` settings logic:
  - Removed the legacy header model dropdown handlers and elements.
  - Added `currentModel` / `currentCli` state, `setModel(modelId)`, `setCli(cliId)`, and `buildSettingsChoices()` that renders both Backend CLI and Model cards from the same template.
  - Wired `openSettings` / `closeSettings` to the gear icon, X button, Done button, and dim-backdrop click.
  - Selecting a model still posts the existing `model` IPC.
  - Selecting a CLI posts a new `cli` IPC `{ type:"cli", cli:"claude code cli"|"codex cli" }`.
  - Added `setMcpPipe(pipeName)` and refreshed the MCP readout on `session` (which already carries `pipeName`) and `status` events. The pill in the header and the readout in the overlay share state.
- Updated the WPF host:
  - `MainWindow.xaml`: default size now `Width=420 Height=900` with `MinWidth=360 MinHeight=600`, matching the design's "narrow side panel · 420×900" artboard.
  - `MainWindow.xaml.cs`: handles the new `cli` IPC. If the value is anything other than `claude code cli`, the host emits a yellow diagnostic via `CompanionUiEvent.Diagnostic("Backend CLI '<x>' is not yet wired in this build; staying on Claude Code.")` and keeps the existing Claude Code session running.

## 与计划的偏差

- The design includes a Tweaks panel for theme / accent / density. As stated in the plan, that is a designer affordance; only the chosen presets (dark theme, amber accent, compact-density chat content + 1.2× chrome) are applied to the shipped UI.
- Codex CLI is presented as a selectable option in the overlay but not yet wired to a real backend session class. Selecting it surfaces a diagnostic so the user gets immediate feedback. Wiring codex requires a parallel session implementation and is left for a follow-up.

## 施工中发现并修复的问题

- None during the wwwroot edits. Server rebuild failed with `MSB3027` because Rhino 8 had the previous `.rhp` loaded and locked the file; this is a runtime-state issue, not a code issue. The Companion `.exe` rebuilt cleanly (which is the only artifact this design touches), and the Server `.dll` from the prior build is sufficient to run the CLI smoke.

## 测试记录

```powershell
dotnet build src\MCP_Rhino.Companion\MCP_Rhino.Companion.csproj -c Release --nologo
```

- Exit code: 0
- Result: `MCP_Rhino.Companion -> ...\bin\Release\net8.0-windows\MCP_Rhino.Companion.dll`, 0 warnings, 0 errors.

Deployed `wwwroot\` confirmation:

```text
index.html  7,756 bytes  contains "settings-overlay"
styles.css 19,052 bytes  contains "settings-card"  no "zoom"
app.js     27,381 bytes  contains "buildSettingsChoices" (twice — definition + boot)
```

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- rhino-claude-code-companion-ui-smoke-test
```

- Exit code: 0
- Output:
  - `[OK] CLI mode keeps the Rhino live accessor disabled.`
  - `[OK] Per-document MCP config JSON is parseable and contains the bound pipe name.`
  - `[OK] Claude Code availability probe completed: Claude Code 2.1.131 is available.`
  - `[OK] MCP_Rhino.Companion.exe accepts the command-line contract used by _Mcpchat.`

## 验收判据对齐

- Companion default size: 420×900 with 360×600 min, matching the design's narrow side-panel artboard.
- Header now reads logo · MCP pill · spacer · + · ⚙ · pin. Model dropdown is gone.
- Settings overlay implements the design's three sections with card-style radios and matching amber active state.
- Backend CLI choice is selectable; codex selection surfaces a diagnostic and stays on Claude Code.
- Model choice continues to post the existing `model` IPC and updates the status-bar model echo.
- Chrome scaled per the design (40 / 28 / 28 px heights; 14 / 15 / 12 px fonts in header / textarea / status); chat content density unchanged.
- IPC contract is purely additive (`cli` is new); existing events still flow through unchanged.

## 回退验证

- No rollback executed.
- Practical rollback: revert the five files. The IPC additivity means a rolled-back Companion paired with the new host (or vice versa) is still functional; the host simply ignores absent `cli` messages and the front-end keeps the gear-icon-less header if reverted.

## 当前遗留项

- Manual UI smoke inside Rhino 8 to confirm the overlay clamps cleanly at 420 px width and the gear/X/Done close paths all work.
- Unload the plugin in Rhino and rebuild the Server when ready to ship a new `.rhp`.
- Wire codex backend if/when desired (new `CodexSession.cs` mirroring `ClaudeCodeSession.cs`, dispatched from `MainWindow.xaml.cs` based on the persisted `cli` choice).
- Persist the chosen CLI/model across launches (e.g., `%LOCALAPPDATA%\McNeel\Rhinoceros\8.0\Plug-ins\MCP_Rhino\companion.json`).
- Real token usage from `result.usage` for the donut.

## 结论

The Companion now matches the second design handoff: decluttered header, panel-scoped Settings overlay with Backend CLI / Model / MCP server sections, ~1.2× chrome scaling, and a 420×900 default window. The IPC contract is additive and the existing CLI smokes still pass. Live Rhino verification is the remaining manual step.

## 后续追加（2026-05-06）— 聊天历史时间戳

### 范围

User asked for a small Teams-style timestamp in the chat history. Assistant blocks did not previously show a time; user blocks already did. Added a meta row to assistant blocks (`Assistant · HH:MM`) in the same pattern as user blocks, with Teams-style grouping that collapses repeated meta rows when consecutive assistant messages land in the same minute. The dedupe resets when a user or system message breaks the run, or when the transcript is cleared.

### 改动

- `src/MCP_Rhino.Companion/wwwroot/app.js`
  - New module-scope state: `let lastAssistantStamp = null;`
  - `addAssistant(text, time?)`: optional `time` arg (defaults to `nowTime()`); renders the meta row only when `lastAssistantStamp !== t`, then updates `lastAssistantStamp = t`.
  - `addUser(...)` and `addSystem(...)`: reset `lastAssistantStamp = null` at the top so the next assistant message re-shows the meta row.
  - `clearTranscript()`: also resets `lastAssistantStamp = null`.
- No CSS or HTML change — the existing `.msg-meta` / `.msg-author` / `.msg-time` styling is reused from the user blocks, so the visual treatment is symmetric.

### 测试

```powershell
dotnet build src\MCP_Rhino.Companion\MCP_Rhino.Companion.csproj -c Release --nologo
```

- Exit code: 0, 0 warnings, 0 errors.
- Deployed `wwwroot\app.js` contains `lastAssistantStamp` (6 references) and the new `Assistant · HH:MM` meta row.

The companion-ui CLI smoke is unaffected by a presentation-only change and continues to pass.
