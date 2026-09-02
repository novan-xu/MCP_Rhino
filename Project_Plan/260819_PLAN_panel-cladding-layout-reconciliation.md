# Panel Cladding Layout Reconciliation PLAN

## Background

The current panel-cladding workflow has the right attribute ownership but two operations still compare or rebuild layouts too literally:

- `PCMatchSrf` accepts targets by raw grid/cell shape instead of answering the broader question: can the target extrusion topology represent the source cladding partition?
- `PCSyncSrf` safely preserves stored structural offsets during a surface-only sync, but a row/column count change causes topology masks and curve identifiers to be rebuilt by index. That loses valid merge, segment, and hide intent even when most physical tracks have not moved.
- Panel save, match, and sync paths still produce a persisted `Signature` user-text value even though the authoritative state already exists in `CW_2.05` through `CW_2.08` and the `CW_4.xx_CLADDING_*` cell payload.

The open `Untitled 1.3dm` document provides the required live regression case. The state-1 panel has vertical offsets `25.0729`, `42.80245`, and `70.65855`. The state-2 panel inserts `33.93768`, producing `25.0729`, `33.93768`, `42.80245`, and `70.65855`. The old tracks therefore have stable geometric identities even though their indices change:

- old `V0` remains new `V0`;
- old `V1` becomes new `V2`;
- old `V2` becomes new `V3`;
- new `V1` is the inserted track.

The same fact applies to curve codes and merge runs. The curves formerly named for `INT_1` and `INT_2` must be reindexed to `INT_2` and `INT_3`, while their existing merged state remains merged. Resetting all three topology masks is safe in the narrow sense that it creates valid data, but it discards unrelated design work and is not acceptable for a small surface split.

The two live states are a regression fixture, not a transformation template. The implementation must use one general reconciliation algorithm for any ordered combination of inserted, removed, retained, or moved horizontal and vertical tracks. It must not contain panel IDs, state values, fixture coordinates, fixed grid sizes, or one-off `INT_*` renaming rules.

This plan treats numeric indices as serialization details. Retained physical tracks and cladding regions are identified geometrically and topologically first, then deterministically serialized into the new index space.

## Goal

- Make `PCMatchSrf` apply whenever the target extrusion layout can represent the source cladding layout, regardless of numeric offset values, redundant internal tracks, or `CW_2.06_MERGE_MASK` differences.
- Make `PCSyncSrf` reconcile inserted, retained, and removed grid elements without resetting unaffected `CW_2.05`, `CW_2.06`, or `CW_2.07` state.
- Reindex associated curve `CID`, `CRV`, and Rhino object names when grid numbering changes, without changing retained curve geometry or merge intent.
- Keep the current cladding persistence contract: `CW_4.xx_CLADDING_*` remains the authoritative material/parent payload, and `CW_2.08_CLADDING_LOGIC` remains its derived, material-free cell graph.
- Stop producing persisted `Signature` and legacy `CW_4.00_CLADDING_SIGNATURE` user-text values.
- Preserve transactional behavior: validate the complete selection before mutating Rhino objects or the workbook, and commit within one Rhino undo record.

## Architecture ownership

- Domain owns immutable descriptions of grid correspondence, logical cladding regions, feasible target assignments, and reconciliation diagnostics. These models contain no Rhino types.
- Application owns coordinate-tolerant track matching, topology-mask remapping, semantic `PCMatchSrf` feasibility, canonical cell-parent rebuilding, and the removal of persisted-signature dependencies.
- Infrastructure owns extraction of panel/surface/curve geometry, collection of all snapshots needed by the Application services, Rhino attribute writes, workbook migration, object-name updates, rollback, and undo behavior.
- UI command classes remain thin adapters that collect selections, invoke the existing planning/application services, and report deterministic failure messages.
- Packaging retains the existing plug-in identity and validates the assembly-level GUID before installation.

## Key design

### 1. Define semantic cladding representability for `PCMatchSrf`

Replace raw cell-label/count equality with a pure feasibility service.

1. Decode the source `CW_4.xx_CLADDING_*` values and `CW_2.08_CLADDING_LOGIC` into canonical owner regions. Parent cells belong to their resolved owner; material is attached only to the owner region.
2. Remove source row/column divisions that never separate two logical cladding regions. These are redundant extrusion tracks inside a cladding region, not required cladding boundaries.
3. Decode the target `CW_2.05_SEGMENT_MASK` into atomic target zones. Adjacent target cells connected across a missing divider segment form an indivisible zone for matching purposes.
4. Find an order-preserving assignment from the source logical regions to unions of target zones. A match is achievable when:
   - every target indivisible zone maps wholly to one source logical region;
   - every required source region boundary can be represented by available target divider segments;
   - every target cell is assigned exactly once; and
   - source region adjacency and material ownership are preserved.
