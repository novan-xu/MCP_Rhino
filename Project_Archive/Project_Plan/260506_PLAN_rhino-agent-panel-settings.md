# 260506_PLAN_rhino-agent-panel-settings

## Background

A second Claude Design handoff bundle for the Rhino Agent Panel (`Rhino Agent Panel.html`)
arrived after the first redesign (`260506_PLAN_rhino-agent-panel-redesign.md`). The new
design tightens the previous one in three ways:

1. The header was decluttered: the model selector was removed and replaced with a single
   gear icon. Model selection moves into a panel-scoped Settings overlay.
2. A new "Backend CLI" choice was introduced in the same overlay — the panel is meant to
   front any agent CLI; the design lists `claude code cli` and `codex cli`.
3. The header, status bar, and composer chrome are scaled up ~1.2× while chat content
   density is unchanged. Density tokens are reorganised (`--fs-mono`, `--fs-meta`,
   `--ctrl-h`, `--hdr-h`, `--status-h`).

The user also asked that the narrow side-panel form factor (420×900) be the default for
the WPF host window. This matches the design's "narrow side panel" artboard and fits the
"docked next to a Rhino viewport" use case better than the prior 1120×780 default.

The `zoom: 1.5` rule applied to `body` after the first pass was a workaround for the
small base sizing. It is no longer needed because chrome now has explicit larger sizes,
and stacking it would over-scale chat content.

## Goals

1. Header: collapse model menu into a Settings overlay reachable via a gear icon. Keep
   the rhombus logo, MCP pill, new-chat (+), settings (⚙), and pin (📌) icons. Use the
   28×28 hit-target variant (`rh-btn-icon--lg`).
2. Settings overlay: panel-scoped modal with three sections —
   - **Backend CLI** — radio cards for `claude code cli` and `codex cli`.
   - **Model** — radio cards for `Default`, `claude-haiku-4-5`, `claude-sonnet-4-6`,
     `claude-opus-4-7`.
   - **MCP server** — read-only readout of the bound pipe with a green dot when the
     bound MCP reports `connected`.
3. Chrome scale: bump density tokens (`--pad-x` 12, `--pad-row` 6, `--gap-row` 10,
   `--gap-msg` 3, `--fs-body` 15) and add `--fs-mono` 13, `--fs-mono-sm` 12,
   `--fs-meta` 11.5; header height 40, status bar height 28, status font 12, donut SVG
   20×20, composer textarea 15px, send/stop button 28×28, paperclip 16, kbd 11px,
   composer hint 11px.
4. Drop the `zoom: 1.5` body rule — it overlaps the new explicit sizes.
5. Default WPF window size: 420×900 with min size 360×600. Match the "narrow side
   panel" artboard.
6. Wire `cli` IPC: front-end posts `{type:"cli", cli:"claude code cli"|"codex cli"}` on
   selection. The host accepts the message and, for now, emits a system diagnostic when
   `codex cli` is selected (codex is not yet wired in this build) and stays on Claude
   Code. Persisting the choice across sessions is a future extension.

Non-goals:

- Wiring the codex CLI backend (would need a parallel session class spawning
  `codex --json` or equivalent).
- Persisting the selected CLI / model across launches.
- Adding the design canvas's Tweaks panel (theme, accent, density toggles) — those are
  designer affordances, not in the shipped panel.

## Architecture Ownership

Same as the first redesign — presentation only inside `src/MCP_Rhino.Companion/wwwroot/`
plus the WPF host window (size + new IPC type). No Tools / Skills / Agents / Domain /
Application change. No RhinoCommon access.

## Key Design

### CSS density tokens (final values)

```
--pad-x: 12px  --pad-row: 6px  --gap-row: 10px  --gap-msg: 3px
--fs-body: 15px  --fs-mono: 13px  --fs-mono-sm: 12px  --fs-meta: 11.5px
--ctrl-h: 26px  --hdr-h: 40px  --status-h: 28px
```

Chat-content sizes (avatar 20×20, body 15px, code 13px, system row 12px, etc.) inherit
from these and are intentionally smaller than chrome — the user said chat density was
fine; only chrome should grow.

### `rh-btn-icon--lg`

Adds 28×28 hit target with 4px radius, paired with 16px icons.

### Header

Slot order: rhombus logo + `rhino·agent` wordmark, MCP pill (12px font, 7px dot),
flex-spacer, +, ⚙, pin. Removes the previous model dropdown.

### Settings overlay

A panel-scoped modal with three Field cards. Card pattern: outer button with 1px border
that turns to `--accent` plus an `rgba(180,130,40,0.08)` fill when active; an 8px
filled / hollow circle indicates state. Closes on the X icon, the Done button, or
clicking the dim backdrop. Wired in JS with class toggles and posts an IPC message on
each selection (`model` already exists; new `cli`).

### `cli` IPC

```json
{ "type": "cli", "cli": "claude code cli" | "codex cli" }
```

`MainWindow.xaml.cs` reads the field. If the value is not `claude code cli`, emits a
diagnostic via `CompanionUiEvent.Diagnostic("Backend CLI 'codex cli' is not yet wired in
this build; staying on Claude Code.")`. The Claude Code session keeps running.

### WPF window

`MainWindow.xaml`: `Width="420" Height="900" MinWidth="360" MinHeight="600"`.
Title remains the bound document filename.

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
Project_Plan/260506_PLAN_rhino-agent-panel-settings.md
Project_Test/260506_TEST_rhino-agent-panel-settings/README.md
Project_Exet/260506_EXET_rhino-agent-panel-settings.md
```

## Usage

```powershell
dotnet build src\MCP_Rhino.Bridge\MCP_Rhino.Bridge.csproj         -c Release --nologo
dotnet build src\MCP_Rhino.Companion\MCP_Rhino.Companion.csproj   -c Release --nologo
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj         -c Release --nologo
```

In Rhino with a saved `.3dm`, run `_Mcpchat`. The companion now opens at 420×900 with
the gear icon in the header. Click ⚙ to open Settings; pick a model or backend CLI.

## Acceptance Criteria

- All three projects build with 0 warnings, 0 errors.
- `rhino-claude-code-companion-ui-smoke-test` and `rhino-claude-code-panel-smoke-test`
  still pass.
- The companion window opens at 420×900 by default.
- Header shows logo, MCP pill, +, ⚙, pin — no model dropdown.
- ⚙ opens a modal with Backend CLI, Model, MCP server sections.
- Picking codex emits a diagnostic and keeps Claude Code active.
- Picking a non-default model still posts the existing `model` IPC.

## Risks And Rollback

- **Risk**: The settings overlay clips at 420px width if its `max-width: 460px` is too
  large. Mitigation: `width: calc(100% - 24px)` and `max-width: 460px` clamp; at 420px
  the overlay is 396px wide (calc).
- **Risk**: Codex picked but not wired confuses users. Mitigation: explicit diagnostic
  via the warn block visual.
- **Rollback**: revert the five files. IPC contract is additive (`cli` is new; nothing
  else changes), so backward-compatible with the previous build.
