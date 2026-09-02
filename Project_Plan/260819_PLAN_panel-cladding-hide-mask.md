# Panel Cladding Hide Mask PLAN

## Background

The panel cladding model currently distinguishes only present/missing atomic extrusion segments (`CW_2.05_SEGMENT_MASK`) and merged curve runs (`CW_2.06_MERGE_MASK`). A material boundary can require separate logical cladding cells even when no physical intermediate extrusion exists behind that boundary. Treating those absent extrusions as deleted is incorrect because deletion merges adjacent logical cells and can collapse/renumber a complete H/V track.

Panel `PID_BKT_N1_01_07` is the motivating condition: the MPL-002 boundary must remain visible in the cladding layout while `INT_B0`, `INT_B1`, `INT_C0`, and `INT_C1` are absent from generated extrusion geometry.

## Goal

- Add `CW_2.07_HIDE_MASK` as a panel-level, dimensioned topology payload.
- Add a **Hide** action beside Merge and Explode in extrusion view.
- Keep hidden segments in the logical cell grid while omitting them from physical curve generation.
- Make hidden segments visibly distinct and selectable in the editor so the action is reversible.
- Persist, load, match, synchronize, and include the hide mask in panel type identity.

## Architecture ownership

- Topology state and payload contracts remain in Domain.
- Binary mask validation and serialization remain in `PanelCladdingKeyService` in Application.
- Physical curve omission remains in `PanelCladdingExtrusionPlanningService`.
- Editor interaction and hidden-state visualization remain in UI.
- Rhino persistence/synchronization remains in the live Infrastructure repositories.
- Regression coverage belongs in `Project_Test/260819_TEST_panel-cladding-hide-mask/`.

## Key design

1. `HiddenSegments` is independent from `MissingSegments`.
2. A segment cannot be both hidden and missing, and merge runs cannot cross either state because neither produces a physical extrusion there.
3. Hide-mask bits default to `false`; `true` means the atomic segment is logically retained but physically suppressed.
4. Missing segments continue to drive logical cell merging and full-track collapse. Hidden segments never participate in that collapse.
5. The editor retains hidden atoms in its selectable extrusion graph, renders them dashed/muted, and changes the action to **Unhide** when every selected editable atom is hidden.
6. `PCSpawnCrv` omits hidden atoms. `PCMatchCrv` copies all three topology masks.
7. Existing panels with only segment/merge masks remain readable as having no hidden segments. The next extrusion save writes all three masks.
8. Surface sync preserves existing hidden state on a retained grid. Curve sync preserves explicitly hidden atoms when reconciling spawned geometry, so an absent curve caused by the hide mask is not converted into a deleted logical divider.

## Files involved

- `src/PanelCladdingEditor/Domain/PanelCladdingModels.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladdingKeyService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladdingSaveService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladdingTypeSignatureService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/*`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/*`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml*`
- `src/PanelCladdingEditor/UI/PanelCladdingGridCanvas.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingExtrusion.cs`
- `Project_Test/260819_TEST_panel-cladding-hide-mask/`
- `Packaging/PanelCladdingEditor/package-manifest.json`

## Acceptance criteria

- Saving extrusion edits writes a valid `CW_2.07_HIDE_MASK`.
- Reloading restores the same hidden atomic segments.
- Hiding `INT_B0`, `INT_B1`, `INT_C0`, and `INT_C1` does not remove offsets, merge cells, or renumber logical cells.
- Hidden segments remain visible as an editor-only dashed state and can be unhidden.
- `PCSpawnCrv` produces no Rhino curves for hidden atoms.
- `PCMatchCrv` copies segment, merge, and hide masks while continuing to ignore numerical offset differences.
- Existing two-mask panels decode successfully with an empty hidden set.
- Focused Debug/Release smoke, relevant regressions, solution builds, package creation, and assembly identity validation pass.

## Risks and rollback

- Old plug-in versions will ignore `CW_2.07_HIDE_MASK`; the segment and merge payloads remain unchanged for backward readability.
- Curve sync can only preserve explicit hidden state when it can map the stored grid to the geometry-derived grid. Ambiguous remaps must fail or fall back conservatively rather than silently converting hidden divisions into deletions.
- Roll back by reinstalling package `1.0.55`; the additional user-text key is non-destructive and can be ignored or removed by a later save.
