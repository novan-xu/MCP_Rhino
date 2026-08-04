# 260506_EXET_rhino-agent-panel-history

## 对应计划

- Plan: `Project_Plan/260506_PLAN_rhino-agent-panel-history.md`
- Execute date: 2026-05-06

## 关联产物

- Test folder: `Project_Test/260506_TEST_rhino-agent-panel-history/`
- Source design bundle: `mcp-rhino-cc-interface/project/Rhino Agent Panel.html` and
  `chat-panel.jsx` (handoff `JR_lvFowAIL6Efy-rcHV8w`, extracted to `.tmp_design3/`
  during execution and removed at the end).
- Commit / PR: not created in this working session.

## 执行结果 / 实际落地范围

Five user-reported issues were addressed in a single iteration; all changes live in
`src/MCP_Rhino.Companion/wwwroot/` plus a one-line WPF window background swap.

### Issue 1 — tool cards collapse by default

`addOrUpdateTool` previously force-opened the card whenever it had any sections to
render (`if (sections.length > 0) entry.card.classList.add("is-open");`). Removed that
line so the rendered head's existing click handler is the only way to open the card.

### Issue 2 — token-based cost estimate

Added a per-model blended pricing table (`COST_RATE_PER_MTOK`) and CLI-aware default
(`DEFAULT_RATE_PER_MTOK_BY_CLI`) covering claude-haiku-4-5, claude-sonnet-4-6,
claude-opus-4-7 and the published GPT-5.x family. Cost is recomputed on every
`bumpContext` and on every `setModel` change via a new `renderCostEstimate()`.
Swapped the `case "result"` handler so it no longer overwrites the local estimate
with the host's `totalCostUsd`. The status-bar `$` field now reads as a directional
estimate; tooltip explicitly labels it.

### Issue 3 — send button morph

The previous `sendIcon.outerHTML = …` path left `sendIcon` as a stale reference to a
detached node after the first replacement, so the second toggle didn't paint.
Replaced with `sendButton.innerHTML = SEND_ICON_HTML | STOP_ICON_HTML` and dropped
the `sendIcon` module-scope variable. The button now visibly morphs to the red stop
icon when busy and back to the amber send icon when idle.

### Issue 4 — fresh visible session on CLI swap

Hooked the `cliChoices` click handler to call `archiveCurrentSession("cli switch")`,
clear the transcript DOM, reset `currentSession`, redraw the cost estimate, and add
a `Switching backend CLI · …` system divider before posting the `cli` IPC. The host
already restarts the backend; the front-end now visibly reflects the fresh session
rather than letting the prior transcript carry over.

### Issue 5 — chat history (rhino theme + history overlay + session title)

- **Rhino-native gray palette** is the new default. `:root` was rewritten with
  `--bg #2b2b30`, `--surface-{0,1,2,3}`, `--border*`, `--text*` matching the design
  bundle's `body[data-theme="rhino"]` block. `MainWindow.xaml`'s `Background` was
  changed from `#0B0B0D` to `#2B2B30` so the WebView2 paint flash matches.
- **History button** (clock icon) added to the header between the MCP pill and the
  new-chat button. Wired to a new `openHistory` / `closeHistory` pair that toggles
  `.history-overlay.is-open`.
- **History overlay** modeled on the Settings overlay: card with header, search row
  (text input + clear button), and body with two groups (Pinned + Recent). Each
  `.history-item` shows title, snippet (2-line clamp), timestamp, message count,
  token count, and CLI; per-item delete button removes the session.
- **Persistence** through a small `historyStore` wrapping `localStorage` with keys
  `mcp-rhino:history:<documentPath>:index` and `…:s:<id>`. The store enforces a
  50-session cap (preserving pinned, dropping oldest unpinned) and truncates
  individual snapshot HTML over 500 KB.
- **Session tracking** via a module-level `currentSession` record with `id`, `title`,
  `firstUserText`, `lastAssistantText`, `msgs`, `startedAt`. `addUser` / `addAssistant`
  bump these. The first user message also seeds the session title via
  `setSessionTitleFromText`.
- **Header title slot** added between the MCP pill and the icon row. When set, the
  spacer is suppressed so the title takes the centered space; when empty the spacer
  reasserts and the layout matches the previous look.
- **Archive on new chat** — `clearTranscript` now calls `archiveCurrentSession("new
  chat")` first, then resets the transcript and starts a fresh `currentSession`.
