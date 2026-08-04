# 260506_PLAN_rhino-agent-panel-redesign

## Background

The MCP_Rhino Companion (`src/MCP_Rhino.Companion/`) was introduced earlier today as a
WPF + WebView2 chat host bound per Rhino document (see
`Project_Plan/260506_PLAN_rhino-claude-code-companion-ui.md`). Its initial UI is a plain
light-themed chat with a topbar, transcript area, and composer. The wire protocol from
the WPF host is `CompanionUiEvent` (session/status/input/message/thinking/diagnostic/tool/result).

The user provided a Claude Design handoff bundle (`Rhino Agent Panel.html`) that specifies
a richer dark IDE aesthetic, warm amber accent, compact density, and a fuller set of
message blocks (system divider, user, assistant, collapsible thinking, expandable tool
calls with status dot, syntax-highlighted code, warning, geometry diff, drag-drop overlay,
status bar with context donut + cost + model echo, header with MCP indicator + model
selector + new-chat + always-on-top pin).

This plan redesigns the Companion's `wwwroot/` UI to match the design while keeping the
existing WPF host, IPC protocol, and per-document binding model unchanged.

## Goals

1. Replace `wwwroot/{index.html, styles.css, app.js}` with the design's dark IDE look:
   - palette (`--bg #0b0b0d`, `--surface-0/1/2/3`, `--border*`, amber accent), Inter/JetBrains Mono.
   - block hierarchy: system divider, user (avatar+time+text+attachments), assistant
     (amber AI badge + bold), thinking (collapsible), tool (compact one-liner, expandable
     to result + duration + ok/warn/err dot), code (syntax-highlighted Python with copy),
     warning (amber border + icon + title + text), diff (+ added / ~ modified / − deleted).
   - input area: paperclip + auto-grow textarea + send/stop morph button, attachment chips
     above input, hint row (`⏎ send · ⇧⏎ newline · drop files anywhere`).
   - drag-drop overlay covers the panel during drag.
   - status bar: SVG context donut (used / total tokens, color shifts at 70 / 85 %),
     cost echo, model echo.
   - header: rhombus logo + `rhino·agent`, MCP status pill with green pulse when connected,
     model selector dropdown, new-chat button, pin toggle (amber when on).
2. Wire UI controls back to the WPF host where it makes sense:
   - `pin` message → toggle `Window.Topmost`.
   - `clear` message → no-op for the host (UI just clears the transcript).
   - `stop` and `send` already exist.
