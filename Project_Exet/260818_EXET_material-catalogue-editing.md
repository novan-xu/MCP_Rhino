# Material Catalogue Editing And Layout Execution

## Corresponding Plan

- Plan: `Project_Plan/260818_PLAN_material-catalogue-editing.md`
- Execution date: 2026-08-18

## Related Artifacts

- Material Setup layout: `src/PanelCladdingEditor/UI/MaterialSetupDialog.xaml`
- Add/edit state flow: `src/PanelCladdingEditor/UI/MaterialSetupDialog.xaml.cs`
- Category modal: `src/PanelCladdingEditor/UI/MaterialCategoryDialog.xaml` and `.xaml.cs`
- Category prompt adapter: `src/PanelCladdingEditor/UI/MaterialCategoryPrompt.cs`
- Focused tests: `Project_Test/260818_TEST_material-catalogue-editing/`
- Visual evidence:
  - `material-catalogue-editing-720x740.png`
  - `material-catalogue-editing-620x640.png`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.35/`
- Staged bundle:
  `%LOCALAPPDATA%/PanelCladdingEditor/staged/1.0.35-20260819010337112/`
- Active installed version during execution: `1.0.32`
- Commit / PR: none created in this execution.

## Execution Result / Actual Delivered Scope

Material Setup is now an add-or-edit catalogue workflow. The Project Material Catalogue section was
moved between workbook and definition and replaced with a selectable three-column tile grid. Each
tile follows the main editor's Drag Materials language: a small material-color swatch plus material
code, with no visible description column. Hover, keyboard focus, and selected borders remain clear.

Selecting a tile loads its code, category, description, exact color, hex value, and corresponding
suggested palette state into Material Definition. The load is guarded so selecting the category does
not replace a saved custom color with that category's first suggestion. A matching suggestion is
selected when one exists; otherwise no misleading palette tile remains selected.

The footer's middle orange action derives its label and behavior from the normalized code field:

- an existing case-insensitive code shows `Save edits` and replaces that immutable material object
  at the same collection index; and
- an unused code shows `Add material` and appends exactly one new object.

Both paths select the resulting catalogue tile and retain its form settings for continued editing.
Changing away from the selected tile's code clears the stale tile selection while preserving the
code-based action rule.

The former five category buttons are now one 40-DIP dropdown with an adjacent Add category button.
Standard categories and all custom categories found in loaded materials seed a dialog-local
observable list. The new owned category-name dialog validates blank and case-insensitive duplicate
names, supports Enter/Escape, and is isolated behind `IMaterialCategoryPrompt` for deterministic
testing. Custom categories use the Composite fallback suggestion palette and persist naturally
through the existing material Category workbook column once used by a material.

The description field keeps its existing right-column width but is now a 128-DIP wrapping multiline
editor. Its bottom aligns with the suggested-variation help line on the left. The suggested-color
host increased from 32 to 36 DIPs while its items remain 32 DIPs, eliminating the reported bottom
clipping. The footer now contains equal-width Cancel, orange Add material/Save edits, and green
Confirm buttons; `Use materials` and related status copy were renamed to `Confirm`.

## Differences From Plan

Implementation followed the plan. The description height settled at 128 DIPs rather than using an
estimated value: actual WPF layout measurement showed that height aligns its bottom within 0.4 DIP
of the suggested-color help bottom at the target size.

Four Rhino processes remained open, so package `1.0.35` was validated and staged instead of
overwriting loaded `1.0.32`. This package supersedes the earlier staged `1.0.34`; users should run
only the `1.0.35` staged installer.

## Problems Found And Fixed During Construction

1. The first focused-test compile used the general `Visual` type as `TranslatePoint`'s relative
   target, while WPF's framework-element overload requires `UIElement`. The test helpers were narrowed
   to the correct type.
2. The initial 126-DIP description height ended 2.4 DIPs above the variation-help bottom. Actual
   layout evidence was used to set 128 DIPs and verify the resulting alignment.
3. Existing visual-polish regression assumptions treated every text box as a 40-DIP single-line
   field and the category selector as a ListBox. Those assertions were updated to distinguish the
   intentional 128-DIP multiline description, 40-DIP ComboBox, and 36-DIP unclipped palette.
4. Material objects expose immutable init-only attributes. Edit behavior therefore replaces the
   matching observable-collection entry rather than attempting mutation, retaining domain
   immutability and collection notification correctness.

## Test Record

### Focused catalogue-editing smoke

Commands:

```powershell
dotnet run --project .\Project_Test\260818_TEST_material-catalogue-editing\MaterialCatalogueEditingSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_material-catalogue-editing\MaterialCatalogueEditingSmoke.csproj -c Release
```

Both final runs exited `0` and reported:

```text
[OK] Catalogue selection loads exact material attributes without color replacement.
[OK] Existing codes save in place; unused codes add exactly one material.
[OK] Category dropdown/addition and case-insensitive duplicate protection passed.
[OK] Catalogue grid, expanded description, palette bounds, and equal footer passed.
```

The smoke verifies:

- workbook → catalogue → definition section order;
- a selectable three-column uniform catalogue grid;
- exactly one visible code label plus color swatch in each tile;
- selection-to-form loading for code, category, description, preview, hex, and palette;
- preservation of an exact non-palette custom color;
- Save edits replacement with unchanged count and complete attribute update;
- Add material append with count increased by exactly one;
- case-insensitive category deduplication and custom-category restoration in a new dialog session;
- five fallback suggestions for custom categories;
- dropdown/button 40-DIP alignment;
- 128-DIP multiline description and variation-help bottom alignment;
- every 32-DIP palette item fitting within the 36-DIP host;
- equal footer widths, correct order/labels, and exact orange `#D97706` action color; and
- deterministic normal and compact off-screen renders.

