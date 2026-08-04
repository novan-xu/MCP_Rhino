# 260506_PLAN_rhino-agent-panel-history

## Background

Five issues surfaced during live use of the Companion:

1. Tool cards expand by default and dump huge JSON results (the user wants them collapsed,
   expand on click).
2. The status-bar `$` field shows `0.0000` because subscription Claude/Codex CLIs don't
   surface cost in their stream events. The user wants a hypothesis cost estimate based on
   token usage and the chosen model's pricing.
3. The send button doesn't visually morph to a stop button when the agent is busy — the
   underlying click does swap behaviour, but the icon still shows the paper plane. Caused
   by `sendIcon.outerHTML = …`: the variable becomes a stale reference to a detached node
   after the first replacement, so subsequent updates don't paint.
4. Switching the Backend CLI in Settings restarts the backend session (host side) but the
   transcript carries over, which makes it feel like the same conversation continued under
   a new model. The user wants the panel to also show a fresh visible session on CLI
   change.
5. There's no way to access prior chats. The latest design bundle adds a clock icon to the
   header, a panel-scoped chat-history overlay, and a centred session-title in the header.
   The user also asked the panel to use the Rhino-native gray theme (`data-theme="rhino"`)
   and to keep 420×900 as the default window size.

The new design bundle (`Rhino Agent Panel.html`, handoff `JR_lvFowAIL6Efy-rcHV8w`) lays
out the history overlay shape, the session-title header slot, and the rhino theme palette.

## Goals

1. Tool cards: render collapsed by default, expand only when the user clicks the head.
2. Cost meter: replace the host-driven `totalCostUsd` echo with a client-side estimate
   computed from `contextUsed` × per-model blended rate. Update on every token bump.
3. Send button: morph to a red stop icon while busy; back to amber send when idle.
4. CLI swap: when the user picks a different Backend CLI in Settings, the front-end
   archives the current session and clears the transcript before the host restarts the
   backend.
