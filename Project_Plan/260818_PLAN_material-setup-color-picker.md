# Material Setup Color Picker

## Background

The Material Setup window already shows the current custom material color beside the hexadecimal
field, but that swatch is a passive `Border`. Users expect the visible color preview to be the
direct entry point for choosing a custom color.

## Goal

Turn the custom-color preview into a mouse- and keyboard-accessible button. Activating it must open
Rhino's native color selector, initialize the selector from the current custom color, and update
the preview plus `#RRGGBB` field only when the user confirms a color.

## Architecture Ownership

- `UI/MaterialSetupDialog.xaml`: button semantics, visual states, tooltip, and accessibility name.
- `UI/MaterialSetupDialog.xaml.cs`: interaction orchestration and synchronization with the existing
  material color state.
- `UI/MaterialColorPicker.cs`: small Rhino UI adapter isolated behind a testable picker contract.
- `Project_Test/260818_TEST_material-setup-color-picker/`: focused acceptance, cancellation,
  keyboard semantics, styling, and render evidence.
- `Packaging/PanelCladdingEditor/`: versioned package and safe current-user activation or staging.

## Key Design

1. Use `Rhino.UI.Dialogs.ShowColorDialog` with the Material Setup window as modal owner and alpha
   disabled. This preserves Rhino's native color-wheel experience and window behavior.
2. Keep Rhino API conversion behind `IMaterialColorPicker`, allowing deterministic tests without
   displaying a modal dialog or using UI automation.
3. Preserve the existing `CustomColorPreview` element name while changing its type to `Button`, so
   the control has native click, Enter/Space, focus, and automation semantics.
4. Reuse the selected color as the button background and add clear hover, press, and keyboard-focus
   borders without obscuring the color.
5. On confirmation, clear the suggested-palette selection and synchronize `_selectedColor`, the
   preview brush, and the normalized uppercase hex field. On cancellation, do not mutate any of
   those values.
6. Keep direct hex editing and suggested-category colors working through the existing shared
   `SetSelectedColor` path.

## Files Involved

- `src/PanelCladdingEditor/UI/MaterialSetupDialog.xaml`
- `src/PanelCladdingEditor/UI/MaterialSetupDialog.xaml.cs`
- `src/PanelCladdingEditor/UI/MaterialColorPicker.cs` (new)
- `Project_Test/260818_TEST_material-setup-color-picker/` (new)
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj` (standalone test exclusion if required by the
  repository test-source compile glob)
- `Packaging/PanelCladdingEditor/package-manifest.json`
- `Project_Exet/260818_EXET_material-setup-color-picker.md` (after verification)

## Usage

Open Material Setup, click the custom-color swatch, choose a color in Rhino's color selector, and
confirm. The swatch and hex field immediately show the chosen color. The swatch is also reachable
with Tab and can be activated using Enter or Space.

## Test Strategy

The focused WPF smoke will inject deterministic accepting and cancelling picker implementations.
It will verify:

- `CustomColorPreview` is a focusable `Button` with the expected accessible name and hand cursor;
- activating the button passes the current selected color into the picker;
- an accepted color updates the preview, normalized hex text, and effective material color;
- accepting a custom color clears any selected suggested palette swatch;
- cancellation preserves preview, hex text, effective color, and palette selection;
- the button template exposes hover, pressed, and keyboard-focus feedback; and
- an off-screen render retains the intended compact swatch layout.

Existing Material Setup visual-polish and WPF UI smokes will be rerun. Debug/Release direct builds,
solution builds, `git diff --check`, and compiled RHP assembly-identity checks remain required.

## Acceptance Criteria

- Clicking the custom-color preview opens Rhino's native color selector.
- The selector starts at the color currently shown in the preview/hex field.
- Confirming updates the preview and hex field to one consistent opaque `#RRGGBB` value.
- Cancelling makes no color-state changes.
- Direct hex entry and suggested palette selection remain functional.
- The swatch is keyboard accessible and visibly indicates hover/focus/press state.
- Focused and relevant regression tests, builds, identity checks, and safe package validation pass.

## Risks And Rollback

- Rhino owns the native selector's exact appearance; the adapter deliberately depends only on its
  stable confirmed/cancelled result contract.
- Rounding normalized Rhino color components back to bytes is centralized and clamped to avoid
  out-of-range conversion.
- The feature is UI-only. It does not write panel geometry or material workbook data until the
  existing Add material / Use materials actions are invoked.
- Rollback consists of restoring the passive preview border and removing the picker adapter and
  click handler; no data migration is required.

## Future Extensions

- Offer a recent-colors row if repeated custom finish selection becomes a common workflow.
- Add optional numeric RGB/HSL controls if users need exact non-hex editing alongside Rhino's
  selector.
