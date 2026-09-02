# PCMatchSrf Explicit Parent Cells PLAN

## Background

The live source panel `PID_BKT_N1_01_07` contains explicit parent-cell assignments `1A=0A`, `1B=0B`, `1C=0C`, and `1D=0D`. After matching it to `PID_BKT_N1_01_05`, the target retained only the `0A` through `0D` owner material keys.

`PCMatchSrf` currently validates the complete source graph and then collapses cells through extrusion topology before constructing writes. That collapse is useful for omitting absent hidden cells, but it must never discard a cladding-cell key that is explicitly present on the source panel. PCMatchSrf is a surface-cell command and intentionally does not require equal segment/merge topology.

The active Rhino installation is also still version `1.0.47`; newer packages are staged but not loaded. The fix therefore needs both a defensive source-projection rule and a newly versioned package that can be activated after Rhino closes.

## Goal

- Transfer every explicitly stored source cladding-cell assignment exactly, including parent references such as `1A=0A`.
- Continue omitting topology-hidden physical cells that are absent from the source attributes.
- Preserve blank writes for inferred surviving logical cells when the source has no explicit assignment.
- Preserve PCMatchSrf's cell-code compatibility rule and target-owned offsets, masks, type/signature, identity, and unrelated metadata.

## Architecture ownership

- Source cell projection belongs to `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingMatchPlanningService.cs`.
- Rhino transaction behavior remains in `LivePanelCladdingMatchService` and requires no change.
- Regression coverage belongs in `Project_Test/260819_TEST_pcmatch-srf-explicit-parent-cells/`.

## Key design

1. Parse and validate the complete source cell graph as today.
2. Collapse physical cells through topology to obtain the inferred surviving logical-cell writes.
3. Overlay every source cladding key that is explicitly present and belongs to the parsed H/V grid.
4. Preserve the explicit value token exactly after canonical storage encoding; a parent token remains a parent token and is not resolved to material.
5. Do not synthesize a topology-hidden cell when its key is absent from the source.
6. Continue deleting the target's existing cladding-cell keys before applying the complete projected source set.

## Files involved

- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingMatchPlanningService.cs`
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`
- `Project_Test/260819_TEST_pcmatch-srf-explicit-parent-cells/`
- `Packaging/PanelCladdingEditor/package-manifest.json`

## Usage

1. Run `PCMatchSrf`.
2. Select the target panels first.
3. Select the source panel last.
4. Explicit source parent assignments such as `1A=0A` are copied by cell code; extrusion topology and numeric offset values remain target-owned and outside this command.

## Acceptance criteria

- The reported 2-column by 4-row source graph copies `1A=0A`, `1B=0B`, `1C=0C`, and `1D=0D`.
- An explicit parent key remains copied even when source extrusion topology would otherwise collapse that physical cell.
- A topology-hidden cell absent from source user text remains omitted.
- Target H/V offsets, segment/merge masks, type/signature, identity, and unrelated text remain unchanged.
- Existing PCMatchSrf parent, logical-cell cleanup, and command regressions pass.
- Debug and Release solution builds, package creation, plug-in identity validation, and staged/install validation pass.

## Risks and rollback

- An explicitly stored stale source cell will be treated as intentional configuration. This is preferable to silent data loss and is consistent with cell-only matching; `PCSyncSrf` or PCEditor can be used first to normalize source attributes when needed.
- Roll back by removing the explicit-cell overlay and reinstalling package `1.0.51`.

## Future extensions

- Add a non-mutating PCMatchSrf preview listing source keys that are inferred versus explicitly stored.
- Add a source-normalization warning when explicit cell keys conflict with extrusion topology.
