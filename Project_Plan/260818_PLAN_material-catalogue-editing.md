# Material Catalogue Editing And Layout

## Background

Material Setup can currently append new materials, but its catalogue is a passive table: selecting
an existing material does not populate the definition fields and duplicate codes are rejected
instead of becoming an edit workflow. The five fixed category buttons also consume substantial
space, the description field is too shallow, the catalogue is visually inconsistent with the main
editor's Drag Materials grid, the material action is separated from the dialog confirmation flow,
and the 32-DIP palette host clips the bottom edge of some suggested-color swatches.

## Goal

Make the Material Setup dialog a compact add-or-edit catalogue editor. Existing materials must be
selectable as color/code tiles and load all their settings into the definition form. The footer
action must show `Save edits` whenever the entered code already exists (case-insensitive), otherwise
`Add material`. Categories must use a dropdown with an adjacent Add category action, descriptions
must have a taller multiline editor, and the sections/footer must follow the requested order and
labels.

## Architecture Ownership

- `UI/MaterialSetupDialog.xaml`: catalogue grid, section order, category dropdown, expanded
  description editor, unclipped palette, and three-button footer.
- `UI/MaterialSetupDialog.xaml.cs`: selection-to-form loading, add/edit resolution by material
  code, action-state synchronization, category collection, and form state coordination.
- `UI/MaterialCategoryDialog.*` and `UI/MaterialCategoryPrompt.cs`: focused category-name input and
  testable modal adapter.
- `Project_Test/260818_TEST_material-catalogue-editing/`: focused behavior, layout, clipping, and
  render coverage.
- `Packaging/PanelCladdingEditor/`: corrected versioned package and safe activation or staging.

## Key Design

1. Replace the catalogue table with a selectable three-column `UniformGrid` of compact tiles that
   reuse the main editor's swatch-plus-code visual language. Description and category remain in the
   editable form and tile tooltip, not in the visible catalogue preview.
2. Move Project Material Catalogue between Project Material Workbook and Material Definition.
3. On catalogue selection, add the material's category to the dropdown if needed, then populate
   code, category, description, custom color preview, normalized hex text, and matching suggested
   palette selection. Guard the load sequence so changing the dropdown does not replace the saved
   material color with the first suggestion.
4. Determine the footer material action solely from the normalized code field. An existing code
   produces `Save edits` and replaces that collection entry in place; a new code produces
   `Add material` and appends a new entry. After either operation, select the resulting tile and
   retain its settings for further editing.
5. Replace the five category buttons with a styled dropdown backed by a dialog-local observable
   category collection. Seed it from the five standard palette categories plus all categories found
   in loaded materials.
6. Add category through a small owned modal text dialog behind `IMaterialCategoryPrompt`, allowing
   deterministic testing without UI automation. Trim names, reject empty/duplicate values, and
   select a newly added category. A custom category uses the existing Composite suggested palette
   fallback. Because the workbook stores category per material, a custom category persists once a
   material using it is confirmed to the catalogue.
7. Arrange the definition as two columns: code plus suggested colors/help on the left, and a taller
   wrapping description editor on the right whose bottom aligns with the suggested-color help line.
8. Increase the palette host above its 32-DIP item height so all borders/rings render inside the
   viewport.
9. Move the orange `Add material` / `Save edits` action into the footer between equal-width Cancel
   and Confirm buttons. Rename `Use materials` to `Confirm` and update related status copy.

## Files Involved

- `src/PanelCladdingEditor/UI/MaterialSetupDialog.xaml`
- `src/PanelCladdingEditor/UI/MaterialSetupDialog.xaml.cs`
- `src/PanelCladdingEditor/UI/MaterialCategoryDialog.xaml` (new)
- `src/PanelCladdingEditor/UI/MaterialCategoryDialog.xaml.cs` (new)
- `src/PanelCladdingEditor/UI/MaterialCategoryPrompt.cs` (new)
- `Project_Test/260818_TEST_material-catalogue-editing/` (new)
- relevant existing Material Setup WPF smoke assertions
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj` (standalone test exclusion)
- `Packaging/PanelCladdingEditor/package-manifest.json`
- `Project_Exet/260818_EXET_material-catalogue-editing.md` (after verification)

## Usage

1. Open Material Setup.
2. Select a catalogue tile to edit that material; its code, category, description, and color load
   into Material Definition and the orange footer action reads `Save edits`.
3. Change the desired attributes and choose Save edits.
4. Enter an unused code to create another material; the footer action changes to `Add material`.
5. Use Add category to create and select a custom category, then assign it to a material.
6. Choose Confirm to save/use the catalogue, or Cancel to close without accepting it.

## Test Strategy

The focused WPF smoke will verify:

- section order is workbook → catalogue → definition;
- the catalogue uses a selectable three-column uniform grid and each tile visibly contains only
  swatch plus code;
- selecting an existing tile loads exact code, category, description, preview, hex, and palette
  state;
- an existing code switches the orange action to Save edits and replaces the matching entry
  without changing collection count;
- an unused code switches the action to Add material and increases collection count;
- added categories appear once, become selected, and use the fallback palette;
- the category dropdown and Add category button replace the five-button selector;
- the multiline description editor keeps its column width and aligns vertically with the
  suggested-color help line;
- all suggested-color item bounds fit within the taller palette viewport;
- the footer contains equal-width Cancel, material action, and Confirm buttons in that order; and
- 720 × 740 and compact-size off-screen renders remain usable.

Existing WPF UI, visual polish, material catalogue, latest UI, and color-picker smokes will be
updated or rerun as applicable. Serial Debug/Release solution builds, direct plug-in builds,
assembly identity validation, `git diff --check`, package construction, and safe install/staging
remain required.

## Acceptance Criteria

- Selecting a catalogue material populates every definition attribute without changing its color.
- Existing codes produce Save edits and update exactly one material; unused codes produce Add
  material and create exactly one material.
- Category is a dropdown, Add category works, and loaded custom categories remain selectable.
- Description is multiline and extends to the suggested-color help baseline.
- Catalogue is a three-column swatch/code tile grid located above Material Definition.
- The three equal footer buttons read Cancel, Add material/Save edits, and Confirm, with the middle
  action visually orange.
- Suggested category color swatches are fully visible without bottom clipping.
- Focused behavior/layout/visual tests and relevant regressions/build/identity/package checks pass.

## Risks And Rollback

- Replacing immutable material objects is required for edits; preserving collection index and
  reselection keeps observable UI state stable.
- A custom category without any saved material has no workbook row and therefore is session-only.
  Once used by a material, it round-trips through the existing Category column.
- Renaming an existing material code is intentionally not inferred: changing to an unused code
  follows the requested rule and adds a new material, leaving the old code available for existing
  panel assignments.
- The change edits the dialog's in-memory copy only until Confirm follows the existing save path.
  Cancel remains non-mutating to the main editor collection/workbook.
- Rollback restores the former table, category buttons, single Add action, and two-button footer; no
  workbook or Rhino data migration is required.

## Future Extensions

- Add explicit delete/archive material behavior with assignment-impact warnings.
- Add a dedicated rename workflow that can preview affected panel assignments before changing a
  material code.
- Persist unused category definitions in a separate workbook category table if standalone category
  management becomes necessary.
