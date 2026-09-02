# Panel Cladding Curve Template And Catalogue UI Execution

## Corresponding Plan

- Plan: `Project_Plan/260820_PLAN_panel-cladding-curve-template-ui.md`
- Execution date: 2026-08-20

## Associated Artifacts

- Focused tests: `Project_Test/260820_TEST_panel-cladding-curve-template-ui/`
- Footer QA render:
  `Project_Test/260820_TEST_panel-cladding-curve-template-ui/panel-cladding-footer-980x700.png`
  (65,654 bytes)
- Material catalogue QA render:
  `Project_Test/260820_TEST_panel-cladding-curve-template-ui/material-catalogue-responsive-720x740.png`
  (44,208 bytes)
- Commit / PR: none; work remains in the user's existing dirty working tree.

## Execution Result / Actual Delivered Scope

### Editor footer

- Added a compact footer-specific button profile for the two secondary save actions.
- Applied equivalent compact padding and font sizing to the primary `Save Both` action without
  changing its green primary-button behavior.
- Kept all three equal-width columns and verified the complete labels at a 980 x 700 constrained
  editor render.

### Material Setup catalogue

- Replaced the fixed `UniformGrid Columns="3"` with a wrapping panel.
- Added a shared `CatalogueItemWidth` calculated with the actual 11-DIP Consolas code typeface.
  The width is the longest code's measured width plus the swatch/gap/padding/border allowance, with
  a 76-DIP minimum.
- Recalculates after constructor population, workbook catalogue load, material replacement, and
  material addition.
- Increased the catalogue viewport cap from 98 to 224 DIPs and retained vertical scrolling for
  larger catalogues.
- The focused ten-material render fitted four columns while showing the full
  `THERMALLY-BROKEN` code.

### `PCCrvTemplate`

- Added a concrete Rhino command with GUID
  `6D026D22-A99A-4EF4-83C0-6140427248D2` and exact English name `PCCrvTemplate`.
- The command requires a saved active document, prompts first for one or more panel Breps, then
  offers `HPriority` and `VPriority`.
- Added a pure application planner that:
  - clears prior merge runs from both axes;
  - creates maximal mergeable runs only on the selected priority axis;
  - splits runs at missing segments and hidden/visible transitions;
  - preserves missing and hidden topology evidence; and
  - delegates payload construction to the canonical `PanelCladdingKeyService.EncodeTopology`
    codec.
- Added a live batch service that validates every selected layout before mutation, replaces only
  the merge-mask topology key, invalidates current/legacy stored signatures after an actual mask
  change, applies all changed panels under one Rhino Undo record, redraws once, and restores prior
  attributes if a later write fails.
- Added the command to the current PC command inventory and package documentation.

## Variance From Plan

- The plan stated that the live mutation would write only the merge-mask user string. During the
  final domain-consistency review, the existing type-signature payload was confirmed to hash the
  merge mask. The delivered mutation therefore also deletes `Signature` and
  `CW_4.00_CLADDING_SIGNATURE`, but only when the mask changes. This follows the existing
  `PCCreate` / `PCMatchCrv` invalidation contract and prevents stale identity metadata. Segment and
  hide masks remain untouched.
- No package build or live installation was performed. The request did not authorize changing the
  user's installed plug-in or registry state, and direct Debug/Release RHP builds fully validated
  the compiled command surface.

## Problems Found And Fixed During Construction

- The legacy material-catalogue smoke initially compared the internal wrapping panel's arranged
  width directly with a ListBox that intentionally has a negative right margin. That assertion was
  layout-internal and false even though the tiles rendered correctly. It was narrowed to verify the
  shared item width and that the wrapping viewport is wider than one tile.
- A topology identity review found that retaining a pre-template `Signature` would leave metadata
  inconsistent with the new merge mask. Planned signature invalidation was added before closure.
- The repository already contained extensive modified and untracked PanelCladdingEditor work. All
  changes were applied in place without resetting, deleting, or overwriting unrelated work.

## Test Record

All passing commands below completed with exit code `0`.

### Focused curve-template and UI smoke

```powershell
dotnet run --project .\Project_Test\260820_TEST_panel-cladding-curve-template-ui\PanelCladdingCurveTemplateUiSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260820_TEST_panel-cladding-curve-template-ui\PanelCladdingCurveTemplateUiSmoke.csproj -c Release
```

Key assertions in both configurations:

- H fixture: 3 merge runs (`H0:0-1`, `H1:0-1`, `H1:2-3`).
- V fixture: 3 full-height merge runs, one per vertical track.
- Missing and hidden segment lists round-trip unchanged.
- Reapplying H priority produces the identical merge payload.
- Empty selection fails during planning.
- Command name/GUID uniqueness, selection-before-priority order, one Undo entry, rollback source
  contract, merge-only topology write, and signature invalidation pass.