5. Treat target tracks that fall inside one mapped source region as harmless redundancy. Generate parent references across those target cells so the extra extrusion tracks do not create extra cladding regions.
6. Ignore numeric H/V offset values, `CW_2.06_MERGE_MASK`, and `CW_2.07_HIDE_MASK` when deciding cladding representability. They affect extrusion realization, not the logical cladding partition.
7. When more than one assignment is feasible, choose the lexicographically first order-preserving mapping, top-to-bottom and left-to-right. Do not use offset magnitudes as a compatibility or tie-breaking condition.
8. Reject only when no complete assignment exists. Return a diagnostic naming the first required source boundary or target indivisible zone that prevents representation.

`PCMatchSrf` must continue to preserve the target panel's offsets and all three extrusion topology masks. It writes only the mapped `CW_4.xx_CLADDING_*` payload, the derived `CW_2.08_CLADDING_LOGIC`, cladding type metadata that remains supported, and associated surface attributes/layers.

### 2. Reconcile `PCSyncSrf` by geometric identity before reindexing

Add a pure Application reconciliation service that receives the stored layout, stored topology, surface-inferred layout, associated-curve evidence, and model tolerance. Its result contains new offsets, old-to-new track/bay maps, remapped topology, cell-region correspondence, curve reindex instructions, and diagnostics.

Track classification is evidence-based and order-preserving:

- **Retained:** an old and new coordinate match within model tolerance, or an unmatched ordered pair is the unique best continuation after normalized-position, neighboring-track, associated-curve, and region-boundary evidence are considered. This permits a retained track to move when a surface is resized. Preserve identity even when its coordinate and serialized index change.
- **Inserted:** a new coordinate has no old match. Derive the new boundary's segment state from associated curve coverage when present, otherwise from the edited surface boundary that introduced it.
- **Removed:** remove an old structural track only when authoritative curve-sync evidence shows it is gone. Surface-only absence is not sufficient; `PCSyncSrf` continues preserving stored structural offsets under the existing structural-grid rule.

Use deterministic ordered-sequence alignment independently for H and V tracks. Exact tolerance matches are anchors; remaining spans use a minimum-cost monotonic alignment with explicit insert/delete costs and normalized coordinates so panel resizing does not masquerade as wholesale replacement. Associated curve coverage and cladding-boundary continuity reduce the cost of retaining a track. Equal-cost alignments that would produce different topology are ambiguous and must fail before mutation.

Topology remapping rules:

- Copy retained segment, merge, and hide atoms through the coordinate correspondence instead of through the old array index.
- When an inserted row/column splits an old bay, expand the old bay's unaffected segment and merge runs across its child bays. A retained merged curve stays one merged run unless curve geometry positively shows a split.
- Infer atoms on the inserted track from curve geometry first. If the edit contains only a cladding boundary, persist the segment state required by that boundary without changing unrelated tracks.
- Preserve `CW_2.07` on remapped atoms. New atoms default visible unless geometry or an explicit existing value supplies hide intent.
- If curve and surface evidence conflict, or a curve cannot be assigned uniquely within tolerance, fail that panel before any mutation. Never fall back to clearing all masks.

For the live state-1/state-2 case, the planned mapping must preserve the topology carried by old `V0`, `V1`, and `V2`, insert new `V1`, and expand only the old bay that was divided. No other row, column, segment, merge run, or hide atom may change.

The same rules must work for one or many insertions on either axis, simultaneous H/V changes, moved tracks after resizing, and authoritative removals without any fixture-specific branch.

### 3. Reindex associated curves as part of surface reconciliation

Surface-scope snapshot creation must include associated curve geometry and attributes after the reconciled grid is known. For every retained curve:

- resolve its atomic coverage on the new grid;
- regenerate canonical `CRV` code and `CID` from that coverage;
- update the Rhino object name and the supported curve user text together;
- preserve curve geometry, layer, and merge realization unless the edited evidence requires a change.

In the live insertion case, the curves represented as `INT_1` and `INT_2` in state 1 become `INT_2` and `INT_3` in state 2, and both remain merged. The operation must not delete and respawn retained curves merely to rename them.

