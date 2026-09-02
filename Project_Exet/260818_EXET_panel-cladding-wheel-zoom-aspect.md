# Panel Cladding Wheel Zoom Aspect Execution

## Corresponding Plan

- Plan: `Project_Plan/260818_PLAN_panel-cladding-wheel-zoom-aspect.md`
- Execution date: 2026-08-18

## Related Artifacts

- Production fix: `src/PanelCladdingEditor/UI/PanelCladdingGridCanvas.cs`
- Focused tests: `Project_Test/260818_TEST_panel-cladding-wheel-zoom-aspect/`
- Visual evidence:
  - `panel-E1_05_48-zoom-80.png`
  - `panel-E1_05_48-zoom-150.png`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.33/`
- Staged bundle:
  `%LOCALAPPDATA%/PanelCladdingEditor/staged/1.0.33-20260819000758685/`
- Active installed version during execution: `1.0.32`
- Commit / PR: none created in this execution.

## Execution Result / Actual Delivered Scope

The wheel event was already reaching the preview, changing `Zoom` by 5%, and synchronizing the
toolbar. The bug was in the downstream panel-size calculation. `CalculatePanelSize` first derived an
aspect-correct fitted width, but then independently applied an 80-DIP minimum width and a 60-DIP
minimum height after zoom.

For panel `E1_05_48` (`18.75 × 199.5`, ratio approximately `0.094`) under the constrained focused
canvas, the former formula produced:

- 80%: width `80`, height `304`;
- 150%: width `80`, height `570`.

The wheel therefore changed height while width remained frozen, changing the displayed aspect ratio
and creating the reported stretch.

The corrected calculation validates the model ratio, computes the same available-canvas fit, and
applies one finite positive scalar to both axes. Height is now always derived as
`scaledWidth / modelRatio`; there is no independent width or height floor. At the same focused
canvas, the corrected bounds are approximately:

- 80%: width `28.57`, height `304`;
- 150%: width `53.57`, height `570`.

Both dimensions scale by exactly `1.875`, matching `150 / 80`, and the preview ratio remains the
model ratio at every supported zoom. The 80–150% range, 5% wheel step, toolbar synchronization,
centered baseline, pan envelope, hit geometry, dimensions, grid, material drawing, and extrusion
view remain on their existing paths.

## Differences From Plan

The product fix and tests followed the plan. Installation was not attempted while loaded: two Rhino
processes were running, so the supported installer staged `1.0.33` and left the active `1.0.32`
installation and registry entry unchanged.

## Problems Found And Fixed During Construction

1. The initial focused test omitted `System.IO` and used a nonexistent fixture-only `PanelId`
   property. The test was aligned with the actual domain contract before its first successful run.
2. Reflection by method name alone encountered both the canvas's private wheel handler and the
   inherited WPF method. The smoke now restricts lookup to methods declared directly on
   `PanelCladdingGridCanvas`, matching the production handler under test.
3. The MCP server project intentionally compiles repository test sources and explicitly excludes
   standalone panel WPF smokes. The new focused test folder was added to that established exclusion
   list so server and identity builds remain isolated.

## Test Record

### Focused tall-panel smoke

Commands:

```powershell
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-wheel-zoom-aspect\PanelCladdingWheelZoomAspectSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-wheel-zoom-aspect\PanelCladdingWheelZoomAspectSmoke.csproj -c Release
```

Both final runs exited `0` and reported:

```text
[OK] The old independent width/height floor reproduces E1_05_48 stretching.
[OK] Corrected 80-150% bounds preserve the model aspect ratio and scale both axes equally.
[OK] Positive and negative wheel input uniformly zoom by one 5% step and remains handled.
[OK] Cladding hit geometry and extrusion view share the corrected panel bounds.
```

The smoke asserts:

- the old formula freezes width at 80 while height grows;
- corrected 80%, 100%, and 150% ratios equal `18.75 / 199.5`;
- X and Y scale factors both equal `150 / 80` across the full zoom range;
- one positive wheel detent changes 100% to 105% and one negative detent restores 100%;
- both wheel events are handled;
- zero-pan bounds remain centered;
- cell hit rectangles grow on both axes and remain inside panel bounds; and
- cladding/extrusion view switching retains identical corrected bounds.

### Visual QA

The focused Debug smoke generated 2× pixel-density off-screen renders:

- `panel-E1_05_48-zoom-80.png` — 66,440 bytes;
- `panel-E1_05_48-zoom-150.png` — 55,159 bytes.

Both were inspected. The 80% render shows the complete slender panel centered in the canvas. The
150% render grows both width and height uniformly, retains the same proportions, and keeps divider,
dimension, label, and hit geometry aligned. The narrower low-zoom preview is intentional: the old
extra width was the geometric distortion.

### Existing WPF regressions

The following Release smokes exited `0` after the fix:

- canvas alignment;
- direct interactions;
- latest UI;
- extrusion visuals;
- visual polish;
- WPF UI; and
- cell topology.

These retain centered cladding/extrusion bounds, 80–150% and 5% input contracts, symmetric pan,
selection/hit behavior, divider/dimension alignment, material assignment, cell merge/renumbering,
and desktop/compact rendering.

### Builds and plug-in identity

- `MCP_Rhino.sln` Debug: PASS, zero warnings/errors.
- `MCP_Rhino.sln` Release: PASS, zero warnings/errors.
- Direct PanelCladdingEditor Debug RHP build: PASS, zero warnings/errors.
- Direct PanelCladdingEditor Release RHP build: PASS, zero warnings/errors.
- Debug assembly identity: PASS.
- Release assembly identity: PASS.
- Packaged Release assembly identity: PASS.
- PanelCladdingEditor assembly-level plug-in GUID:
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, manifest-matching and distinct.
- `git diff --check`: PASS; existing line-ending notices only.

### Package and staging

- Version increment: `1.0.32` → `1.0.33`.
- Bundle build: PASS.
- Rhino process count before activation: `2`.
- Supported installer result: safely staged; active installation not changed.
- Bundle/staged RHP SHA-256:
  `0EA86375A301ADDF43C11B4E24DEDDF9FD1CD4103B214CBE1C3208DB72C0E00C`.
- Active installed version remains `1.0.32` until every Rhino process closes.

## Acceptance Criteria Alignment

- Wheel changes both preview dimensions for the `E1_05_48` fixture: passed.
- Model aspect ratio is invariant at every tested zoom: passed.
- X/Y scale factors are identical from 80% through 150%: passed.
- Toolbar/wheel state and 5% steps: passed focused and existing direct-interaction tests.
- Cladding/extrusion bounds, hit geometry, dimensions, grid, and panning: passed.
- Focused Debug/Release, relevant regressions, builds, and identity checks: passed.
- Safe package handling: passed; package staged because Rhino is running.

## Rollback Verification

The change affects preview-only WPF size calculation. It does not mutate Rhino geometry, panel user
text, workbook data, selections, or saved layouts. Reverting the one size calculation restores prior
behavior. Version `1.0.32` remains active and untouched until the staged installer is deliberately
run after Rhino closes.

## Current Remaining Items

- Close every Rhino window.
- Run the staged `1.0.33` installer:

```powershell
powershell -ExecutionPolicy Bypass -File "C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.33-20260819000758685\Installer\Install-PanelCladdingEditor.ps1" -Mode Install -BundleRoot "C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.33-20260819000758685"
```

- Restart Rhino and verify wheel zoom on `E1_05_48` interactively. No Rhino UI automation or
  production document mutation was performed.

## Conclusion

The stretching bug was caused by independent post-zoom dimension clamps, not by lost wheel input.
Panel preview sizing now uses one uniform scale, preserving the real panel aspect ratio while both
axes zoom together. The fix is fully regression-tested, visually verified, packaged as `1.0.33`,
identity-validated, and safely staged for activation after Rhino closes.
