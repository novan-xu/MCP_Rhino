# Panel Cladding Scoped Save PLAN

## Background

The editor currently has one `Save` action that always persists cladding assignments, H/V offsets, and canonical segment/merge masks together. As a result, merely saving cladding on a panel whose extrusion topology has never been configured creates default masks that imply an explicit configuration. `PCSpawnCrv` then cannot distinguish that implicit editor default from a topology the user intentionally confirmed.

The footer also exposes `Exit` and one combined `Save`, so users cannot independently commit extrusion work or cladding work.

## Goal

- Preserve missing segment/merge mask keys as the canonical “extrusions not configured” state.
- Reject `PCSpawnCrv` when either explicit topology mask is absent or blank.
- Replace the editor footer with equal-width `Save Extrusions`, `Save Cladding`, and `Save Both` actions.
- Make `Save Extrusions` and `Save Both` explicitly persist masks even when every default intermediate segment is present and divided.
- Make `Save Cladding` update cladding cells/type data without creating, deleting, or modifying extrusion topology attributes.

## Architecture ownership

- Save scope contract: `src/PanelCladdingEditor/Domain/PanelCladdingModels.cs`
- Scoped attribute transaction: `src/PanelCladdingEditor/Application/Services/PanelCladdingSaveService.cs`
- Explicit-mask detection: `src/PanelCladdingEditor/Application/Services/PanelCladdingKeyService.cs`
- Curve-spawn guard: `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSpawnService.cs`
- Editor state and commands: `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml` and `.xaml.cs`
- Regression coverage: `Project_Test/260819_TEST_panel-cladding-scoped-save/`

## Key design

1. Add an explicit save scope with `Extrusions`, `Cladding`, and `Both` values; retain `Both` as the request default for API compatibility.
2. The extrusion scope writes H/V offsets, unit dimensions, segment mask, and merge mask, and removes only obsolete offset/topology keys.
3. The cladding scope writes surviving logical cell keys plus type/signature data and removes only obsolete cladding/type keys. It leaves all extrusion keys untouched, including their absence.
4. The combined scope performs both scoped mutations in one attribute transaction.
5. Track extrusion and cladding dirty state independently. Structural edits also mark cladding dirty because they change the logical cell set.
6. Reject a cladding-only save while unsaved structural edits exist; the user must save extrusions first or use `Save Both`, preventing cladding keys from being committed against an unpersisted grid.
7. A successful partial save clears only its own dirty scope and preserves the other unsaved editor state. A successful combined save reloads the panel.
8. `PCSpawnCrv` validates that both canonical mask keys exist and contain nonblank payloads before planning or generating any curves. `PCSpawnSrf` remains independent of this requirement.

## Files involved

- `src/PanelCladdingEditor/Domain/PanelCladdingModels.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladdingKeyService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladdingSaveService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSpawnService.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml.cs`
- `Project_Test/260819_TEST_panel-cladding-scoped-save/`
- `Packaging/PanelCladdingEditor/package-manifest.json`

## Usage

- Use `Save Cladding` after material or parent-cell changes when the extrusion layout has not changed. This action must preserve missing topology masks.
- Use `Save Extrusions` after any H/V divider, segment delete, merge, explode, or dimension change. This action intentionally configures the topology, including a complete default topology.
- Use `Save Both` to commit both scopes in one transaction.
- Run `PCSpawnCrv` only after `Save Extrusions` or `Save Both` has established both topology mask keys.

## Acceptance criteria

- Saving cladding on a panel with no topology masks leaves both masks absent.
- Saving extrusions writes both masks even for the complete default topology.
- Saving both writes extrusion and cladding attributes in one transaction.
- Extrusion-only save preserves all existing cladding values and type/signature data.
- Cladding-only save preserves all existing offsets and topology masks, or preserves their absence.
- The editor contains no `Exit` button and presents the three requested save buttons at equal widths.
- Partial saves retain unsaved work in the other scope and close confirmation remains active while either scope is dirty.
- `PCSpawnCrv` rejects panels missing either mask before generating any curve, with an actionable diagnostic.
- `PCSpawnSrf` is unaffected.
- Focused Debug/Release tests, existing regressions, solution builds, package creation, RHP identity validation, and staged/install validation pass.

## Risks and rollback

- Partial saves can produce temporarily divergent extrusion/cladding attributes. The UI blocks cladding-only persistence while structural changes remain unsaved and keeps the outstanding dirty state visible.
- Existing callers that omit scope must retain combined-save behavior. The model default remains `Both`.
- Roll back by removing the scope contract and spawn guard, restoring the two-button footer and original unified save transaction, and reinstalling the prior package.

## Future extensions

- Add separate saved/unsaved badges per scope if users need more persistent visual status than the combined badge and footer message.
- Add a non-mutating extrusion-configuration indicator beside the extrusion-view tab.