### 4. Rebuild cladding cells without changing persistence ownership

After the new grid and topology are reconciled:

1. Resolve edited surfaces against the new atomic cells.
2. Map old logical owners to the new grid by geometric coverage, not old cell labels.
3. Assign deterministic owners to genuinely split regions and regenerate all parent references in canonical row/column order.
4. Delete obsolete `CW_4.xx_CLADDING_*` keys and write the complete new authoritative set.
5. Derive `CW_2.08_CLADDING_LOGIC` from that complete `CW_4.xx` graph using the existing material-free serialization rules.

This deliberately leaves the current ownership direction unchanged. `CW_2.08` is not promoted to a second editable source of truth, and no material value is written into it.

### 5. Retire the persisted Signature key

The scope of this change is the panel user-text signature, not the supported cladding type code.

- Stop writing both `Signature` and `CW_4.00_CLADDING_SIGNATURE` in create, editor save, match, spawn-related persistence, curve sync, and surface sync flows.
- Delete either key case-insensitively whenever a panel is successfully touched by one of those operations. Untouched panels are not bulk-mutated solely for cleanup.
- Keep legacy signature reads only where required to migrate an existing workbook or panel; a legacy value must never control current compatibility or block an operation.
- Replace workbook logic that requires a stored panel signature with direct canonical comparison of the authoritative layout payload. An internal transient hash may be used for lookup performance, but it must be computed from canonical `CW_2.05`-`CW_2.08` plus relevant `CW_4.xx` data and must never be persisted as a Rhino `Signature` key.
- Keep `CW_1.10_CLADDING_TYPE` unless a later approved plan explicitly removes it. Type labeling and persisted signatures are separate concerns.
- Continue recognizing the two signature keys as clearable legacy keys so `PCClear` and normal touched-panel cleanup can remove old data.

### 6. Preserve transaction and command boundaries

- Both match and sync paths gather complete read snapshots first.
- Application planning validates every selected panel and produces a complete write plan before Infrastructure changes the document.
- Any invalid or ambiguous geometry aborts the batch with no partial panel, curve, surface, document-string, or workbook mutation.
- Rhino writes use one undo record and retain the existing explicit rollback path for failed attribute changes or workbook finalization.
- `PCMatchCrv`, `PCSpawnCrv`, `PCSpawnSrf`, and the editor continue consuming the same authoritative mask/cell keys; their behavior changes only where signature output is removed or reindexed data is now more accurate.

## Files involved

Expected production scope:

