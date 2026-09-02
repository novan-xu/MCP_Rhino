# PCMatchSrf logical-cell matching plan

## Background

The current `PCMatch` command mixes cladding-surface assignment transfer with extrusion topology metadata. Its source validation also requires populated type/signature and cell attributes, even though the source and target cell-code lattices can be resolved from their H/V divider counts when `0A`, `0B`, and similar assignment keys are absent.

The requested command is surface-focused. Numeric divider positions, panel dimensions, and segment/merge topology must not participate in compatibility or transfer.

## Goal

Rename the Rhino command to `PCMatchSrf` and match only cladding logical-cell assignments between panels that generate the same ordered cell codes. Missing source assignment keys are valid and transfer as blank cells.

## Architecture ownership

- `PanelCladdingMatchPlanningService` owns source cell-graph validation, cell-code compatibility, and the exact cell-only transfer plan.
- Existing match snapshots continue to carry geometry and user text; geometry is used only to parse each panel's valid H/V grid, not as an equality constraint.
- `LivePanelCladdingMatchService` continues to validate the whole batch and commit it in one Rhino Undo record.
- The Rhino UI command owns only selection, the public `PCMatchSrf` name, and user-facing messages.

## Key design

1. Parse source and target H/V grids independently.
2. Generate the ordered cell codes from each grid and require exact equality. Equal codes imply equal row/column counts.
3. Ignore panel width/height equality and all numeric H/V offset differences.
4. Ignore segment and merge masks for both compatibility and transfer. Preserve every target topology key unchanged.
5. Remove the source type/signature requirement. The explicitly selected source may contain assigned cells, parent references, blank values, or no cell keys at all.
6. Validate material/parent references with blank cells allowed. Invalid references and cycles remain fail-closed.
7. Transfer every generated source cell code. Assigned material and parent tokens remain exact normalized values; absent or blank source cells use the established Rhino-retained blank representation.
8. Delete/replace only target cladding-cell keys. Preserve target offsets, masks, type/signature metadata, identity metadata, and unrelated user text.
9. Rename the public command from `PCMatch` to `PCMatchSrf` without retaining a visible alias.
10. Validate every selected target before mutation; one incompatible cell-code lattice rejects the entire batch.

## Involved files

- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingMatchPlanningService.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingMatchCommand.cs`
- `Packaging/PanelCladdingEditor/package-manifest.json`
- `Project_Test/260819_TEST_pcmatch-srf-logical-cells/`
- existing PCMatch regression fixtures and command-name assertions
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`
- `Project_Exet/260819_EXET_pcmatch-srf-logical-cells.md`

## Usage

Run `PCMatchSrf`, select target panels, and then select the source panel. Panels may have different sizes, H/V distances, and extrusion masks, but they must generate the same ordered cladding cell codes. Only the cell material/parent assignment graph is transferred.

## Acceptance criteria

- The public Rhino command is `PCMatchSrf`, and `PCMatch` is no longer registered.
- Panels with different dimensions, offset values, and masks match when their generated cell codes are identical.
- A different row or column count rejects the complete batch with `PANEL_CLADDING_MATCH_GEOMETRY_MISMATCH` before mutation.
- A source with no cell keys and no cladding type/signature still produces a valid plan containing retained blank writes for every generated cell code.
- Assigned materials and parent-cell values continue to transfer exactly.
- Target H/V offsets, segment/merge masks, type/signature, identity metadata, and unrelated attributes are neither deleted nor written.
- Parent cycles or references to nonexistent cell codes remain rejected.
- Focused Debug/Release, PCMatch parent-fidelity, blank-cell, topology, command, and solution regressions pass.
- Packaged plug-in identity and installation validation pass.

## Risks and rollback

- Risk: target type/signature metadata can describe a different prior cladding configuration because this command deliberately does not update it. Mitigation: keep the requested cell-only scope explicit; PCEditor Save or a later dedicated type-refresh workflow can recalculate derived metadata.
- Risk: source and target extrusion topology can group physical cells differently. Mitigation: PCMatchSrf intentionally transfers values strictly by generated cell code and does not claim extrusion equivalence.
- Risk: blank source cells could disappear at the Rhino boundary. Mitigation: reuse the tested one-space storage representation and existing whitespace normalization.
- Rollback: restore the prior public command name and configuration-key transfer set, then reinstall the previous package.

## Future extensions

- Add a separate `PCMatchCrv` command if extrusion segment/merge topology matching is needed.
- Add a derived type/signature refresh operation scoped independently from cell assignment transfer.
