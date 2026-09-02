# Panel Cladding Curve Template And Catalogue UI

## Background

The current Panel Cladding Editor footer gives three save actions equal shares of a narrow side
panel while retaining the standard button padding, so `Save Extrusions` and `Save Cladding` are
visibly clipped. The Material Setup catalogue is also constrained to three equal-width columns and
a 98-DIP viewport, which wastes space for short material codes and exposes too few entries.

Panel extrusion topology already persists canonical segment, merge, and hide masks. Repetitive
panel layouts commonly need one of two standard curve templates: all valid horizontal mullion runs
continuous across the panel, or all valid vertical mullion runs continuous through the panel.

## Goal

- Keep the complete `Save Extrusions`, `Save Cladding`, and `Save Both` labels visible in the editor
  footer.
- Make the project material catalogue responsive: tile width follows the longest visible material
  code and the catalogue fits as many columns as the available width permits, with a materially
  taller browsing viewport.
- Add the Rhino command `PCCrvTemplate`. It selects one or more panel Breps, prompts for
  `HPriority` or `VPriority`, computes the corresponding canonical merge mask from each panel's
  stored setup, and applies the batch in one Undo record.

## Architecture Ownership

- `UI/PanelCladdingEditorWindow.xaml`: footer button presentation only.
- `UI/MaterialSetupDialog.xaml(.cs)`: responsive WPF catalogue presentation and code-width
  measurement.
- `UI/PanelCladdingCurveTemplateCommand.cs`: thin Rhino selection/option adapter and command-line
  result reporting.
- `Application/Services/PanelCladding/PanelCladdingCurveTemplatePlanningService.cs`: pure merge-run
  planning and canonical merge-mask encoding.
- `Application/Interfaces/IPanelCladdingServices.cs`: live batch mutation contract.
- `Domain/PanelCladdingModels.cs`: curve-priority request/snapshot/plan/result records.
- `Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingCurveTemplateService.cs`: live panel
  reads, one-record Undo mutation, rollback, and redraw.
- `Project_Test/260820_TEST_panel-cladding-curve-template-ui/`: focused planning, command inventory,
  and WPF layout regressions.

This is a standalone PanelCladdingEditor plug-in capability. It does not change the MCP server tool
surface, Router protocol, or routed document-session lifecycle.

## Key Design

1. Give footer save actions a compact footer-specific font/padding profile while retaining the
   existing equal-width layout and action hierarchy.
2. Replace the Material Setup catalogue's fixed `UniformGrid Columns="3"` with a wrapping panel.
   Each item receives one shared width calculated from the longest material code using the actual
   catalogue typeface, plus swatch, gap, padding, and border allowance. Recalculate after workbook
   load and material add/edit so all codes remain visible.
3. Increase the catalogue viewport height while retaining vertical scrolling for large catalogues.
4. Model the template choice as `Horizontal` or `Vertical`. For every intermediate track on the
   priority axis, merge each maximal sequence of at least two present segments. Deleted segments
   split runs. A hidden/visible transition also splits runs because the canonical topology contract
   forbids a partially hidden merge group.
5. Replace prior merge runs on both axes with the computed priority-axis runs. Preserve segment and
   hide masks unchanged, then use `PanelCladdingKeyService.EncodeTopology` as the sole merge-mask
   encoder.
6. Read and validate every selected panel before mutation. Commit only the merge-mask user string,
   case-insensitively replacing an existing canonical key. Apply all actual changes under one Rhino
   Undo record and restore already modified attributes if any later write fails.
7. Register `PCCrvTemplate` through a concrete Rhino `Command` subclass with a new unique GUID.
   Rhino command discovery remains assembly-based; no second command registry is introduced.

## Files Involved

- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml`
- `src/PanelCladdingEditor/UI/MaterialSetupDialog.xaml`
- `src/PanelCladdingEditor/UI/MaterialSetupDialog.xaml.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingCurveTemplateCommand.cs` (new)
- `src/PanelCladdingEditor/Application/Interfaces/IPanelCladdingServices.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingCurveTemplatePlanningService.cs` (new)
- `src/PanelCladdingEditor/Domain/PanelCladdingModels.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingCurveTemplateService.cs` (new)
- `Packaging/PanelCladdingEditor/README.md`
- `Project_Test/260818_TEST_material-catalogue-editing/Program.cs`
- `Project_Test/260818_TEST_panel-cladding-pc-commands/Program.cs`
- `Project_Test/260820_TEST_panel-cladding-curve-template-ui/` (new)
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`
- `Project_Exet/260820_EXET_panel-cladding-curve-template-ui.md` (after verification)

## Usage

1. Save the active Rhino document.
2. Run `_PCCrvTemplate`.
3. Select one or more panel Breps and finish the selection.
4. Choose `HPriority` for continuous horizontal mullions or `VPriority` for continuous vertical
   mullions.
5. Use Rhino Undo once to revert the complete selected-panel batch if needed.

## Acceptance Criteria

- All three footer action labels render in full at the editor's default and minimum supported
  widths.
- Material catalogue tiles expose the full longest code, wrap responsively, and are not fixed to
  three columns; the catalogue displays substantially more vertical content than the old 98-DIP
  cap.
- `PCCrvTemplate` is registered with a unique non-empty command GUID and prompts in the required
  panel-selection-then-priority order.
- H priority creates only valid horizontal maximal merge runs; V priority creates only valid
  vertical maximal merge runs.
- Missing and hidden segment evidence is preserved, generated payloads round-trip through the
  canonical topology codec, and reapplying the same priority is idempotent.
- Invalid selected panels cause no mutation. A write failure rolls back prior writes. A successful
  multi-panel operation produces one Undo record.
- Focused Debug and Release smokes, relevant existing UI/command regressions, direct plug-in Debug
  and Release builds, assembly GUID verification, and `git diff --check` pass.

## Risks And Rollback

- A very long code intentionally reduces the responsive column count so it remains legible.
- Existing unusual topology can contain deleted or mixed hidden segments. Splitting at those
  boundaries keeps the persisted masks valid, but such a track may contain multiple dominant runs
  rather than one full-panel curve.
- The command deliberately replaces earlier manual merge groups. Segment deletion and hide state
  are preserved.
- Rollback restores the prior XAML catalogue/footer constraints and removes the command, planning
  service, live service, domain records, interface, and focused test folder. Rhino document changes
  made by the command are independently recoverable with one Undo.

## Future Extensions

- Expose the same H/V template choice as an editor toolbar action.
- Add template presets that combine priority merges with explicit hide patterns.
- Add a per-project default priority if a stable workbook-level requirement emerges.
