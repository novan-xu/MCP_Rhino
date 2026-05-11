# 260506_PLAN_rhino-chat-save-safety

## Background

The Rhino-hosted Claude Code panel fallback currently subscribes to `RhinoDoc.EndSaveDocument`.
Its save handler calls the same startup path used for explicit panel opening, which can open UI,
start a per-document MCP pipe, and create a Claude Code session during Rhino save completion.

Save events should not launch chat UI or process/session work. They should only refresh state for
an already-running chat session after `Save` / `SaveAs`.

## Goal

- Prevent Rhino save from starting the Claude Code panel fallback.
- Keep explicit `_Mcpchat` startup behavior intact.
- Preserve path/header refresh for an already-running panel session after `SaveAs`.
- Add a regression smoke check that fails if `EndSaveDocument` starts panel/session work again.

## Architecture Ownership

- `Infrastructure/Plugin/Panel`: owns Rhino panel lifecycle and Rhino document event handling.
- `Project_Test/260506_TEST_rhino-chat-save-safety`: owns the focused regression smoke.
- `Infrastructure/CLI/DeveloperCommandHandler`: owns smoke slug registration.

No MCP tool, skill, agent, or live document accessor contract changes are required.

## Key Design

- Change `PerDocumentPanelDispatcher.OnEndSaveDocument` so it only calls
  `_host.UpdateDocumentPath(document)`.
- Do not call `TryStartDocument` from save events.
- Keep explicit `TryShowDocument` / `_Mcpchat` as the startup path.
- Keep document open/new behavior unchanged for this narrow fix.
- Add a source-level CLI smoke because Rhino save events cannot be driven reliably outside Rhino.

## Involved Files

- `src/MCP_Rhino.Server/Infrastructure/Plugin/Panel/PerDocumentPanelDispatcher.cs`
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs`
- `Project_Test/260506_TEST_rhino-chat-save-safety/DeveloperCommandHandler.RhinoChatSaveSafetySmokeTest.cs`
- `Project_Test/260506_TEST_rhino-chat-save-safety/README.md`
- `Project_Exet/260506_EXET_rhino-chat-save-safety.md`

## Usage

After rebuilding and reloading the `.rhp`, saving a Rhino document must not open or restart the
Claude Code panel. Users should start chat explicitly with:

```powershell
_Mcpchat
```

## Acceptance Criteria

- `dotnet build .\MCP_Rhino.sln -c Release` succeeds.
- `rhino-chat-save-safety-smoke-test` succeeds.
- Existing Claude Code companion/panel CLI smokes still pass.
- Code inspection confirms `OnEndSaveDocument` no longer calls `TryStartDocument`.

## Risks And Rollback

- Removing auto-start-on-save means an unsaved document that becomes saved will not open chat
  automatically. This is acceptable because `_Mcpchat` is now the intended user entry point.
- Rollback is limited to restoring the old save handler, but that reintroduces save-time UI/session
  startup.

## Future Work

- Add a real Rhino live smoke for Save/SaveAs once an automated Rhino command runner is available.
- Add explicit `.rws` worksession handling for chat binding instead of assuming normal `.3dm`
  document semantics.
