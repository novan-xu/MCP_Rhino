# Panel Cladding Topology Persistence Plan

## Background

The panel cladding editor can already add, delete, merge, and explode intermediate extrusion
segments in the extrusion view. Those changes currently exist only in the editor session:
`PanelCladdingEditorWindow` marks them as structural previews, disables Save, and clears the
deleted-segment and merged-run collections when the panel is loaded again.

The approved topology method distinguishes two independent conditions:

- whether every atomic horizontal or vertical extrusion segment exists; and
- whether adjacent existing segments on the same track are joined as one curve.

The persisted Rhino object data should contain one segment-mask key and one merge-mask key for the
entire panel, rather than one user-text key per curve.

## Goals

- Enable Save after extrusion, mullion, divider-offset, and dimension edits.
- Persist edited H/V offsets, the full current cell key set, one panel-level segment mask, and one
  panel-level merge mask in the selected panel's Rhino attributes.
- Reload the masks into the editor so deleted boundaries and joined curve runs are reconstructed.
- Keep segment existence independent from curve joining: deleting a segment changes cell topology;
  joining segments only changes curve grouping.
- Include both masks in the deterministic panel type signature so different topologies cannot reuse
  the same type identity.
- Preserve panels without the new keys by treating them as all segments present and no joins.

## Architecture Ownership

- `Domain/PanelCladdingModels.cs` owns topology coordinates, merge runs, and the topology state.
- `Application/Services/PanelCladdingKeyService.cs` owns the two Rhino user-text keys and their
  compact, versioned binary-to-Base64 serialization contract.
- `Application/Services/PanelCladdingSaveService.cs` validates the requested edited grid, computes
  the v4 identity, and prepares the complete attribute mutation.
- `Infrastructure/Rhino/LivePanelCladdingRepository.cs` continues to own live Rhino object reads and
  the single Undo-wrapped attribute commit.
- `UI/PanelCladdingEditorWindow.xaml.cs` maps the editor's atomic extrusion IDs to domain topology
  coordinates, restores persisted state, and submits the edited working layout to Save.

## Key Design

### Panel-level keys

- `CW_2.05_SEGMENT_MASK`: one compact payload covering every atomic intermediate segment.
- `CW_2.06_MERGE_MASK`: one compact payload covering every possible junction between adjacent
  segments on the same track.

Both values use a versioned binary header followed by packed bits and are stored as Base64. The
header records payload kind and the horizontal-track, vertical-track, column, and row counts so a
mask cannot be silently applied to a different lattice.

### Canonical bit order

- Segment mask: horizontal tracks bottom-to-top with bays left-to-right, followed by vertical
  tracks left-to-right with bays bottom-to-top. `1` means present and `0` means absent.
- Merge mask: junctions use the same track order. `1` joins the two adjacent present segments and
  `0` keeps them as separate curve objects.
- A merge bit is invalid when either adjacent segment is absent.

### Compatibility and validation

- Missing segment key defaults to all segments present.
- Missing merge key defaults to no merged junctions.
- Payload version, kind, dimensions, byte length, padding, coordinate ranges, and merge validity
  are validated before the editor accepts or saves the topology.
- Structural save rewrites canonical offset and cell keys, removes obsolete grid cell/offset keys,
  and writes exactly the two topology keys.
- Type identity advances from schema v3 to v4 and hashes the canonical segment and merge payloads.

## Files Involved

- `src/PanelCladdingEditor/Domain/PanelCladdingModels.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladdingKeyService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladdingTypeSignatureService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladdingSaveService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/LivePanelCladdingRepository.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml.cs`
- affected existing panel-cladding regression expectations for the v4 signature
- `Project_Test/260818_TEST_panel-cladding-topology-persistence/`
- `Project_Exet/260818_EXET_panel-cladding-topology-persistence.md`

## Usage

1. Open a panel with `PCEditor` and switch to Extrusion view.
2. Add/delete intermediate segments or merge/explode adjacent segments.
3. Click Save.
4. Reopen the panel; its edited offsets, cells, absent segments, and merged runs are restored from
   Rhino object user text.

No manual editing or interpretation of the two mask values is required.

## Acceptance Criteria

- Save remains enabled for a projectable panel after a structural edit.
- Save no longer shows the structural-preview refusal dialog.
- One segment-mask key and one merge-mask key are written; no per-curve topology keys are created.
- A deleted boundary survives Save/reload and still merges the correct adjacent logical cells.
- A merged run survives Save/reload and is still shown as one curve; exploding it persists as
  separate segments without changing cell topology.
- Added H/V tracks and their new/renumbered cell keys survive Save/reload.
- Invalid or dimension-mismatched payloads fail explicitly.
- Legacy panels without masks load as a complete unmerged lattice.
- Identical geometry/materials with different segment or merge topology receive different v4 type
  signatures.
- Focused Debug/Release tests, production Debug/Release builds, package build, and compiled RHP
  assembly GUID validation pass.

## Risks and Rollback

- Risk: stale masks could be applied after a grid-count change. Mitigation: embed and validate grid
  dimensions in both payloads and regenerate both masks on every structural Save.
- Risk: joins might be mistaken for missing boundaries. Mitigation: separate payloads and reject
  joins across absent segments.
- Risk: newly added cells leave obsolete user-text keys. Mitigation: delete canonical old-grid keys
  not present in the requested grid during the same attribute commit.
- Risk: signature schema changes affect existing regression expectations. Mitigation: update focused
  assertions to v4 while keeping legacy stored signatures readable until the next Save.
- Rollback consists of reverting the production and test changes. Existing panels remain compatible
  because the two new keys are ignored by the old build; Rhino Undo covers each live attribute Save.

## Future Extensions

- Reuse the same topology state in spawn/surface-sync geometry partitioning so physical extrusion
  generation and cladding surface generation both honor irregular logical cells.
- Add a diagnostic export that decodes the opaque masks for support without changing the persisted
  schema.
- Add explicit mask migration only if a future binary schema version is introduced.