- Ten catalogue entries fit at least four columns at 720 DIPs and the longest code is untrimmed.
- All three save labels fit at 980 DIPs.

### Existing UI and command regressions

```powershell
dotnet run --project .\Project_Test\260818_TEST_material-catalogue-editing\MaterialCatalogueEditingSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_material-catalogue-editing\MaterialCatalogueEditingSmoke.csproj -c Release
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-pc-commands\PanelCladdingPcCommandsSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-pc-commands\PanelCladdingPcCommandsSmoke.csproj -c Release
dotnet run --project .\Project_Test\260812_TEST_panel-cladding-wpf-ui\PanelCladdingWpfUiSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260812_TEST_panel-cladding-wpf-ui\PanelCladdingWpfUiSmoke.csproj -c Release
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-curve-topology\PanelCladdingCurveTopologySmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-curve-topology\PanelCladdingCurveTopologySmoke.csproj -c Release
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-scoped-save\PanelCladdingScopedSaveSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-scoped-save\PanelCladdingScopedSaveSmoke.csproj -c Release
```

Results:

- Responsive catalogue editing/addition/color/category regressions pass.
- The exact command inventory now contains ten unique PC-prefixed commands, including
  `PCCrvTemplate`.
- General editor WPF layout/renders pass in default and compact sizes.
- Curve topology inference/mask transfer and scoped save behavior pass.

### Debug / Release builds

```powershell
dotnet build .\MCP_Rhino.sln -c Debug --nologo
dotnet build .\MCP_Rhino.sln -c Release --nologo
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Debug --nologo
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Release --nologo
```

Each build completed with `0 Warning(s)` and `0 Error(s)`. Direct project builds produced:

- `src/PanelCladdingEditor/bin/Debug/net8.0-windows/PanelCladdingEditor.rhp`
- `src/PanelCladdingEditor/bin/Release/net8.0-windows/PanelCladdingEditor.rhp`

### Plug-in assembly identity

```powershell
powershell -ExecutionPolicy Bypass -File .\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Debug -SkipBuild
powershell -ExecutionPolicy Bypass -File .\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Release -SkipBuild
```

Both configurations reported two declared, distinct, non-empty IDs. PanelCladdingEditor remained
`7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, matching its assembly, plug-in class, and package manifest.

### Diff hygiene

```powershell
git diff --check
```

Exit code `0`; only existing Windows line-ending conversion warnings were printed.

### Inherited non-blocking test mismatch

The following current-tree regression was also run in Debug and Release and exited `1`:

```powershell
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-topology-persistence\PanelCladdingTopologyPersistenceSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-topology-persistence\PanelCladdingTopologyPersistenceSmoke.csproj -c Release
```

Both runs failed at the pre-existing assertion `Structural Save did not produce a v4 identity.`
The current `PanelCladdingSaveService` returns an empty `PanelCladdingSaveResult.StoredSignature`
after the present save workflow, while this older smoke still requires that response field to start
with `v4:sha256:`. The new command does not call the save service, and this change does not modify
signature generation or that response contract. The focused mask round-trip, current curve
topology, and scoped-save regressions all pass. The stale smoke was not rewritten as part of this
request.

## Acceptance Alignment

- Complete save labels: passed measured-width assertions and visual inspection at 980 x 700.
- Responsive catalogue: passed content-width, wrapping, vertical viewport, and visual inspection
  at 720 x 740.
- Correct command surface: exact name, new unique GUID, and ten-command inventory passed.
- H/V behavior: maximal priority-axis runs and non-priority clearing passed canonical round-trip
  tests.
- Topology preservation: missing/hide evidence and idempotence passed.
- Mutation safety: plan-before-mutation, one batch Undo record, rollback, and no-op reapplication
  contracts passed compiled/source smoke plus Debug/Release builds.
- Plug-in identity: Debug and Release RHP metadata passed direct inspection.

## Rollback Verification

- UI rollback is limited to the footer-specific style and the Material Setup ListBox wrapping/width
  calculation changes.
- Capability rollback removes the command, interface, domain records, planner, live adapter, and
  focused test folder, and removes the command from documentation/inventory.
- No Rhino document, workbook, package, registry key, or installed file was mutated during this
  execution. If the command is later used, its complete selected-panel batch is recoverable with one
  Rhino Undo.

## Current Remaining Items

- Live interactive Rhino execution of `PCCrvTemplate` was not performed because the user did not
  authorize Windows UI automation and the repository explicitly prohibits it without that request.
  The command compiled into both RHP configurations and its selection/options/mutation contracts are
  covered by focused tests.
- The inherited topology-persistence response assertion remains stale as documented above.
- No package build, installation, commit, push, or PR was requested or performed.

## Conclusion

The two requested UI corrections and `PCCrvTemplate` are implemented and closed under the required
PLAN → TEST → EXET chain. Current focused and relevant regressions, Debug/Release solution and RHP
builds, visual QA, diff hygiene, and plug-in identity validation pass.
