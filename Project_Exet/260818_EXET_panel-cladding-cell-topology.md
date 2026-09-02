# Panel Cladding Cell Topology Execution

## Corresponding Plan

- Plan: `Project_Plan/260818_PLAN_panel-cladding-cell-topology.md`
- Execution date: 2026-08-18

## Related Artifacts

- Focused tests: `Project_Test/260818_TEST_panel-cladding-cell-topology/`
- Production implementation:
  - `src/PanelCladdingEditor/UI/PanelCladdingCellTopology.cs`
  - `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml.cs`
  - `src/PanelCladdingEditor/UI/PanelCladdingGridCanvas.cs`
- Commit / PR: none created in this execution.

## Execution Result / Actual Delivered Scope

The editor now projects one logical cell topology from the regular working grid plus the session's
deleted atomic extrusion ids. Each deleted horizontal or vertical INT segment unions its two
adjacent primitive cells. Connected components use the lowest-row, then lowest-column member as the
canonical cell; consequently, deleting the boundary between `0C` and `0D` produces one logical
`0C` cell.

The WPF canvas renders rectangular components as one spanning rectangle. Non-rectangular components
retain their fragments but remove deleted internal boundary gaps and expose one canonical label,
selection key, and hit target. Material drops and assignments use the canonical logical cell and
apply to all primitive members.

Deletion now clears the complete affected connected component, remaps cell selections to canonical
keys, and normalizes parent references that pointed at a member label which became non-canonical.
The existing editor snapshot restores deleted ids, values, labels, and selection in one undo.

Cell-scoped Add H/V now targets the complete logical component rather than one primitive cell. The
placement range uses the component bounds, the intended member bays receive visible new segments,
and the new segments outside the target remain deleted. The regular working grid still supplies
stable row/column remapping:

- Add H creates a row, leaves the lower split label in place, assigns the next row letter to the
  upper split, and shifts all higher rows.
- Add V creates a column, leaves the left split label in place, assigns the next column number to
  the right split, and shifts all columns to the right.
- Unaffected bays connected across hidden new segments render as one logical cell rather than
  exposing phantom cells.

The pre-existing structural-preview boundary remains unchanged. These topology edits remain
reversible editor-session state, and Save stays disabled until structural edits are undone.

## Deviation From Plan

No architectural deviation was required. During regression validation, the existing direct-
interaction smoke was found to assert the original left-inset source expression verbatim. The
renderer retains that established expression and applies the new zero-inset deleted-boundary rule
immediately afterward; this preserves both the historical line-weight contract and the new topology.

## Problems Found And Fixed During Construction

1. The old delete handler only added atomic ids to `_deletedExtrusions` and cleared immediate
   neighbors. It did not rebuild topology, normalize selections, or repaint the primitive boundary.
2. Cell-scoped Add H/V inserted a global regular-grid row or column and hid segments outside the
   target, but the missing topology projection made those hidden segments appear as phantom cells.
3. The previous Add H smoke asserted only that the horizontal-offset count increased. It did not
   verify cells, labels, assignments, Add V, parent references, rendering, or undo.
4. The first direct-interaction regression run failed its source-string line-weight assertion after
   the renderer refactor. The inset calculation was reshaped without changing its numeric behavior,
   and the rerun passed.

## Test Record

### Focused topology smoke

Commands:

```powershell
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-cell-topology\PanelCladdingCellTopologySmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-cell-topology\PanelCladdingCellTopologySmoke.csproj -c Release
```

Final Debug and Release runs both exited `0` and reported:

```text
[OK] Deleted horizontal and vertical INT boundaries produce canonical logical cells.
[OK] Deletion clears the complete affected group, renders one spanning cell, and is undoable.
[OK] Add H creates/renumbers rows without phantom neighboring cells.
[OK] Add V creates/renumbers columns without phantom neighboring cells.
```

The smoke explicitly asserts:

- `0C` + `0D` becomes canonical `0C` with three logical cells in the four-row fixture;
- both merged primitive assignments are cleared and selection changes from `0D` to `0C`;
- one undo restores four cells and both original assignments;
- horizontal and vertical deleted-boundary chains use lower/left labels;
- a three-cell L component remains one non-rectangular logical group;
- scoped Add H produces logical labels `0A,0B,0C,0D,1A,1B,1D`;
- scoped Add V produces logical labels `0A,0B,0C,1B,2A,2B,2C`;
- shifted parent references become `0D` after Add H and column `2` references after Add V;
- undo restores the pre-insertion row/column counts and deleted-segment state.

### Visual QA

The focused smoke generated these deterministic off-screen WPF renders:

- `panel-cell-topology-delete-1200x900.png` — 74,738 bytes
- `panel-cell-topology-add-h-1200x900.png` — 79,025 bytes
- `panel-cell-topology-add-v-1200x900.png` — 78,414 bytes

All three were inspected:

- deletion shows one `0C` cell spanning the former `0C`/`0D` boundary with no INT at that boundary;
- Add H shows target `0B` split into `0B`/`0C`, former `0C` shifted to `0D`, and neighboring `1B`
  spanning the absent segment;
- Add V shows target `0B` split into `0B`/`1B`, the former right column shifted to column `2`, and
  neighboring `0A`/`0C` spanning the absent segments.

### Production builds

Commands:

```powershell
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Debug
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Release
```

Both final builds exited `0` with zero warnings and zero errors and produced the corresponding
`PanelCladdingEditor.rhp` outputs.

### Existing editor regressions

These Release smokes exited `0` after the topology integration:

```powershell
dotnet run --project .\Project_Test\260813_TEST_panel-cladding-latest-ui\PanelCladdingLatestUiSmoke.csproj -c Release
dotnet run --project .\Project_Test\260813_TEST_panel-cladding-direct-interactions\PanelCladdingDirectInteractionsSmoke.csproj -c Release
dotnet run --project .\Project_Test\260813_TEST_panel-cladding-extrusion-fidelity\PanelCladdingExtrusionFidelitySmoke.csproj -c Release
```

They confirmed the existing extrusion action model, merge/undo/add-mullion interaction, direct
assignment, zoom/pan/dimension behavior, aligned frame/divider weights, badge alignment, and
selection halo remain intact.

## Acceptance Criteria Alignment

- Delete `0C`/`0D` boundary and expose canonical `0C`: passed managed and visual assertions.
- Connected horizontal/vertical/non-rectangular groups: passed.
- Add H row creation and renumbering: passed.
- Add V column creation and renumbering: passed.
- No phantom neighboring cells for scoped additions: passed managed and visual assertions.
- Assignment, parent-reference, selection, drag/drop topology behavior: passed focused assertions
  and existing direct-interaction regression.
- Undo restores topology and values: passed for deletion, Add H, and Add V.
- Focused Debug/Release and production Debug/Release validation: passed.

## Rollback Verification

Focused deletion, Add H, and Add V tests each exercised the editor's undo path and restored the
original offsets, deleted-segment set, topology, and values. All tests used stub repositories and
off-screen WPF rendering; no live Rhino document, workbook, package, registry entry, or installation
was mutated.

## Current Remaining Items

- Structural topology remains an editor-session preview. Persisting divider geometry, offsets, and
  logical-cell identities still requires a future application preview/apply contract and one Rhino
  Undo record.
- A live Rhino launch check is useful for final user acceptance, but the production RHP builds and
  managed WPF regressions are complete.

## Conclusion

Panel extrusion deletion and cell-scoped Add H/V now share one deterministic cell-topology model.
Deleted boundaries merge adjacent cells under lower/left labels, new H/V segments create and
renumber only the intended logical cells, neighboring absent segments no longer expose phantom
cells, and all focused/build/regression checks pass.