- `src/PanelCladdingEditor/Domain/PanelCladdingModels.cs`
- `src/PanelCladdingEditor/Application/Interfaces/IPanelCladdingServices.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladdingKeyService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladdingTypeSignatureService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladdingSaveService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingLogicalCellService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingLogicService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingMatchPlanningService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSurfaceSyncPlanningService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSurfaceSyncService.cs`
- a new Application-level layout feasibility/reconciliation service under `src/PanelCladdingEditor/Application/Services/PanelCladding/`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingMatchService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSurfaceSyncRepository.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingCreateService.cs`
- `src/PanelCladdingEditor/Infrastructure/File/OpenXmlPanelCladdingWorkbookRepository.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorController.cs`
- `Packaging/PanelCladdingEditor/package-manifest.json`

Expected verification artifacts:

- `Project_Exet/260819_EXET_panel-cladding-layout-reconciliation.md`
- `Project_Test/260819_TEST_panel-cladding-layout-reconciliation/`
- existing focused panel-cladding match, topology, surface-sync, split/spawn/sync, parent-persistence, scoped-save, clear, and assembly-identity suites.

The execution pass must confirm actual call sites before editing; files with no required behavior change should remain untouched.

## Usage

- `PCMatchSrf`: select one source panel/surface set and compatible target panels as today. Compatibility is evaluated semantically, so different offset numbers, different merge masks, and redundant internal tracks no longer prevent an achievable match.
- `PCSyncSrf`: after manually splitting, resizing, or relayering cladding surfaces, run the existing command. It reconciles the new grid, retains unaffected extrusion topology, reindexes associated curve metadata, and rebuilds `CW_4.xx` plus derived `CW_2.08`.
- `PCSyncCrv`: remains the authoritative command for intentional structural-track removal based on edited curve geometry.
- No new user-facing signature command or setting is introduced. Touched panels simply cease to contain the retired signature keys.

## Acceptance criteria

### `PCMatchSrf`

- A source whose intermediate extrusion segments are merged matches a target whose corresponding segments are separate when both can produce the same cladding regions.
- Different numeric H/V offset values do not prevent a match.
- A target with extra tracks inside a source cladding region matches; generated target cells use parent references so the logical cladding region remains one piece.
- A source with redundant tracks inside its regions can match a target with fewer tracks when all essential source boundaries remain representable.
- A target is rejected when a `CW_2.05`-defined indivisible zone crosses a required source cladding boundary.
- `CW_2.06` and `CW_2.07` never decide `PCMatchSrf` compatibility and remain unchanged on the target.
- Multiple feasible mappings produce the same deterministic result on repeated runs.

### `PCSyncSrf`

- The exact `Untitled 1.3dm` state-1/state-2 fixture maps old vertical tracks `V0 -> V0`, `V1 -> V2`, and `V2 -> V3`, with inserted `V1=33.93768`.
- Unaffected state-1 segment, merge, and hide atoms survive byte-for-byte after decoding and coordinate remapping; only atoms introduced or divided by the new track may differ.
- Curves formerly identified by `INT_1` and `INT_2` are renamed/rekeyed to `INT_2` and `INT_3` and remain merged.
- Retained curves keep their Rhino geometry and layers.
- Surface-only sync does not delete a stored structural track merely because no surface boundary or associated curve exposes it.
- Curve/surface conflicts abort before mutation instead of resetting all topology masks.
- Updated `CW_4.xx_CLADDING_*` values accurately describe the resized/split surfaces, and `CW_2.08_CLADDING_LOGIC` is the material-free derivation of that graph.
- Re-running sync without geometry changes is idempotent.
- Multiple insertions on one axis, simultaneous H/V insertions, retained-track movement after resizing, and authoritative curve-scope removals all use the same reconciliation service and preserve every unaffected topology atom.
- Production code contains no reference to the fixture's state values, panel IDs, coordinates, grid dimensions, or literal `INT_1`/`INT_2` transition.

### Signature retirement and regression safety

- No successful create, editor-save, match, spawn-related save, curve-sync, or surface-sync path writes `Signature` or `CW_4.00_CLADDING_SIGNATURE`.
- A touched panel containing either legacy key has both removed after successful commit.
- No current operation requires a signature user-text value to determine compatibility, type reuse, workbook persistence, spawn, or sync behavior.
- `CW_1.10_CLADDING_TYPE` remains supported.
- Existing panels and workbooks that contain signatures remain readable during migration.
- Batch validation, rollback, one-undo-record behavior, and untouched-panel isolation remain intact.
- Focused Debug and Release builds pass.
- The updated package is built and installed for the current user; the compiled RHP has a non-empty assembly GUID matching the manifest, plug-in declaration, and registry identity.
- A live Rhino rerun against a copy of `Untitled 1.3dm` verifies the state-1/state-2 reconciliation and confirms that no Signature key is produced.

## Risks and rollback

- A semantic feasibility solver is broader than raw shape equality and could choose an unintended mapping when a layout is symmetric. The deterministic top/left ordering rule and focused symmetric-layout tests make the choice stable and inspectable.
- Surface edits do not always prove that a structural track was removed. The plan deliberately preserves stored tracks in surface scope and leaves authoritative removal to `PCSyncCrv`.
- Curve evidence and surface evidence can disagree. The safe behavior is a pre-mutation failure with a panel/track diagnostic, not silent topology reset.
- Workbook type reuse currently depends heavily on stored signatures. Migration must be completed atomically so a mixed implementation cannot create duplicate or orphaned type records.
- Removing signature output may affect external consumers not present in this repository. The rollback path is to reinstall the previous package; no destructive bulk migration is planned, and legacy values on untouched panels remain available.
- If execution reveals that `CW_1.10_CLADDING_TYPE` is semantically inseparable from persisted signatures, stop and report that architecture gap before expanding this plan to remove or redesign type codes.

## Future extensions

- Add a non-mutating preview that displays the chosen source-region to target-zone mapping before `PCMatchSrf` commits.
- Expose reconciliation diagnostics in the editor, including retained, inserted, removed, and ambiguous tracks.
- Reuse the same coordinate-correspondence service for direct editor row/column insertion so editor edits and geometry sync share one remapping contract.
- Consider an explicit structural-track deletion marker for surface workflows if users later need `PCSyncSrf` itself to authorize track removal without a curve edit.
