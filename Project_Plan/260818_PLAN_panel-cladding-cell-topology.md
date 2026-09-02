# Panel Cladding Cell Topology

## Background

The Panel Cladding Editor currently treats extrusion deletion as a visual-only operation. Deleting
an intermediate (`INT`) segment adds its atomic id to the session's deleted-segment set and clears
the immediately adjacent assignments, but the WPF grid continues to render and select both
primitive cells. For example, deleting the segment between `0C` and `0D` leaves two labeled cells
instead of one merged `0C` cell.

The same missing topology projection affects cell-scoped **Add H** and **Add V**. The editor inserts
a global row or column, hides the new atomic segments outside the selected target, and remaps the
underlying regular-grid keys. Without rebuilding connected cell groups from those hidden segments,
unaffected bays display phantom cells instead of remaining one logical cell.

## Goal

Make deleted intermediate extrusion segments define the editor-session cladding topology. Adjacent
primitive cells connected across deleted boundaries must behave and render as one logical cell with
the lower/left canonical label. Ensure cell-scoped **Add H** and **Add V** create the intended new
cells, renumber shifted rows or columns, and leave unaffected bays topologically merged.

## Architecture Ownership

- `src/PanelCladdingEditor/UI/`: owns the reversible editor-session extrusion and cell-topology
  projection, topology-aware selection, Add H/V targeting, and WPF rendering.
- Existing `Application/`, `Domain/`, and `Infrastructure/Rhino/` persistence contracts remain
  unchanged. Structural edits remain session previews and continue to use the existing save guard.
- `Project_Test/260818_TEST_panel-cladding-cell-topology/`: owns focused managed and WPF regression
  evidence for topology, labels, assignments, rendering, and undo.

## Key Design

1. Add a focused cell-topology projection that starts from the regular `PanelCladdingLayout.Cells`
   grid and unions the two adjacent cells for every deleted atomic horizontal or vertical segment.
2. Represent each connected component as one logical group. Select the canonical representative by
   lowest row, then lowest column, so deleting the boundary between `0C` and `0D` produces `0C`.
3. Render rectangular groups as one spanning cell. Render non-rectangular groups as connected
   fragments with deleted internal boundaries filled, while showing content and hit-testing under
   one canonical key.
4. Normalize selection, material drops, assignment clearing, and parent choices to topology-group
   representatives. Assignment changes to a logical cell apply to all primitive members.
5. When adding a cell-scoped H/V segment, target the complete topology group, use its geometric
   bounds for the placement dialog, clear both sides of the intended split, remap shifted keys and
   parent references, and delete new atomic segments only outside the group's member bays.
6. Preserve the existing undo snapshot as the authority for offsets, values, deleted segments,
   merged extrusion groups, selections, and structural-dirty state.

## Files Involved

- `src/PanelCladdingEditor/UI/PanelCladdingCellTopology.cs` (new)
- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingGridCanvas.cs`
- `Project_Test/260818_TEST_panel-cladding-cell-topology/` (new)
- `Project_Exet/260818_EXET_panel-cladding-cell-topology.md` (after verified execution)

## Usage

In **Extrusion view**, select and delete an intermediate segment. The cells on its two sides become
one logical cell under the lower/left label. Use **Add H** or **Add V**, then click a logical cell;
the new segment splits that target, creates unassigned cells on both sides, and renumbers cells above
or to the right while unaffected neighboring bays remain merged.

## Acceptance Criteria

- Deleting the INT boundary between `0C` and `0D` renders and selects one cell labeled `0C`.
- Multiple deleted boundaries produce stable connected groups with deterministic canonical labels.
- Deleted horizontal and vertical boundaries behave symmetrically.
- Add H creates and renumbers rows; Add V creates and renumbers columns.
- Cell-scoped Add H/V splits the complete selected topology group and does not expose phantom cells
  in bays where the new segment is absent.
- Affected logical-cell assignments are cleared; shifted parent references remain valid.
- Selection, material assignment, and drag/drop use canonical logical-cell keys.
- One editor undo restores topology, values, labels, offsets, and selection.
- Focused smoke tests and PanelCladdingEditor Debug/Release builds pass.

## Risks And Rollback

- Non-rectangular connected components cannot be represented by one WPF rectangle. The renderer
  will use multiple filled fragments with one canonical label and one logical hit target.
- Structural state is still not persisted to Rhino geometry. The existing structural-dirty save
  guard remains in place and no new mutation authority is introduced.
- The working tree already contains active PanelCladdingEditor changes. This work will preserve
  those changes and touch only the focused files listed above.
- Rollback consists of removing the new topology projection and reverting the focused window/canvas
  integrations; no live Rhino document, workbook, package, or installation is modified.

## Future Extensions

- Promote the editor-session topology into an application-level preview/apply contract when Rhino
  geometry, offsets, synthetic keys, and one-record Undo persistence are designed.
- Persist stable logical-cell identities independently of their displayed row/column labels.
- Add live Rhino visual acceptance once structural persistence exists.