### Visual QA

Both focused renders were inspected:

- `material-catalogue-editing-720x740.png` — 48,086 bytes. The complete normal layout shows the
  three-column catalogue, dropdown/add action, aligned expanded description, full swatches, custom
  color row, and equal sticky footer.
- `material-catalogue-editing-620x640.png` — 44,204 bytes. The compact layout keeps the footer fixed,
  preserves usable catalogue/category/definition widths, and exposes remaining content through the
  existing vertical scroll area.

### Existing regressions

The following Debug smokes exited `0` after the change:

- Material Setup color picker;
- panel-cladding WPF UI;
- visual polish (updated desired-layout assertions);
- material catalogue round trip; and
- latest UI.

These retain native color-picker confirmation/cancellation, broader editor behavior, workbook
catalogue persistence, rounded visual language, and normal/compact rendering.

### Builds and plug-in identity

- `MCP_Rhino.sln` Debug with `-m:1`: PASS, zero warnings/errors.
- `MCP_Rhino.sln` Release with `-m:1`: PASS, zero warnings/errors.
- Direct PanelCladdingEditor Debug RHP build: PASS, zero warnings/errors.
- Direct PanelCladdingEditor Release RHP build: PASS, zero warnings/errors.
- Debug assembly identity: PASS.
- Release assembly identity: PASS.
- Packaged Release assembly identity: PASS.
- PanelCladdingEditor assembly-level plug-in GUID:
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, manifest-matching and distinct.
- `git diff --check`: PASS; existing line-ending notices only.

### Package and staging

- Version increment: `1.0.34` → `1.0.35` in the active construction state.
- Bundle build: PASS.
- Rhino process count before activation: `4`.
- Supported installer result: safely staged; active installation not changed.
- Bundle/staged RHP SHA-256:
  `2D3BE4DD1643E3670EF61525CA2ED677B4C86526B46CF2D21FD279E2C3E64EE7`.
- Active installed version remains `1.0.32` until every Rhino process closes.

## Acceptance Criteria Alignment

- Catalogue selection populates every attribute without changing saved color: passed.
- Existing codes save exactly one edit; unused codes add exactly one material: passed.
- Category dropdown and Add category behavior: passed, including custom reload and duplicate guard.
- Expanded description size and help-line alignment: passed measured layout assertion.
- Catalogue tile grid, new section order, and description-free previews: passed.
- Equal Cancel / orange material action / Confirm footer: passed.
- Suggested-color bottom clipping: passed every-container bounds assertion and visual inspection.
- Focused Debug/Release, relevant regressions, builds, identities, package, and staging: passed.

## Rollback Verification

All edits occur in Material Setup's existing in-memory material copy until Confirm invokes the
existing catalogue save path. Cancel therefore remains non-mutating to the main editor collection
and workbook. The feature does not alter Rhino geometry, panel user text, or the workbook schema.
Restoring the former XAML and add-only handler removes the workflow without migration. Active
installed `1.0.32` remains untouched.

## Current Remaining Items

- Close every Rhino window.
- Run the staged `1.0.35` installer (not the superseded `1.0.34` stage):

```powershell
powershell -ExecutionPolicy Bypass -File "C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.35-20260819010337112\Installer\Install-PanelCladdingEditor.ps1" -Mode Install -BundleRoot "C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.35-20260819010337112"
```

- Restart Rhino and interactively select, edit, and confirm one project material. No Rhino UI
  automation or production document mutation was performed.

## Conclusion

Material Setup now supports direct existing-material editing and new-material creation through one
code-aware orange footer action. Categories are compact and extensible, descriptions have the
requested working area, the catalogue matches the main editor's tile grid, footer semantics are
clear, and suggested swatches no longer clip. Version `1.0.35` is fully regression-tested,
visually verified, identity-validated, and safely staged for activation after Rhino closes.
