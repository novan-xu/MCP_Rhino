# Panel Cladding Extrusion Fidelity Execution

## Corresponding Plan

- Plan: `Project_Plan/260813_PLAN_panel-cladding-extrusion-fidelity.md`
- Execution date: 2026-08-13

## Related Artifacts

- Focused tests: `Project_Test/260813_TEST_panel-cladding-extrusion-fidelity/`
- Focused render:
  `Project_Test/260813_TEST_panel-cladding-extrusion-fidelity/extrusion-fidelity-1120x800.png`
- Desktop regression render:
  `Project_Test/260812_TEST_panel-cladding-wpf-ui/panel-cladding-wpf-ui-1440x900.png`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.30/`
- Staged bundle:
  `%LOCALAPPDATA%/PanelCladdingEditor/staged/1.0.30-20260813155758097/`
- Active installed version while Rhino remains open:
  `%LOCALAPPDATA%/PanelCladdingEditor/plugin/1.0.29/`
- Commit/PR: none requested.

## Execution Result / Actual Scope

- Removed unit suffixes from every Current Panel divider-offset display/edit value. Values remain
  formatted at five decimals; unit metadata is retained only so pasted legacy values containing the
  document unit can still be parsed.
- Reallocated Current Panel metadata space by reducing the wall/units column from 82 to 68 DIPs,
  reducing the offset code gutter from 26 to 22 DIPs, and tightening margins. Every offset editor now
  reserves at least 64 DIPs, enough for `999.99999` in the configured 10-DIP Consolas typography.
- Centered every extrusion name badge on its segment midpoint in both axes. The badge center now
  overlaps the curve, including horizontal, vertical, and frame curves.
- Corrected the underlying cell geometry. Cell boundaries now derive from full panel/model positions;
  outer cells inset by the 2-DIP frame and internal cells inset by half of the 1.5-DIP divider on each
  side. Cladding dividers and extrusion segment centerlines therefore use the same coordinates.
- Removed redundant unselected extrusion repainting. The dark cladding background is now the single
  base curve geometry in extrusion mode, eliminating anti-aliased widening while preserving exact
  2/1.5-DIP frame/divider pixel bands.
- Replaced the thin yellow selection overwrite with two 2-DIP bright-yellow halo bands offset from the
  original dark curve by a 0.75-DIP gap. The dark base curve is never repainted or covered, so its
  exact normal line weight remains visible. Selected curve-name badges use the same yellow fill with a
  dark border and black text.
- Added a private segment-label rectangle map so deterministic tests can compare every badge center to
  its actual model-derived curve midpoint.
- Bumped and packaged PanelCladdingEditor `1.0.30`. The supported installer staged the validated bundle
  because three Rhino processes were running; it did not modify the loaded `1.0.29` installation.

## Differences From Plan

- The first planned halo implementation painted a broad yellow line and restored a dark core. Pixel
  testing showed that any second 1.5-DIP centerline paint widens WPF's fractional anti-aliased band.
  The final implementation instead draws only adjacent yellow bands and leaves the original dark
  divider untouched.
- Installation validation could not run for `1.0.30` because Rhino was open. The bundle and staged RHP
  passed manifest/assembly identity and SHA-256 validation; activation is intentionally deferred until
  every Rhino process is closed.

## Issues Found And Fixed During Construction

- Equal numeric pen widths did not initially produce equal pixels because the extrusion renderer
  repainted a 1.5-DIP stroke over the existing divider, expanding the anti-aliased dark band from one
  sampled pixel to four. Removing the redundant paint made cladding and extrusion bands identical.
- The cladding cell grid previously mapped dividers inside a frame-shrunken area, while extrusion
  curves used full panel coordinates. This slight centerline mismatch caused the selected yellow line
  to cover the apparent dark core. Full-panel cell positioning aligned both representations exactly.
- A test helper temporarily rearranged the offset ItemsControl in isolation, making its rows overlap
  the heading only in the test render. Removing that extra child arrange preserved the real window
  layout; the focused and desktop renders now show correctly separated, complete offset rows.

## Test Record

- Focused extrusion-fidelity smoke Debug and Release — PASS:
  - every offset display equals its five-decimal numeric value and contains no unit suffix;
  - every offset editor is at least 64 DIPs and its usable width exceeds rendered `999.99999` width;
  - every extrusion label center matches its segment's model-derived midpoint within 0.01 DIP;
  - cladding/extrusion frame bands compare 2/2 pixels and divider bands compare 1/1 pixel in the fixture;
  - selected segment retains a one-to-four-pixel dark core and at least two visible yellow halo pixels;
  - deterministic 1120x800 render generated and visually inspected.
- All 15 panel-cladding Release smoke projects — PASS. This covers standalone editor, spawn, match,
  clear, surface sync, region/offset reconstruction, WPF UI, latest UI, refinement, catalogue, direct
  interactions, visual polish, extrusion visuals, and extrusion fidelity.
- Rhino-native Brep probes in the region/offset suites — expected SKIP because they require a native
  Rhino test host; the UI/package validation itself completed while Rhino processes were open.
- Desktop 1440x900 and focused 1120x800 WPF renders — visually inspected and PASS.
- `dotnet build MCP_Rhino.sln -c Debug --nologo -m:1 /nodeReuse:false` — exit 0, zero warnings/errors.
- `dotnet build MCP_Rhino.sln -c Release --nologo -m:1 /nodeReuse:false` — exit 0, zero warnings/errors.
- `git diff --check` — exit 0.
- Assembly identity — PASS. PanelCladdingEditor GUID
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35` matches the package manifest and remains distinct from
  MCP_Rhino.
- Package build — `PanelCladdingEditor-1.0.30`, Release, zero warnings/errors.
- Packaged and staged RHP identity checks — PASS.
- Staged RHP SHA-256:
  `A9C53B1CA577E5D04ADAC6C8DE4DED74E7814F184F53F948C15AE5D907766C81`.

## Acceptance Alignment

All five requested refinements are implemented: unit-free unclipped offsets, midpoint-overlapping curve
names, exact shared base line weights, and a complete yellow-halo/dark-core extrusion selection state.

## Rollback Verification

- Active `1.0.29` remains untouched while Rhino is open.
- Existing `1.0.29` and earlier package artifacts remain available.
- This increment did not mutate any Rhino document or project workbook.

## Current Remaining Items

- Close every Rhino window, then activate the staged build with:

  ```powershell
  powershell -ExecutionPolicy Bypass -File "C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.30-20260813155758097\Installer\Install-PanelCladdingEditor.ps1" -Mode Install -BundleRoot "C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.30-20260813155758097"
  ```

- Restart Rhino after installation to load `1.0.30`.

## Conclusion

The requested extrusion fidelity refinements are implemented, fully regression-tested, visually
verified, packaged, identity-validated, and safely staged for activation after Rhino closes.
