# Panel cladding logical-cell cleanup plan

## Background

Deleting an intermediate extrusion changes the panel's logical cell topology, but the structural Save path currently writes assignments for every physical H/V grid cell. A cell hidden by a deleted segment therefore remains as stale Rhino user text even though the editor displays the merged logical cell.

The live panel `PID_BKT_N1_01_11` demonstrates the defect: the segment mask merges `0C + 0D` and `1C + 1D`, while `CW_4.00_CLADDING_0D` and `CW_4.01_CLADDING_1D` remain on the panel.

## Goal

Make structural Save persist one cladding key/value per logical cell. The lowest row, then lowest column, is the representative cell. Non-representative physical-cell keys must be included in the commit delete set.

## Architecture ownership

- Domain models continue to own physical cells and panel-level topology state.
- Application services derive logical-cell representatives and prepare the Rhino attribute transaction.
- Infrastructure remains responsible only for committing the prepared write/delete set.
- UI topology display behavior remains unchanged.

## Key design

1. Build connected logical-cell groups from `PanelCladdingTopologyState.MissingSegments` using the physical grid coordinates.
2. Select the deterministic representative by row, then column, matching the editor display.
3. Preserve the existing physical-grid type signature and panel-level segment/merge masks.
4. Collapse normalized cell assignments to representative keys immediately before the commit write/delete set is assembled.
5. Remap parent-cell labels that target a hidden group member to that group's representative label.
6. Treat every source cladding-cell key absent from the collapsed write set as obsolete.

## Involved files

- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingLogicalCellService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladdingSaveService.cs`
- `Project_Test/260819_TEST_panel-cladding-logical-cell-cleanup/`
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`
- `Project_Exet/260819_EXET_panel-cladding-logical-cell-cleanup.md`

## Usage

In PCEditor extrusion view, delete one or more intermediate segments and click Save. The panel retains H/V offsets and panel-level masks, while hidden physical-cell assignment keys are removed automatically.

## Acceptance criteria

- A 2x4 physical grid with the C/D horizontal segments deleted in both columns writes only `0A-0C` and `1A-1C` cell keys.
- Existing `0D` and `1D` keys are present in `UserTextDeletes` and absent from `UserTextWrites`.
- `0C` and `1C` keep the correct material/parent-cell assignments.
- Parent references to a hidden cell are remapped to its logical representative.
- Existing topology persistence, PCMatch parent fidelity, Debug, and Release validation pass.
- The packaged RHP retains the required assembly/plugin GUID identity.

## Risks and rollback

- Risk: collapsing before signature generation could change established type identities. Mitigation: retain physical normalized cells for the v4 signature and collapse only the persisted assignment map.
- Risk: an invalid topology coordinate could collapse the wrong cells. Mitigation: rely on existing topology validation and ignore no valid in-grid adjacency.
- Rollback: revert the logical projection call and helper; the previous physical-cell persistence behavior is isolated to the Save service.

## Future extensions

- Reuse the application logical-cell projection in sync/import workflows.
- Move UI logical-cell grouping onto the same application/domain primitive to remove duplicate union logic.
