# PCSyncSrf Parent-Cell Persistence PLAN

## Background

`PCSyncSrf` can reconstruct a spanning cladding region in the surface planner as an owner/material plus child/owner references, for example `0A=MPL-001` and `1A=0A`. The live command can nevertheless finish with the child key absent. The current workflow hands the final panel write through type-signature normalization and deletes every existing cladding key before writing the replacement set, but it does not prove that the geometry-derived parent reference is still present in the final write or committed Rhino attributes.

## Goal

- Treat the surface planner's complete geometry-derived logical cell graph as the authoritative persistence payload.
- Preserve parent references such as `1A=0A` exactly through orchestration and Rhino commit.
- Persist every logical cell key, including blanks, so surface sync never makes the panel layout disappear.
- Reject and roll back a sync when the committed panel attributes differ from the planned logical cell graph.

## Architecture ownership

- Complete logical-cell write construction belongs to `PanelCladdingSurfaceSyncService` in Application.
- Pure panel-write postcondition validation belongs to the surface-sync planning/application layer.
- Rhino read-back and rollback belong to `LivePanelCladdingSurfaceSyncRepository` in Infrastructure.
- Regression coverage belongs in `Project_Test/260819_TEST_pcsync-srf-parent-persistence/`.

## Key design

1. Build the stored cell payload from every cell in the inferred layout and the planner's geometry-derived value for that exact key.
2. Fail before mutation if any inferred layout cell is absent from the planner payload.
3. Encode blank values with the existing persisted-blank sentinel; leave material and parent tokens unchanged.
4. Continue using normalized values for type/signature calculation, but do not substitute the normalized dictionary for the authoritative surface-sync write graph.
5. After each panel attribute commit, read the object user text and verify every planned cell key/value exactly, including `1A=0A`.
6. On verification failure, restore all already-modified objects and return failure.

## Files involved

- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSurfaceSyncService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSurfaceSyncPlanningService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSurfaceSyncRepository.cs`
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`
- `Project_Test/260819_TEST_pcsync-srf-parent-persistence/`
- `Packaging/PanelCladdingEditor/package-manifest.json`

## Acceptance criteria

- One spanning surface over `0A` and `1A` produces a final commit request containing `0A=MPL-001` and `1A=0A`.
- Type/signature calculation does not remove or replace the planned parent reference in the persistence payload.
- Every inferred layout cell is written; blank cells remain present with the persisted-blank sentinel.
- A missing planned cell fails before mutation.
- Postcondition validation rejects a missing or changed `1A` value.
- Existing surface-sync structural-grid, base surface-sync, and panel-cladding regressions pass.
- Debug/Release builds, package creation, and plug-in identity validation pass.

## Risks and rollback

- The command becomes stricter and will reject incomplete surface coverage instead of committing a partial logical graph. This is consistent with geometry-authoritative surface sync.
- Roll back by restoring normalized cell values as the panel-write source and removing post-commit validation, then reinstalling the prior package.
