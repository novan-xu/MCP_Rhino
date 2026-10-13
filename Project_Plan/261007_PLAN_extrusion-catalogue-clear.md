# Extrusion catalogue clear

## Background

The user explicitly requested a Clear button in Extrusion Setup that removes all
configured and unconfigured extrusions, clears the extraction path, and asks for
confirmation after the button is clicked. This authorizes capability construction.

## Goal

Provide a footer Clear all action with confirmation and a complete reset of the
dialog's working catalogue and schedule PDF selection.

## Architecture ownership

The standalone `PanelCladdingEditor/UI` owns the button, confirmation prompt, and
working-copy reset. Existing controller/workbook persistence owns saving on the
main Confirm button. No MCP, schema, geometry or installer changes are needed.

## Key design

- Place a red-text Clear all button immediately before Cancel and Confirm.
- Ask a modal Yes/No confirmation, defaulting to No, explaining that configured
  and unconfigured entries and the PDF path will be cleared.
- A declined prompt changes nothing. Acceptance empties both lists, clears the
  PDF path, selected profile, preview, values and parent options, resets counts
  and reports the next step. Workbook selection remains available for saving.
- Preserve the dialog's existing working-copy boundary: main Confirm persists
  the clear, main Cancel discards it. Existing panel assignments remain unchanged.
- Loading an empty workbook catalogue also clears any stale PDF path inherited
  from the caller, so saved empty state reopens empty and ready for extraction.
- Follow the existing injectable UI-prompt pattern to test acceptance and decline
  without Windows UI automation. Keep existing constructor call sites compatible.

## Files

- `src/PanelCladdingEditor/UI/ExtrusionSetupDialog.xaml` and `.xaml.cs`.
- `src/PanelCladdingEditor/UI/ExtrusionCatalogueClearPrompt.cs`.
- Existing `Project_Test/260820_TEST_panel-extrusion-assignment/Program.cs` smoke.
- Matching PLAN, EXET and `Project_Test/261007_TEST_extrusion-catalogue-clear/`.

## Usage

Open Extrusion Setup, choose Clear all, then Yes to clear the working catalogue
and PDF path. Select and extract another PDF, or choose Confirm to save the empty
catalogue. No dismisses the prompt; Cancel closes setup without saving changes.

## Acceptance criteria

- Clear all is visible at normal and minimum window sizes.
- Decline preserves profile selection, both lists, fields, PDF path and workbook.
- Accept clears configured/unconfigured entries, editor state and extraction path.
- Original caller state and workbook stay unchanged until the existing save path
  is used; an empty saved catalogue reloads without a stale PDF path.
- A new extraction works after clearing and does not inherit former configuration.
- Existing extrusion smoke checks and standalone Debug/Release builds pass.

## Risks and rollback

Clear is confined to the project catalogue; it does not remove geometry, assigned
panel definitions, the source PDF or workbook file. Cancel discards the working
copy. Revert the dialog/prompt change to remove the feature without migration.
Preserve unrelated working-tree edits. Deployment is outside this requested edit.

## Future extensions

Selective profile removal can be scoped separately if requested.

## Revision (2026-10-07)

The user's follow-up explicitly requests installation. Build version 1.0.95 and
verify its assembly-level identity and package hashes. Use the independent hidden
host launch mechanism already attested during this chat's 1.0.94 installation,
with fresh pre-install host/provider agreement. Install only while Rhino is closed,
run mandatory Validate, then verify from another completed host process that the
new RHP/hash and exact commands are registered and all three timestamps advanced.
Retain and verify the 1.0.94 rollback copy. Append deployment evidence to TEST/EXET.
