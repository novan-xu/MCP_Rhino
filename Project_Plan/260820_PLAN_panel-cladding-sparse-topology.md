# Panel Cladding Sparse Topology Persistence

## Background

Panel extrusion topology has three independently defaultable states:

- `CW_2.05_SEGMENT_MASK`: all segments present by default;
- `CW_2.06_MERGE_MASK`: all segments remain atomic by default;
- `CW_2.07_HIDE_MASK`: all present segments are visible by default.

The current writers persist all three encoded payloads whenever extrusion topology is saved or
inferred. That creates redundant attributes for untouched panels and for one-row/one-column cases
where no merge is possible. It also prevents a hide-only edit from being represented by only its
hide mask. The current curve-template command overwrites an existing merge mask, while the desired
workflow is for it to initialize only panels whose merge code is absent.

## Goal

- Persist each topology attribute only when its corresponding state differs from its implicit
  default.
- Treat absent topology attributes as valid defaults in every read and curve-spawn path.
- Remove stale default mask attributes during save, create, match, and surface-sync reconciliation.
- Make `PCCrvTemplate` reject a selected batch if any panel already has a nonblank merge mask.
- Do not create a merge attribute when the requested template produces no merge runs.

## Architecture Ownership

- `Application/Services/PanelCladdingKeyService.cs`: validate topology once, produce full canonical
  payloads for signatures, and expose sparse non-default persistence writes.
- Save/create/curve-match planning services: delete prior topology keys and write only the sparse
  payload set.
- Spawn planning/live services: rely on topology decoding defaults instead of requiring explicit
  segment and merge attributes.
- `PanelCladdingCurveTemplatePlanningService` and its live adapter: enforce merge-mask absence and
  no-op when no merge run is applicable.
- `LivePanelCladdingSurfaceSyncRepository`: compare and commit sparse topology representation.
- `Domain/PanelCladdingModels.cs`: carry whether a template input already has a merge code.
- `Project_Test/260820_TEST_panel-cladding-sparse-topology/`: focused sparse persistence and template
  regressions, with relevant existing suites updated where the old explicit-mask contract changed.

The canonical topology binary format and type-signature format are unchanged. Missing keys decode
to the existing default bit values.

## Key Design

1. Keep `EncodeTopology` unchanged as the full canonical codec used for validation, decoding
   round-trips, and signatures.
2. Add a sparse encoding operation that first calls the canonical encoder, then emits:
   - segment mask only when `MissingSegments` is non-empty;
   - merge mask only when `MergeRuns` is non-empty;
   - hide mask only when `HiddenSegments` is non-empty.
3. All authoritative writers remove existing topology keys before applying sparse writes. This
   both stores new defaults sparsely and cleans old explicit-default payloads.
4. Surface-sync change detection compares exact case-insensitive key presence and canonical sparse
   values, so an old explicit-default or blank key is scheduled for cleanup.
5. Remove the explicit-topology precondition from curve spawning. Parsing already decodes absent
   values to all-present, all-atomic, all-visible topology.
6. Snapshot nonblank merge-key presence before `PCCrvTemplate` planning. Reject the complete batch
   before mutation if any selected panel already has a merge code. Blank keys count as absent.
7. Template plans with zero merge runs carry no merge write and invalidate no signatures.

## Files Involved

- `src/PanelCladdingEditor/Application/Services/PanelCladdingKeyService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladdingSaveService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingCreatePlanningService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingCurveMatchPlanningService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSpawnPlanningService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingCurveTemplatePlanningService.cs`
- `src/PanelCladdingEditor/Domain/PanelCladdingModels.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSpawnService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingCurveTemplateService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSurfaceSyncRepository.cs`
- relevant existing topology, save, create, match, spawn, and template regression projects
- `Project_Test/260820_TEST_panel-cladding-sparse-topology/` (new)
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`
- `Project_Exet/260820_EXET_panel-cladding-sparse-topology.md` (after verification)

## Acceptance Criteria

- Default topology produces none of the `2.05`-`2.07` attributes.
- Missing-only, merge-only, and hide-only topology produce exactly their respective attribute.
- A hide-only editor save removes stale segment/merge defaults and retains only `2.07`.
- One-row/one-column template cases that cannot merge produce no merge attribute.
- Absent topology attributes parse and spawn as the existing all-present, all-atomic, all-visible
  default.
- Save, create, curve match, and surface sync converge on the same sparse representation.
- `PCCrvTemplate` fails without mutation when any selected panel has a nonblank `2.06` value and
  remains eligible when the key is absent or blank.
- Type signatures continue using the unchanged full canonical payloads.
- Focused and relevant regression tests, Debug/Release builds, compiled RHP assembly GUID checks,
  and `git diff --check` pass.

## Risks And Rollback

- Older tests or integrations may treat topology-key presence as an "editor configured" marker.
  That interpretation is deliberately removed; dimensions/offsets and normal parsing govern
  readiness, while absent mask keys mean default topology.
- Surface sync must distinguish an absent key from a present blank key to clean the latter.
- Rollback restores full three-mask writes and the explicit spawn guard. Existing sparse documents
  remain readable because the decoder already supports missing keys.