5. History feature:
   - Add a clock icon between the MCP pill and the new-chat (+) button, opening a Chat
     History overlay scoped to the panel (matching the Settings overlay's modal shape).
   - The overlay supports search, splits sessions into Pinned and Recent groups, shows
     each session's title, last-message snippet, when, message count, token count, and
     CLI used, and supports per-session delete.
   - Sessions are persisted in `localStorage` keyed on the bound document path so they
     survive Companion restarts and are scoped to the same `.3dm`.
   - Clicking a session loads its transcript back into the panel (read-only replay) and
     restores the session title, model, and CLI metadata.
   - The header shows the current session title, middle-truncated, dim, between the MCP
     pill and the icon row. New-chat clears it.
   - Default theme switches to `data-theme="rhino"` (the Rhino-native gray palette) and
     the WPF window background updates to match (`#2b2b30`).

Non-goals (v1):

- Real cross-turn continuity in the backend when loading a past session — the current
  Claude / Codex sessions don't replay the loaded transcript into their context. The
  loaded session reads as a transcript reference; new prompts start a fresh backend
  conversation. The EXET documents this limitation and the path to v2 (`claude --resume`
  / `codex resume`).
- Disk persistence beyond localStorage. WebView2 persists localStorage per origin
  (`mcp-rhino.local`), so it survives Companion process restarts on the same machine.
- Real input/output token split — the current `contextUsed` heuristic conflates both,
  so the cost estimate uses a blended rate.

## Architecture Ownership

Presentation-only changes inside `src/MCP_Rhino.Companion/wwwroot/` plus the WPF window
background colour update. The IPC contract is unchanged. No new C# types, no
RhinoCommon, no Tools/Skills/Agents.

## Key Design

### 1. Tool collapse fix (`app.js`)

Remove the `if (sections.length > 0) entry.card.classList.add("is-open");` line at the
bottom of `addOrUpdateTool`. The head's existing click handler already toggles `is-open`,
so the default rendered state stays collapsed.

### 2. Cost estimate (`app.js`)

Add a per-model blended rate table (USD per 1M tokens), with a CLI-aware default fallback
when the user hasn't picked a specific model. Cost = (`contextUsed` / 1_000_000) × rate.
Recompute and rewrite the `costValue` text on every `bumpContext` and on every model /
CLI change.

Rates (publicly published list prices, with a 70/30 input/output blend):

| Model                  | Input $/Mtok | Output $/Mtok | Blended (70/30) |
| ---------------------- | -----------: | ------------: | --------------: |
| claude-haiku-4-5       |          1.0 |           5.0 |             2.2 |
| claude-sonnet-4-6      |          3.0 |          15.0 |             6.6 |
| claude-opus-4-7        |         15.0 |          75.0 |            33.0 |
| gpt-5.2 / gpt-5.3*     |          5.0 |          15.0 |             8.0 |
| gpt-5.4*               |          7.5 |          22.5 |            12.0 |
| gpt-5.4-mini           |          1.5 |           4.5 |             2.4 |
| gpt-5.5                |         10.0 |          30.0 |            16.0 |
| (default · Claude CLI) |              |               |             6.6 |
| (default · Codex CLI)  |              |               |            12.0 |

*Estimates; the actual pricing trail Anthropic/OpenAI publish may differ. The
estimate's job is to give the user a directional sense, not a billing report. The
status-bar tooltip is updated to read "estimated · based on token usage" so the
nature of the figure is explicit.

### 3. Send-button morph (`app.js`)

Replace the `sendIcon.outerHTML` write path with `sendButton.innerHTML = …` so a fresh
SVG is painted on every state change. Drop the now-unused `sendIcon` module-scope
variable.

### 4. Fresh session on CLI swap (`app.js`)

When the user clicks a different CLI in the Settings overlay, before posting the `cli`
IPC, the front-end calls a new `archiveAndClearSession(reason)` helper that pushes the
current session into history (if non-empty) and clears the transcript. The host then
restarts the backend (existing behaviour), and the new backend's `Session(_options)`
event lands into a clean panel.

### 5. History feature (`app.js`, `index.html`, `styles.css`)

#### Markup

The header gains a `historyButton` between the MCP pill and the new-chat button, and a
`headerTitle` element between the MCP pill and the icon row that displays the current
session title. The new-chat button's spacing logic still keeps icons right-aligned.

A new `.history-overlay` mirrors `.settings-overlay`'s modal shape. The card has a
header, a search row (input + clear button), and a body with two groups (Pinned,
Recent) listing `.history-item`s. Each item shows title, snippet (2-line clamp),
timestamp, message count, token count, CLI, and a per-item delete button.

#### Persistence

A small `historyStore` module (defined inline in `app.js`) wraps `localStorage` with
keys:

```text
mcp-rhino:history:<documentPath>:index   → JSON array of session summaries
mcp-rhino:history:<documentPath>:s:<id>  → JSON of full session messages
```

Each summary: `{ id, title, snippet, when, msgs, tokens, cli, model, pinned? }`. Each
full session: `{ id, html }` (renderable HTML snapshot of the transcript at archive
time). v1 stores the transcript HTML rather than re-serialising every block kind — this
keeps the persistence layer narrow at the cost of read-only replay.

The current session is tracked in `currentSession = { id, title, archivedAt? }`. A
fresh id is generated on app boot and on every new-chat / CLI swap / loaded-session.

#### Title derivation

When archiving, the title is derived from the first user message (truncated to ~60
chars). Snippet = the last assistant text (truncated similarly). `when` uses
`new Date().toLocaleString()`. `msgs` is counted from the rendered DOM (not strictly
accurate but close enough). `tokens` is `contextUsed` at archive time.

#### Rhino theme

Add `body[data-theme="rhino"]` palette overrides to `styles.css` matching the design
bundle. Set `<body data-theme="rhino">` in `index.html`. Update
`MainWindow.xaml`'s `Background="#0B0B0D"` to `Background="#2B2B30"` so the WebView2
paint flash matches the chat surface.

## Involved Files

Modified:

```
src/MCP_Rhino.Companion/wwwroot/index.html
src/MCP_Rhino.Companion/wwwroot/styles.css
src/MCP_Rhino.Companion/wwwroot/app.js
src/MCP_Rhino.Companion/MainWindow.xaml
```

New:

```
Project_Plan/260506_PLAN_rhino-agent-panel-history.md
Project_Test/260506_TEST_rhino-agent-panel-history/README.md
Project_Exet/260506_EXET_rhino-agent-panel-history.md
```

## Usage

```powershell
dotnet build src\MCP_Rhino.Companion\MCP_Rhino.Companion.csproj -c Release --nologo
```

In Rhino with a saved `.3dm`, run `_Mcpchat`. Verify each issue:

1. Tool cards land collapsed; click to expand.
2. Status bar `$` updates as tokens accumulate (e.g., `$ 0.123`).
3. Send button shows a stop icon while busy; back to send when idle.
4. Open Settings → switch CLI → transcript clears, fresh session starts.
5. Header has a clock icon. After a few new-chat cycles, the history overlay lists
   prior sessions; clicking one replays it.

## Acceptance Criteria

- Companion builds clean.
- Existing companion-ui smoke still passes.
- Issues 1–4 verified by manual interaction.
- History overlay opens, search filters, items load, delete works, sessions persist
  across Companion restarts (same `.3dm`).
- Default theme is `rhino`; window background and chat surface are visibly grey rather
  than near-black.

## Risks And Rollback

- **Risk**: localStorage quota exhaustion if users archive very long sessions.
  - Mitigation: trim oldest unpinned sessions to keep ≤ 50 entries; cap individual
    snapshot HTML size at 500 KB and elide the rest.
- **Risk**: Replay of past session HTML loses interactivity (tool toggle, code copy).
  - Mitigation: documented as v1 limitation; future work to re-attach handlers when
    rendering past sessions.
- **Risk**: Cost estimate is wrong relative to actual provider billing.
  - Mitigation: tooltip explicitly labels the figure as an estimate.
- **Rollback**: revert the four files. The IPC contract is unchanged.

## Future Extensions

- v2 token usage from real `result.usage` (Claude `result` event already carries an
  empty cost; if Anthropic/OpenAI start shipping token counts in stream-json, switch to
  the real number).
- v2 backend continuity: pass the loaded session's messages back into Claude Code via
  `--resume <session-id>` (Claude already supports this; codex has `codex resume`).
- v2 disk persistence in `%LOCALAPPDATA%\McNeel\Rhinoceros\8.0\Plug-ins\MCP_Rhino\
  companion\history\<docPath-hash>\…` so the history survives a WebView2 cache wipe.
- v3 export / import of past sessions as JSONL.
