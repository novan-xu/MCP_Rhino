# Rhino Agent Panel History + Polish Smoke

This iteration ships five fixes/features inside the Companion's `wwwroot/`:

1. Tool cards land **collapsed**; the user clicks the head to expand.
2. Status-bar `$` displays a **token-based cost estimate** based on the chosen model,
   not the host's `totalCostUsd` (which is `$0` on subscription plans).
3. Send button **morphs to a red stop icon** while busy.
4. Switching the Backend CLI in Settings **clears the transcript** so the panel visibly
   reflects a fresh session (the host already restarts the backend).
5. **Chat history**: clock icon in the header opens a panel-scoped overlay listing past
   sessions with search, Pinned + Recent groups, per-session metadata, delete, and
   click-to-load. Sessions are persisted in `localStorage` keyed by the bound document
   path so they survive Companion restarts. The current session title shows in the
   header, middle-truncated. Default theme is the Rhino-native gray palette
   (`#2b2b30` background, `#36363c` chat surface).

## Build

```powershell
dotnet build src\MCP_Rhino.Companion\MCP_Rhino.Companion.csproj -c Release --nologo
```

If Rhino has the previous `.rhp` loaded, the Server build will still fail because of the
loaded plugin lock; that's unrelated to this change. Only the Companion is touched.

## CLI smoke

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- rhino-claude-code-companion-ui-smoke-test
```

Expected: all `[OK]` lines pass. The IPC contract is unchanged.

## Manual UI smoke

1. Build the Companion. Confirm
   `src\MCP_Rhino.Companion\bin\Release\net8.0-windows\wwwroot\` contains the updated
   `index.html` (has `history-overlay`), `styles.css` (has `history-card`), and `app.js`
   (has `historyStore`, `renderCostEstimate`, `STOP_ICON_HTML`).
2. Launch Rhino 8, load the plugin, open a saved `.3dm`, run `_Mcpchat`. Verify:
   - Window is the Rhino-native gray (`#2b2b30`), not the previous near-black.
   - Header shows logo · MCP pill · clock · + · gear · pin.
3. Send `List the layers in this file.` and confirm tool cards arrive **collapsed**.
   Click a head → it expands; click again → it collapses.
4. While the agent is busy, the send button shows a red square stop icon. Clicking it
   stops the turn and the icon flips back to the paper-plane send.
5. Watch the status bar: `$ <number>` increments as tokens accumulate, scaled by the
   selected model. Hover the `$` field — tooltip reads "Estimated cost · based on token
   usage and the selected model's published price".
6. Open Settings → switch Backend CLI to `codex cli`. The transcript clears, a system
   divider says `Switching backend CLI · codex cli`, and the host restarts the backend.
7. Click + (new chat). Confirm the prior conversation gets archived to history (clock
   icon shows incremented count).
8. Click the clock. The history overlay opens with one or more past sessions. Type in
   the search box → the list filters live. Click a session → the transcript reloads
   that snapshot (read-only replay; new prompts start a fresh backend conversation —
   see "Known limitations" below).
9. Hover an item, click the trash icon → that session is removed from the list and the
   count updates.
10. Close the Companion, run `_Mcpchat` again on the same `.3dm`. The history list is
    still present (localStorage persistence per document path).

## Known limitations (v1)

- Loading a past session restores the rendered transcript HTML but does not replay
  the conversation back into the underlying Claude Code / Codex session. New prompts
  in a loaded session start a fresh backend turn with no memory of the loaded text.
  v2 will use `claude --resume <session-id>` and `codex resume` for true continuity.
- Tool card click handlers are not re-attached on replayed history snapshots; the
  cards render their last state but click-to-toggle is inert until a new turn.
- Cost estimates are directional, not a billing report. Anthropic / OpenAI publish
  tier-specific list prices that this estimate uses with a 70/30 input/output blend.
- localStorage is capped at ~5 MB per origin. The store trims to ≤ 50 sessions and
  truncates individual snapshots over 500 KB.

## Rollback

Revert the four files (`index.html`, `styles.css`, `app.js`, `MainWindow.xaml`). The
IPC contract didn't change, and there are no new C# files.
