# Material Setup Color Picker Execution

## Corresponding Plan

- Plan: `Project_Plan/260818_PLAN_material-setup-color-picker.md`
- Execution date: 2026-08-18

## Related Artifacts

- Production UI: `src/PanelCladdingEditor/UI/MaterialSetupDialog.xaml`
- Interaction orchestration: `src/PanelCladdingEditor/UI/MaterialSetupDialog.xaml.cs`
- Rhino picker adapter: `src/PanelCladdingEditor/UI/MaterialColorPicker.cs`
- Focused tests: `Project_Test/260818_TEST_material-setup-color-picker/`
- Visual evidence: `material-setup-color-picker-accepted.png`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.34/`
- Staged bundle:
  `%LOCALAPPDATA%/PanelCladdingEditor/staged/1.0.34-20260819004509769/`
- Active installed version during execution: `1.0.32`
- Commit / PR: none created in this execution.

## Execution Result / Actual Delivered Scope

The passive 40-DIP `CustomColorPreview` border is now a real WPF button. It retains the same compact
rounded swatch appearance while adding a hand cursor, tooltip, accessible automation name, Tab
navigation, native Enter/Space activation, and dedicated hover, pressed, and keyboard-focus states.

Activating the swatch calls Rhino 8's native modal color picker through `Rhino.UI.Dialogs` with the
Material Setup window as owner and alpha disabled. The picker starts at the effective color already
displayed by the swatch. A confirmed result is converted to an opaque WPF color, clears the selected
suggested palette swatch, and synchronizes the preview, normalized uppercase `#RRGGBB` field, and
effective material color through the existing shared state path. Cancelling returns without changing
the preview, hex value, effective color, or palette selection.

The Rhino-specific call is isolated behind `IMaterialColorPicker`. Material Setup uses the real
adapter in production, while the focused test injects deterministic accepting and cancelling
implementations. This verifies modal outcomes without UI automation or opening a real picker during
the test.

## Differences From Plan

The product behavior and test scope followed the plan. RhinoCommon's owner-aware overload includes
a legacy WinForms owner signature in the overload set, so compiling the selected modern `object`
overload requires the Windows Desktop WinForms reference assembly. A targeted
`Microsoft.WindowsDesktop.App.WindowsForms` framework reference was added without enabling WinForms
implicit namespaces or changing the WPF UI architecture.

Package activation was not attempted because four Rhino processes were running. The supported
installer validated and staged `1.0.34`; it left the active `1.0.32` installation and registration
unchanged.

## Problems Found And Fixed During Construction

1. The first adapter compile failed because RhinoCommon exposes an older overload containing
   `System.Windows.Forms.IWin32Window`. Adding the narrow Windows Desktop framework reference resolved
   the metadata dependency; casting the WPF owner to `object` selects Rhino's supported WPF-capable
   overload.
2. Temporarily enabling the full `UseWindowsForms` SDK mode introduced conflicting global namespaces
   (`Color`, `Brush`, `Point`, and input event types) throughout the WPF project. That approach was
   removed and replaced with the framework reference, restoring a zero-warning build.
3. The focused smoke initially lacked an explicit `System.IO` import required by its WPF SDK global
   using set. The import was added before the first successful run.
4. A parallel solution build raced the PanelCladdingEditor RHP build against its intentional
   test-host DLL build and lost the intermediate reference assembly. Serial `-m:1` Debug and Release
   solution builds both passed and are the reliable full-solution verification for this dual-output
   project structure.
5. Installer `Validate` mode correctly rejected the new `1.0.34` bundle while the ownership manifest
   still describes active `1.0.32`; that mode validates an installed matching version. The supported
   `Install` mode performed bundle validation and staged the new version because Rhino was running.

## Test Record

### Focused picker smoke

Commands:

```powershell
dotnet run --project .\Project_Test\260818_TEST_material-setup-color-picker\MaterialSetupColorPickerSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_material-setup-color-picker\MaterialSetupColorPickerSmoke.csproj -c Release
```

Both final runs exited `0` and reported:

```text
[OK] The custom-color preview is an accessible mouse/keyboard button.
[OK] Confirmed picker colors synchronize preview, hex, and effective color state.
[OK] Cancelling the picker preserves all color and palette state.
[OK] Compact hover, press, and keyboard-focus visual states are present.
```

The smoke asserts exact initial-color routing, one picker invocation per click, confirmed preview and
`#12A4E8` synchronization, effective-state update, suggested-palette deselection, complete
cancellation preservation, 40-DIP layout, accessible name, hand cursor, Tab focusability, rounded
template, and hover/press/focus triggers.

### Visual QA

The focused Debug smoke generated `material-setup-color-picker-accepted.png` at 720 × 740 (48,781
bytes). It was inspected: the confirmed blue swatch remains aligned with the existing hex field and
Add material row, preserves the intended compact shape, and does not disturb the Material Setup
layout.

### Existing UI regressions

The following Debug smokes exited `0` after the change:

- panel-cladding WPF UI;
- visual polish;
- material catalogue; and
- latest UI.

They retain Material Setup construction and rendering, 40-DIP form alignment, rounded styling,
catalogue behavior, and the broader editor action model. The focused picker smoke also passed in
Release.

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

- Version increment: `1.0.33` → `1.0.34` in the active construction state.
- Bundle build: PASS.
- Packaged RHP size: 411,648 bytes.
- Rhino process count before activation: `4`.
- Supported installer result: safely staged; active installation not changed.
- Bundle/staged RHP SHA-256:
  `CFB1DBC2F8BD7F8A9C26F980CE532A9DE440010029DE04D94580DDDE9E10448E`.
- Active installed version remains `1.0.32` until every Rhino process closes.

## Acceptance Criteria Alignment

- Preview is directly clickable and launches Rhino's native selector: implemented through the
  compiled Rhino adapter.
- Selector receives the currently displayed color: passed focused acceptance and cancellation
  tests.
- Confirmation synchronizes preview, hex, and effective opaque color: passed.
- Cancellation makes no state changes: passed.
- Suggested palette and direct hex paths remain functional: passed existing UI/catalogue regressions;
  picker confirmation deliberately clears only the stale palette selection.
- Keyboard access and hover/focus/press feedback: passed template and control-contract assertions.
- Focused Debug/Release, relevant regressions, builds, identity, package, and staging checks: passed.

## Rollback Verification

The change is isolated to Material Setup UI interaction and a Rhino dialog adapter. It does not
mutate Rhino geometry, panel user text, material workbooks, or catalogue data by itself. Reverting
the button template, click handler, adapter, and framework reference restores the former passive
preview. No data migration is required. Active installed `1.0.32` remains untouched.

## Current Remaining Items

- Close every Rhino window.
- Run the staged `1.0.34` installer:

```powershell
powershell -ExecutionPolicy Bypass -File "C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.34-20260819004509769\Installer\Install-PanelCladdingEditor.ps1" -Mode Install -BundleRoot "C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.34-20260819004509769"
```

- Restart Rhino, open Material Setup, and click the custom-color swatch for an interactive native
  picker check. No Rhino UI automation or production document mutation was performed.

## Conclusion

The Material Setup custom-color preview is now a discoverable, accessible button backed by Rhino's
native color selector. Confirmed colors remain consistent across preview, hex, and material state;
cancel is side-effect free. The change is regression-tested, visually verified, packaged as
`1.0.34`, identity-validated, and safely staged for activation after Rhino closes.
