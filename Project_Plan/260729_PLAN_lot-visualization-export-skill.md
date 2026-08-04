# Lot Visualization Export Skill Plan

## Background

The live `20260729_Lot visualization.3dm` workflow established a repeatable sequence: resolve an effective lot number from object user text, group and color visible geometry by lot, leave ineffective or missing lots white, create four isometric named views, and export four PNGs at one Rhino Print model scale. The first export approach also exposed an important state-safety defect: export background staging must never change Rhino application appearance settings.

## Goal

Add a reusable MCP_Rhino skill that previews and executes the complete lot-visualization export workflow. The execution must use live Rhino state, persist the requested lot groups/colors and four isometric named views, export PNGs with one common print scale, use a temporary solid-white document render background, and restore the exact prior document render background after success or failure.

## Architecture Ownership

- `Skills/Drawing`: fixed workflow composition.
- `Application/Services`: lot-plan validation and live use-case orchestration.
- `Application/Interfaces`: live lot visualization adapter boundary.
- `Infrastructure/Rhino/Live`: RhinoCommon object grouping/coloring and temporary render-background implementation.
- `Tools/Drawing`: thin preview/apply MCP wrappers.
- `Contracts` and `Domain`: requests, responses, and normalized lot plans.
- `Project_Test`: unique CLI/Rhino smoke registration and validation evidence.

## Key Design

1. Provide a read-only preview before the exporting mutation. Preview reports resolved lot keys, lot/object counts, unassigned count, assigned colors, target layers, four view names, and planned PNG paths.
2. Resolve only visible, non-deleted geometry. Accept explicit lot-number keys; when omitted, discover conservative lot-like object user-text keys and apply a documented priority order.
3. Treat blank and placeholder values such as `TBD`, `TBC`, `N/A`, `NA`, `NONE`, and `UNASSIGNED` as ineffective. Color those objects white and remove only prior skill-owned `MCP_Lot_` group membership.
4. Create or reuse one deterministic `MCP_Lot_<value>` Rhino group per effective lot. Assign a high-contrast deterministic palette by sorted effective lot value.
5. Add an `Isometric4` drawing-view preset containing `MCP_Iso_NE`, `MCP_Iso_NW`, `MCP_Iso_SE`, and `MCP_Iso_SW`, fitted to the visible target geometry. The named views establish angle and projection; print-scale export controls framing and scale.
6. Extend the print-scale exporter with an optional solid background color. When requested, capture document `RenderSettings.BackgroundStyle`, top color, and bottom color; set `SolidColor` plus the requested color; and restore all captured values in a guaranteed cleanup path. Never read or write `AppearanceSettings.ViewportBackgroundColor`.
7. Update the existing drawing-export background operator to follow the same document-only rule and restore contract.
8. Default output to the `.3dm` folder with 2400 x 2400 PNGs, 300 DPI, 6 mm margins, `FitAll`, and a shared scale multiplier of 1.08. Allow bounded overrides.

## Files Involved

- New contracts, domain models, service, adapter interface/implementation, skill, and drawing tool.
- `DrawingViewPreset`, `LiveDrawingViewManager`.
- Print-scale request/spec/response mapping and `LiveRhinoPrintScaleImageExporter`.
- Drawing background snapshot/operator.
- Dependency injection and skill registration.
- MCP tool-safety expectation inventory.
- `DeveloperCommandHandler` smoke hook and a dedicated Rhino smoke command.
- Matching PLAN, TEST, and EXET artifacts.

## Usage

1. Call `PreviewLotVisualizationExport` with the active saved `.3dm` path and optional lot-number key preferences.
2. Review lot resolution, object counts, planned colors, view names, and output paths.
3. Call `ExportLotVisualization` with the confirmed parameters.
4. Save the Rhino document when the persistent grouping/color/view changes are accepted.

## Acceptance Criteria

- Preview is read-only and returns a bounded deterministic plan.
- Apply groups every effective lot into one skill-owned group and assigns one distinct lot color.
- Geometry without an effective lot is white and is not left in a skill-owned lot group.
- Exactly four isometric named views are created or updated and fitted to visible target geometry.
- All four PNGs report the exact same positive print model-scale denominator.
- White export background is produced by temporary document render settings only.
- Rhino application appearance background is never changed.
- Prior document render background style/top/bottom values are restored after both success and failure.
- Debug and Release builds, capability smoke, and MCP safety smoke pass.

## Risks And Rollback

- Auto-discovered user-text keys can be ambiguous. Preview exposes the resolved key order; callers can provide explicit keys before applying.
- Group and object-attribute mutation is persistent by design and enters one Rhino Undo record per apply service call.
- Multi-file output cannot be perfectly transactional across arbitrary filesystem failures; the existing exporter renders to temporary files before final moves and cleans temporary files.
- Background restoration failure aborts the export result and is reported explicitly; the implementation never falls back to application appearance settings.
- Reverting the capability consists of removing the new skill/tool/service/adapter/contracts, removing the `Isometric4` preset, and reverting the document-only background additions. Existing print-scale export remains otherwise compatible.

## Future Extensions

- User-supplied color palettes or persisted lot-to-color maps.
- Scope by confirmed object ids, layers, or current selection.
- Legend generation and CSV lot/color schedules.
- A dedicated print-layout output mode sharing the same background transaction.