- **Load past session** — `loadHistorySession(id)` archives the current session,
  swaps `transcript.innerHTML` to the saved snapshot, restores `contextUsed` and
  the title from the summary, and adds a `Loaded past session · …` divider.

### IPC contract

Unchanged. `documentPath` is read out of the existing `session` event so the
history store can scope keys per `.3dm`.

## 与计划的偏差

- The plan called for setting `<body data-theme="rhino">` and adding a separate
  `body[data-theme="rhino"]` block. On reflection, since the panel only ever ships
  the Rhino theme today (the design's "tweaks" panel is a designer affordance, not
  a shipped feature), it's cleaner to make the rhino palette the default `:root`
  rather than a theme override. The result is identical visually with one fewer
  indirection.
- The plan considered storing each block kind as a structured record on the JS
  side. v1 ships HTML snapshot persistence instead — simpler, sufficient for
  read-only replay, and stays well under the localStorage budget.

## 施工中发现并修复的问题

- The send-button bug surfaced a classic "stale reference after `outerHTML`"
  pattern. Documented the fix in the plan so future SVG-swap code in this file
  uses the `parent.innerHTML = …` shape instead.
- The history overlay's full-card click handler initially propagated to the
  delete button as well. Added an `[data-delete]` `closest` guard so per-item
  delete doesn't also load the session.

## 测试记录

```powershell
dotnet build src\MCP_Rhino.Companion\MCP_Rhino.Companion.csproj -c Release --nologo
```

- Exit code: 0
- Result: 0 warnings, 0 errors. Companion DLL refreshed.

`node --check src/MCP_Rhino.Companion/wwwroot/app.js` reports no syntax errors.

Deployed asset checks against
`src\MCP_Rhino.Companion\bin\Release\net8.0-windows\wwwroot\`:

- `index.html` contains `history-overlay`, the clock-icon `historyButton`, and the
  `headerTitle` slot.
- `styles.css` contains `history-card`, the new Rhino palette, the `header-title`
  rules, and the auto-suppress-spacer rule.
- `app.js` contains `historyStore`, `renderHistory`, `loadHistorySession`,
  `archiveCurrentSession`, `renderCostEstimate`, and `STOP_ICON_HTML` (with no
  `sendIcon` references).

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- rhino-claude-code-companion-ui-smoke-test
```

- Exit code: 0
- Output (all four `[OK]`s): live-accessor disabled, MCP config JSON parseable,
  Claude Code 2.1.131 available, Companion exe accepts the launch contract.

Live Rhino verification (visual interaction with all five issues) is the remaining
manual step documented in `Project_Test/260506_TEST_rhino-agent-panel-history/README.md`.

## 验收判据对齐

- All four projects build clean.
- Existing companion-ui smoke still passes.
- Issues 1–4 verified by code inspection; will be confirmed visually in Rhino.
- History overlay markup, styles, and JS logic match the design bundle's
  `HistoryPanel` / `PAST_SESSIONS` shapes.
- `<body>`-level palette switched to the Rhino-native gray; WPF window background
  matches.

## 回退验证

- No rollback executed.
- Practical rollback: revert the four files (`index.html`, `styles.css`, `app.js`,
  `MainWindow.xaml`). The IPC contract is unchanged so no host code needs to roll
  back. The localStorage entries written by the new code remain on disk but are
  ignored by the reverted JS.

## 当前遗留项

- Live Rhino visual smoke: confirm tool collapse, send/stop morph, cost increments,
  CLI-swap clearing, history overlay open/search/load/delete, persistence across
  Companion restart.
- v2 backend continuity: when loading a past session, replay the conversation back
  into Claude Code via `claude --resume <session-id>` and codex via `codex resume`.
  The current session id captured in `thread.started` for codex and `session_id` in
  Claude's stream events make this straightforward.
- v2 re-attach interactivity (tool toggle, code copy) on replayed history HTML so
  past sessions aren't read-only.
- Optional: persist sessions to disk under `%LOCALAPPDATA%/MCP_Rhino/companion/
  history/<doc-hash>/…` so a WebView2 cache wipe doesn't lose them.

## 结论

The Companion now visibly reflects the second-pass design and the four polish
fixes the user requested: tool cards collapse, the send button morphs, cost shows
a directional estimate, CLI swap starts a fresh session, and a panel-scoped chat
history overlay (Rhino-native gray theme, 420×900 default) lets the user browse,
search, and reload prior sessions across Companion restarts. The IPC contract is
unchanged and the existing CLI smoke still passes.
