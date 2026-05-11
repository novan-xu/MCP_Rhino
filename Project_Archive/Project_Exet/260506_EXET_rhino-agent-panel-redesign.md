# 260506_EXET_rhino-agent-panel-redesign

## 对应计划

- Plan: `Project_Plan/260506_PLAN_rhino-agent-panel-redesign.md`
- Execute date: 2026-05-06

## 关联产物

- Test folder: `Project_Test/260506_TEST_rhino-agent-panel-redesign/`
- Source design bundle (Claude Design handoff): `mcp-rhino-cc-interface/project/Rhino Agent Panel.html`, `chat-panel.jsx` (extracted to `.tmp_design/` during execution).
- Commit / PR: not created in this working session.

## 执行结果 / 实际落地范围

- Replaced the Companion's WebView2 UI with the dark IDE / warm amber design:
  - `src/MCP_Rhino.Companion/wwwroot/index.html` — new shell with header (logo, MCP pill, model menu, new-chat, pin), transcript, composer with attach + auto-grow textarea + send/stop morph button + hint row, status bar (context donut, cost, model echo), and drag overlay.
  - `src/MCP_Rhino.Companion/wwwroot/styles.css` — full palette (`--bg`, `--surface-*`, `--border*`, `--text-*`, `--accent`, `--ok`, `--warn*`, `--err`, `--syn-*`), Inter + JetBrains Mono fonts, components for header / message rows / system dividers / thinking / tool cards / code cards / warn / diff / chips / composer / status bar / drag overlay.
  - `src/MCP_Rhino.Companion/wwwroot/app.js` — rich front-end: block builders for `addSystem`, `addUser`, `addAssistant` (renders `**bold**` and fenced ```\`\`\`lang code blocks```), `addThinking`, `addOrUpdateTool` (compact one-liner that expands on click; running/success/warn/failed dot), `addCode` with single-pass Python tokenizer + copy button, `addWarn`, `addDiff`; drag-and-drop with `is-dragging` overlay; `⏎` send / `⇧⏎` newline keyboard handling; auto-grow textarea; send button morphs to stop while busy; client-side context donut driven by cumulative chars / 4; cost bar updated from `result` events; model selector menu posts a `model` IPC message; pin button toggles via `pin` IPC; new-chat clears the transcript without killing Claude Code.
- Wired the WPF host to honor the new IPC affordance:
  - `src/MCP_Rhino.Companion/MainWindow.xaml.cs` now handles `pin` messages by toggling `Window.Topmost`.
  - `src/MCP_Rhino.Companion/MainWindow.xaml` background changed from the prior light grey (`#F6F7F9`) to the design's near-black (`#0B0B0D`) so the WebView2 paint flash matches the chat surface.
- Wire protocol (`CompanionUiEvent`) is unchanged — the redesign is presentation-only and consumes the existing `session` / `status` / `input` / `message` / `thinking` / `diagnostic` / `tool` / `result` event types.

## 与计划的偏差

- The plan called out an optional `clear` host message; on review the new-chat affordance is purely a UI affordance (clears the transcript array) and does not need to recycle the Claude Code child process for v1, so no new IPC type was added. A follow-up could introduce `clear` if we want hard "new conversation" semantics that also reset Claude Code.
- The composer no longer renders a local user echo — instead it relies on the host's `Message("user", text)` echo so user messages remain a single source of truth. This is a small cleanup vs. the prototype's optimistic-render approach and matches the existing IPC.
- Attachments are presented in chips and survive the panel state, but the host IPC still receives only `text`. A `// TODO` comment in `app.js` flags the upload path for follow-up.

## 施工中发现并修复的问题

- An earlier `Edit` to `MainWindow.xaml` accidentally dropped the closing `>` of the `Window` opening tag, surfacing as `MC3000: 'Name cannot begin with the '<' character'` during the Companion build. Fixed by restoring the angle bracket; rebuild succeeded.

## 测试记录

```powershell
dotnet build src\MCP_Rhino.Companion\MCP_Rhino.Companion.csproj -c Release --nologo
```

- Exit code: 0
- Result: `MCP_Rhino.Companion -> ...\bin\Release\net8.0-windows\MCP_Rhino.Companion.dll`, 0 warnings, 0 errors.
- `bin\Release\net8.0-windows\wwwroot\` contains the new `index.html` (4,958 bytes), `styles.css` (16,403 bytes), and `app.js` (25,240 bytes).

```powershell
dotnet build src\MCP_Rhino.Bridge\MCP_Rhino.Bridge.csproj -c Release --nologo
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --nologo
```

- Both exit 0, 0 warnings, 0 errors.

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- rhino-claude-code-companion-ui-smoke-test
```

- Exit code: 0
- Key output:
  - `[OK] CLI mode keeps the Rhino live accessor disabled.`
  - `[OK] Per-document MCP config JSON is parseable and contains the bound pipe name.`
  - `[OK] Claude Code availability probe completed: Claude Code 2.1.131 is available.`
  - `[OK] MCP_Rhino.Companion.exe accepts the command-line contract used by _Mcpchat.`

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- rhino-claude-code-panel-smoke-test
```

- Exit code: 0
- Existing panel smoke still passes (live-accessor disabled, per-doc MCP config parseable, Claude Code 2.1.131 available, Bridge `--pipe ... --help` returns usage).

## 验收判据对齐

- `_Mcpchat` continues to launch the Companion executable; the launch contract checked by the smoke is unchanged.
- Disallowed Claude Code tools and bound-document prompt remain identical to the prior session implementation.
- The redesigned UI exposes all design-bundle affordances called out in the plan: header (rhombus logo, `rhino·agent`, MCP pill with pulse, model selector, new-chat, pin), transcript (system divider, user/assistant blocks, collapsible thinking, tool cards with status dot, code cards with copy, warning, diff), composer (attach, auto-grow textarea with `⏎`/`⇧⏎`, morph send/stop button, hint row, attachment chips), status bar (context donut, cost, model echo), and drag overlay.
- WPF Topmost toggling responds to the new `pin` IPC message.

## 回退验证

- No rollback executed.
- Practical rollback: revert `wwwroot/{index.html, styles.css, app.js}` and the two `MainWindow.*` edits. Backend behavior and IPC contract are unchanged, so the rollback is bit-identical for the host process.

## 当前遗留项

- Manual UI smoke inside Rhino: open two saved `.3dm` files, run `_Mcpchat` in each, send `List the current layers.`, and confirm the redesigned tool cards and status bar update against a real Claude Code stream.
- Confirm the always-on-top toggle by clicking through to Rhino while the pin is on.
- Verify Google Fonts loads inside WebView2 in the target environment; if not, vendor Inter + JetBrains Mono under `wwwroot/fonts/`.
- Wire attachment uploads through the host IPC so dropped files are actually sent to Claude Code (currently chips are visual only; the host receives just the text body).
- Optional follow-up: emit a `clear` host message from the new-chat button if we want hard "new conversation" semantics that also recycle Claude Code.
- Real token usage from `result.usage` for the context donut, replacing the client-side char/4 heuristic.

## 结论

The Companion's WebView2 UI now matches the Claude Design handoff bundle's
"Rhino Agent Panel" — dark IDE aesthetic, warm amber accent, compact density,
full block hierarchy, drag-drop overlay, status bar, and pin/new-chat header
controls — without changing the IPC contract or backend behavior. All three
projects build cleanly and both existing CLI smokes pass; live Rhino smoke
remains for manual verification.
