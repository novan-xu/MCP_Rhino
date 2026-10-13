# Material catalogue removal

## Background

The user explicitly requested a trash-bin action in PCeditor's Material Setup
project material catalogue. Existing entries can be selected and edited, but
cannot be removed through the dialog.

## Goal

Remove the selected catalogue material through a visible trash button alongside
the material tiles, using the existing Confirm/Cancel save boundary.

## Architecture ownership

This is a standalone `PanelCladdingEditor/UI` change. The dialog owns its copied
material collection; the existing controller and workbook repository persist the
remaining collection on Confirm. No MCP, Rhino geometry, registration, or schema
changes are required.

## Key design

- Reserve a 40-DIP trash button to the right of the wrapping catalogue list.
- Provide a vector icon, accessible name, tooltip, keyboard focus and disabled state.
- Enable removal only for a selected catalogue entry. Remove exactly that entry,
  clear selection/code/description, and update the tile width and action state.
- Leave the working copy pending until Confirm; Cancel discards it. Preserve
  existing panel cell assignments and permit saving an empty catalogue.

## Files

- `src/PanelCladdingEditor/UI/MaterialSetupDialog.xaml`
- `src/PanelCladdingEditor/UI/MaterialSetupDialog.xaml.cs`
- Existing `Project_Test/260818_TEST_material-catalogue-editing/Program.cs` smoke.
- Matching PLAN, EXET, and `Project_Test/261007_TEST_material-catalogue-removal/`.

## Usage

Open Material Setup, select a catalogue tile, press the trash button, then Confirm
to save the remaining materials to the project workbook. Cancel discards removal.

## Acceptance criteria

- Button remains visible beside the list at normal and minimum window sizes.
- No selection means no removal; a selection removes only its material.
- Removal clears stale edit fields and does not select another deletion target.
- The caller's collection and workbook remain unchanged before confirmation.
- Reduced and empty catalogues round-trip through the existing workbook save path.
- Existing selection/edit/add/category/layout checks still pass; build the standalone
  editor in Debug and Release. Use in-process WPF tests/offscreen rendering only.

## Risks and rollback

Deleting an entry removes its availability in the catalogue, without rewriting
existing panel assignments. Cancel recovers all pending changes. Revert the two
dialog files to remove the feature; no migration is required. Existing unrelated
working-tree changes must be preserved. Installation is outside this UI edit.

## Future extensions

Bulk removal or material replacement can be scoped separately if requested.

## Revision (2026-10-07)

The user subsequently requested installation. Package this change as 1.0.94,
verify the compiled assembly GUID against the unchanged product identity, and use
the supported current-user installer while Rhino is closed. Establish registry
persistence with an independent host probe before activation, then run mandatory
Validate and independently verify the registered RHP/hash, exact commands, three
registry timestamps and retained previous RHP. Record evidence in the same TEST
folder and append installation results to EXET. Post-start checks apply if Rhino
is started; this request does not authorize Windows UI automation.
