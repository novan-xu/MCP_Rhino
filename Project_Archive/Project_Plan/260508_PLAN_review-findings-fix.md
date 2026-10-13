# Review Findings Fix Plan

## Background

The project code review found several behavior and governance drift issues after recent MCP surface changes:

- mutation failures can leave partial live Rhino document changes because failed `ExecuteWithUndo` calls close the undo record but do not roll it back
- `FilterObjects` bypasses the layer ambiguity confirmation path while still accepting broad `layerQueries`
- several active MCP tool descriptions still mention disk `.3dm` reads/writes or contain mojibake, which weakens model routing
- runtime workflow examples and legacy user-facing strings still reference removed or malformed tool names/messages

## Goals

- Roll back a live Rhino undo record when an `ExecuteWithUndo` operation fails after producing an undoable document change.
- Restore ambiguity protection for the canonical structured `FilterObjects` MCP tool.
- Replace stale or malformed MCP descriptions with live-document, routing-safe English descriptions.
- Update stale workflow examples to the canonical current tool names.
- Add a focused smoke test that prevents the same drift from returning.

## Architecture Ownership

- Undo behavior belongs in `Infrastructure/Rhino/Live/LiveRhinoDocumentAccessorBase.cs`, because it owns Rhino UI-thread document access and undo record wrapping.
- Canonical filter routing belongs in `Tools/Analysis/FilterObjectsTool.cs`; it may call the existing live selection skill rather than duplicating resolver logic.
- User-facing skill strings remain in `Skills/`.
- MCP routing metadata remains in tool `[Description]` attributes, matching the MCP surface governance rules.
- Regression coverage belongs in a new `Project_Test/260508_TEST_review-findings-fix/` smoke folder.

## Key Design

- Capture the current undo record serial before starting the tool undo record.
- On failed work response or thrown exception, close the undo record and call `RhinoDoc.Undo()` only if the current undo serial changed, avoiding accidental rollback of a previous user action when no change happened.
- Make `FilterObjectsTool` delegate to `LiveObjectSelectionSkill.Select(...)`, which already resolves `layerQueries` and fails on ambiguous candidates while returning `OperationResponse<RhinoObjectFilterResult>`.
- Normalize malformed Chinese/mojibake descriptions and messages to concise English.
- Add static smoke assertions for:
  - rollback guard presence
  - `FilterObjects` ambiguity-safe routing
  - no active MCP description contains stale disk-overwrite/mojibake terms
  - workflow example uses `GetLayers`

## Involved Files

- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoDocumentAccessorBase.cs`
- `src/MCP_Rhino.Server/Tools/Analysis/FilterObjectsTool.cs`
- active tool files under `src/MCP_Rhino.Server/Tools/`
- `src/MCP_Rhino.Server/Skills/Inspection/CompositeObjectFilterSkill.cs`
- `src/MCP_Rhino.Server/Skills/Editing/ObjectSelectionSkill.cs`
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs`
- `Runtime_Workflow/MCP_Rhino Workflow.md`
- `Project_Test/260508_TEST_review-findings-fix/`

## Usage

No runtime command changes are expected for users. Existing MCP tool names remain canonical.

The new developer smoke command will be:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- review-findings-fix-smoke-test
```

## Acceptance Criteria

- Debug and Release solution builds pass.
- Existing MCP tool safety and overlap cleanup smokes still pass.
- New `review-findings-fix-smoke-test` passes in Debug and Release.
- `FilterObjects` still returns structured `RhinoObjectFilterResult` but no longer applies ambiguous layer substring queries directly.
- Active MCP tool descriptions do not advertise disk `.3dm` mutation semantics.

## Risks And Rollback

- Calling `RhinoDoc.Undo()` after a failed operation must not undo unrelated user work. The serial guard limits rollback to cases where the just-closed record became the current undo record.
- If Rhino undo behavior differs in a future Rhino version, the rollback path returns an explicit rollback failure message instead of silently hiding partial mutation.
- Rollback is limited to undoable Rhino document operations. External open-world exports are not covered by `ExecuteWithUndo` and keep their own tool contracts.

## Future Extensions

- Add live Rhino smoke coverage that intentionally fails after a partial mutation and verifies object counts after automatic rollback.
- Consolidate duplicate layer-query ambiguity resolution into a shared application service.
