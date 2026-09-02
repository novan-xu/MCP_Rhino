# Panel Cladding Extrusion Spawn and Geometry Sync Plan

## Background

`PCSpawn` currently creates only cladding Breps, beneath `03_Material Surfaces (STEP)`, while the
extrusion topology edited in `PCEditor` exists only as panel key/value masks and an editor preview.
`PCSyncFromSurfaces` discovers surfaces by their existing PID attributes and repairs only surface
and panel cladding metadata. That makes manually edited curve/surface geometry impossible to use as
the authoritative layout and leaves no way to regenerate extrusion objects from the saved topology.

## Goals

- Make `PCSpawn` create the exact frame and internal extrusion curves represented by the editor's
  offsets, segment mask, and merge mask.
- Use underscore curve codes such as `FRM_0` and `INT_A0` everywhere.
- Put spawned curves on `02_CW Extrusions::Curves-PNL::Main Frame` and write PID, CRV, and a CID
  composed as `<panel CID>-<curve code>`.
- Spawn new cladding surfaces beneath `04_STEP Surfaces`; retain read compatibility for the legacy
  root so existing projects can be synchronized.
- Replace the visible `PCSyncFromSurfaces` command with `PCSync`.
- Make `PCSync` associate surfaces and curves to selected panels from geometry and layer placement,
  infer offsets and curve topology, reconstruct panel cell/topology/type/dimension attributes, and
  repair surface/curve metadata from the inferred layout.
- Store width, height, and combined unit dimensions with five decimal places.

## Architecture Ownership

- Domain models describe curve plans and geometry-derived sync snapshots without Rhino types.
- Application planning owns curve naming, mask-to-segment expansion, CIDs, required metadata,
  surface region ownership, and desired object attributes.
- Rhino infrastructure owns Brep/curve sampling, panel association, local-frame classification,
  curve extraction/joining, layer creation, and transactional document mutations.
- The UI command remains a thin selection and reporting adapter.

## Key Design

1. Introduce a shared extrusion planning service that expands the panel topology into four frame
   descriptors plus present internal segments, joins only the declared merge runs, and produces
   stable underscore codes.
2. Extend the spawn plan/result with curves. Prepare all Breps and curves before starting one Rhino
   Undo record, reject duplicate/existing CIDs across both object kinds, and roll back the full batch
   on any failure.
3. Enumerate surface candidates only under supported material roots and curve candidates only under
   the exact extrusion layer. Associate candidates to selected panels by geometric coincidence and
   overlap, never by their existing PID/CID/CRV values.
4. Infer divider offsets from surface boundaries and internal curves. Classify each extrusion curve
   in the panel-local frame, map its span to atomic bays, derive missing segments from absent bays,
   and derive merge runs when one physical curve covers adjacent bays.
5. Resolve material regions from surface geometry and the material leaf layer, assign canonical
   owner/parent cell values, recalculate type identity, and write the rebuilt panel grid/topology.
6. Rewrite associated surface PID/CID/Cladding and curve PID/CID/CRV values from the new layout.
7. Write `CW_2.01_UNIT_WIDTH`, `CW_2.02_UNIT_HEIGHT`, and `CW_2.00_UNIT_DIMENSION` with invariant
   five-decimal formatting in panel-local model units.

## Files Involved

- `src/PanelCladdingEditor/Domain/PanelCladdingModels.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingExtrusionPlanningService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSpawnPlanningService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSurfaceSyncPlanningService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSurfaceSyncService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingGeometryPartitionService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSpawnService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSurfaceSyncRepository.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingExtrusion.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingSyncCommand.cs`
- focused and existing `Project_Test` regressions, packaging manifest, and documentation

## Usage

1. Save a panel layout in `PCEditor`, including deleted and merged extrusion segments.
2. Run `_PCSpawn` and select the panel. Cladding surfaces and extrusion curves are created together.
3. Manually adjust the associated surfaces/curves while preserving their designated layer families.
4. Run `_PCSync` and select the source panel. The command infers the edited layout from geometry,
   repairs object attributes, and rewrites the panel's cells, masks, type, and unit dimensions.

## Acceptance Criteria

- Spawn output includes four correctly named frame curves and the exact present/merged internal
  curves represented by a non-trivial topology mask.
- Every spawned curve is on the required extrusion layer and has the correct PID, CRV, and CID.
- New surfaces use `04_STEP Surfaces`; sync accepts both new and legacy material roots.
- Geometry association and layout inference still work when existing curve/surface metadata is blank
  or wrong.
- PCSync reconstructs offsets, segment mask, merge mask, parent-cell values, and surface/curve
  metadata from geometry/layers.
- Unit width, height, and combined dimensions use exactly five decimal places.
- Only `_PCSync` is registered; `_PCSyncFromSurfaces` is absent.
- Existing editor/save/match/spawn/sync regressions, Debug/Release solution builds, package build,
  hashes, and compiled RHP identity checks pass.

## Risks and Rollback

- Curved panels can have ambiguous local projections. Association/classification must fail closed
  with a panel-specific issue instead of mutating uncertain objects.
- Coincident selected panels can make geometry ownership ambiguous. Those panels are skipped until
  selection or geometry is disambiguated.
- One physical curve spanning adjacent atomic bays is interpreted as a merge run; individual
  touching curve objects remain unmerged. This makes merge state recoverable from geometry.
- Rollback consists of reverting the coordinated planner/infrastructure/UI changes. Live spawn and
  sync mutations remain covered by one Rhino Undo record per command.

## Future Extensions

- Add optional diagnostic preview/highlighting before committing a geometry-derived sync.
- Support separate extrusion profile families/layers after their naming and attribute contract is
  defined.