3. Approximate context usage on the JS side based on cumulative text length (matching the
   prototype's heuristic). Real token accounting can be wired later.
4. Keep the WPF host technology stack untouched: WebView2 + virtual-host mapping to
   `wwwroot/`, JSON IPC via `CompanionUiEvent`.

Non-goals:

- Real token counting from Claude Code's stream-json (defer).
- Light/Rhino-gray themes and a Tweaks panel — the design canvas's tweaks are a designer
  affordance, not part of the shipped panel.
- Replacing or moving the existing Eto-based Rhino-side fallback panel.
- New Rhino-side Tools/Skills/Agents.

## Architecture Ownership

Affected layer: **Infrastructure / Companion UI** (`src/MCP_Rhino.Companion/`).

Per `Project_Guides/MCP_Rhino Architecture.md`:

- The Companion is a sibling Windows desktop project, allowed under "对外 thin shim" sibling
  csproj guidance. UI assets live under its own `wwwroot/`.
- No `Tools/`, `Skills/`, `Agents/`, `Application/`, or `Domain/` change — this is
  presentation-only.
- No RhinoCommon access — the design only changes the WebView2 surface and a small WPF
  message handler for `pin`.
- Live Only execution mode is unaffected: the bound MCP pipe and Claude Code argument
  contract remain identical.

## Key Design

### Palette / typography

CSS custom properties exactly as in the design:

```
--bg #0b0b0d  --surface-0 #111114  --surface-1 #16161a  --surface-2 #1c1c21
--surface-3 #25252c  --border-faint #1f1f25  --border #26262e  --border-strong #2f2f38
--text-faintest #5c5c66  --text-faint #8a8a96  --text #c9c9d1  --text-strong #ebebf0
--avatar-user #25252c  --accent oklch(0.74 0.15 60)  --accent-fg #1a120a
--ok oklch(0.74 0.13 150)  --warn oklch(0.78 0.14 80)
--warn-bg rgba(180,130,40,0.08)  --warn-border rgba(180,130,40,0.3)
--err oklch(0.68 0.18 25)  --syn-kw / --syn-str / --syn-num / --syn-comment
```

Fonts: Inter 400/500/600 + JetBrains Mono 400/500/600 from Google Fonts (already accepted
in the WebView2 — internet egress is fine for Claude Code anyway).

### Blocks

JS factories per block kind, modeled after `chat-panel.jsx`:

- `addSystem(text, time?)` — centered hairline rule with mono text.
- `addUser(text, attachments?, time?)` — gutter avatar, name + time, body text, attachment
  chip row.
- `addAssistant(text)` — amber `AI` avatar, body with `**bold**` rendered.
- `addThinking(text, duration?)` — collapsible with brain icon and `thinking · 1.4s` summary.
- `addTool({ name, input, result, status, durationMs })` — compact one-liner card,
  click to expand and show `→ result`. Status dot color = ok/warn/err.
- `addCode({ lang, code })` — header with lang + copy button, body with single-pass Python
  tokenizer (mirrors prototype: keywords / strings / numbers / comments).
- `addWarn(title, text)` — amber-bordered card.
- `addDiff({ added, modified, deleted })` — geometry diff with `+ / ~ / −` rows.

### Mapping `CompanionUiEvent` → blocks

| Event type | Renderer |
| --- | --- |
| `session` | header doc name + MCP status init |
| `status` | header MCP status pill text + pulse |
| `input` | enable/disable composer, swap send button to stop while busy |
| `message` (role=user) | `addUser` |
| `message` (role=assistant) | `addAssistant` |
| `message` (role=system) | `addSystem` |
| `thinking` | `addThinking` |
| `diagnostic` | `addWarn("Diagnostic", text)` |
| `tool` | `addTool` (status running/success/failed) |
| `result` (totalCostUsd) | status bar cost echo |

`code`, `diff`, `addCode`/`addDiff` are not currently emitted by the host; they remain
available so future event types can render without a UI rewrite. The fenced code parser
inside assistant text continues to render `\`\`\`lang\n...` as a code block (replacing the
prior `<pre>` fallback) using the same syntax highlighter.

### Drag and drop

Whole-panel `dragover` toggles a fixed amber-dashed overlay. On `drop`, files become chips
above the composer. Sending currently posts only the text part to the host; attachments are
listed but not yet uploaded — this matches the prototype which only previews chips. A
TODO comment marks the upload path for follow-up work.

### Pin / always-on-top

`window.chrome.webview.postMessage({type:"pin", pinned:true|false})`. The WPF host toggles
`this.Topmost`. The pin button reflects the pinned state with the amber accent color.

### Context meter

`used` is bumped client-side by `Math.ceil(text.length / 4) + tool_io_chars/4`. Total is
`200000`. Donut color: `--accent` < 70 %, `--warn` 70–85 %, `--err` > 85 %.

### New-chat (`+`)

Clears the transcript array and resets `used` to a small floor. The host is **not** told
to reset Claude Code; the design treats this as a UI-only "fresh view". A follow-up could
add a dedicated `clear` host message to also recycle the Claude Code child process.

## Involved Files

Modified:

```
src/MCP_Rhino.Companion/wwwroot/index.html
src/MCP_Rhino.Companion/wwwroot/styles.css
src/MCP_Rhino.Companion/wwwroot/app.js
src/MCP_Rhino.Companion/MainWindow.xaml
src/MCP_Rhino.Companion/MainWindow.xaml.cs
```

New:

```
Project_Plan/260506_PLAN_rhino-agent-panel-redesign.md
Project_Test/260506_TEST_rhino-agent-panel-redesign/README.md
Project_Exet/260506_EXET_rhino-agent-panel-redesign.md
```

## Usage

```powershell
dotnet build src\MCP_Rhino.Bridge\MCP_Rhino.Bridge.csproj   -c Release --nologo
dotnet build src\MCP_Rhino.Companion\MCP_Rhino.Companion.csproj -c Release --nologo
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj   -c Release --nologo
```

In Rhino with a saved `.3dm`, run `_Mcpchat`. The companion window opens with the dark
amber-accented chat UI. Sending `List the current layers.` should surface a tool card with
the new compact + expandable presentation. The pin button toggles always-on-top. Drag a
file onto the panel and confirm the overlay + chip behavior.

## Acceptance Criteria

### Build

- All three projects build with 0 warnings, 0 errors.
- `wwwroot/{index.html, styles.css, app.js}` are copied to the build output as before.

### Smoke

The existing slug `rhino-claude-code-companion-ui-smoke-test` still passes:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- rhino-claude-code-companion-ui-smoke-test
```

A new slug is **not** introduced because there is no new backend behavior — only UI assets
change. The plan log §"Live Smoke CLI 入口约定" allows this as long as no new entry point is
needed.

The existing slug `rhino-claude-code-panel-smoke-test` is unaffected and still passes.

### Manual

- `_Mcpchat` opens the companion with the dark IDE look and amber accent.
- `⏎` sends, `⇧⏎` newline.
- Send button morphs to a stop button while busy and clicking it cancels the turn.
- Pin button toggles always-on-top (verifiable by clicking past Rhino).
- New-chat button clears the transcript without killing Claude Code.
- A dropped file shows the amber overlay then surfaces a chip above the composer.
- Status bar shows context donut, cost from `result` events, and the model echo.

### Safety

- Disallowed Claude Code tools list is unchanged (`Bash,Edit,Read,Write,…`).
- Bound MCP pipe and bridge launch contract are untouched.
- No new RhinoCommon code, no new Tools/Skills/Agents.

## Risks And Rollback

- **Risk**: WebView2 fails to load Google Fonts on a restricted network.
  - Mitigation: keep `Inter` and `JetBrains Mono` as the first declared family, then a
    system fallback chain (`system-ui, "Segoe UI", sans-serif` for body; `ui-monospace,
    Consolas, monospace` for code). The look degrades gracefully.
  - Rollback: Vendor the fonts under `wwwroot/fonts/` if needed.
- **Risk**: WPF Topmost flicker on rapid pin toggling.
  - Mitigation: simple property assignment; rare in practice.
- **Risk**: Attachment chips suggest upload but the host does not yet upload.
  - Mitigation: comment in `app.js` documents the gap; keep chip removal so users can clear
    accidental drops.
- **Rollback**: revert the three `wwwroot/` files and the two MainWindow files. The
  `CompanionUiEvent` contract is unchanged so backend behavior is bit-identical.

## Future Extensions

- Real token usage from Claude Code's `result.usage` for the donut.
- Light and Rhino-gray themes via the existing CSS custom-property system.
- Attachment upload to Claude Code (paste base64 into the user message or wire a file path
  pass-through tool).
- Rendering of fenced code blocks emitted by Claude Code with the new syntax highlighter.
- Geometry-diff event type emitted by Rhino tools and rendered with `addDiff`.
