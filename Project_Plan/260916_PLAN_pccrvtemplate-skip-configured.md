# PCCrvTemplate: skip configured panels in batches

## Background

The curve-template planner currently fails the entire selected batch when any panel has a
nonblank merge mask. The user explicitly requested changing this behavior on 2026-09-16.

## Goal

Skip panels with an existing merge mask and apply the chosen H/V template to the remaining
eligible panels. Report the skipped count and treat an entirely configured selection as a
successful no-op.

## Architecture ownership

- Application planning owns eligibility and the per-panel mutation plans.
- Domain plan/result records carry skipped panel IDs.
- The live Rhino adapter preserves selected IDs and propagates skipped IDs in successful results.
- The command remains a thin selection/priority adapter and reports the result.

This modifies only the standalone PanelCladdingEditor. MCP tools, host, routing, registration,
and product identity are unaffected.

## Key design

1. Retain distinct, nonempty panel-ID selection and priority validation.
2. Partition selected snapshots by the existing `HasMergeMask` eligibility flag before generating
   mutation plans. Existing nonblank merge masks, including manually configured ones, are skipped.
3. Keep skipped panels out of all mutation plans, including signature invalidation. Do not count
   eligible panels without mergeable runs as configured skips.
4. Preserve batch failure for other invalid panel/layout data, existing rollback behavior, and the
   single Undo record for actual changes. The all-configured path performs no writes or Undo work.
5. Keep full selected IDs in the result and add explicit skipped IDs. Report configured skips in
   the command output alongside the updated count.

## Files involved

- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingCurveTemplatePlanningService.cs`
- `src/PanelCladdingEditor/Domain/PanelCladdingModels.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingCurveTemplateService.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingCurveTemplateCommand.cs`
- `Packaging/PanelCladdingEditor/README.md`
- Existing curve-template and sparse-topology smoke projects under `Project_Test/260820_TEST_*`.
- `Project_Test/260916_TEST_pccrvtemplate-skip-configured/README.md`
- `Project_Exet/260916_EXET_pccrvtemplate-skip-configured.md`

## Usage

Run `_PCCrvTemplate`, select panel Breps (configured and unconfigured may be mixed), and choose
`HPriority` or `VPriority`. The result reports how many selected panels were updated and skipped.

## Acceptance criteria

- Mixed selections succeed for both priorities and plan writes only for unconfigured panels,
  regardless of where configured panels appear in the selection.
- Entirely configured selections succeed with no panel mutations.
- Duplicate IDs do not inflate skipped or planned counts; empty selection and invalid priority
  still fail. Invalid unconfigured panels still prevent any batch mutation.
- No-run eligible panels remain mask-free and are not counted as configured skips.
- Existing merge topology and signature values on skipped panels remain untouched.
- Focused regressions and standalone plug-in Debug/Release builds pass; `git diff --check` passes.

## Risks and rollback

Eligibility continues to mean a nonblank merge mask, not merely having panel offsets or materials.
Live layout-read failures remain validation errors. Revert this change's planner/result/reporting
edits and matching regression expectations to restore prior behavior. Do not overwrite unrelated
working-tree changes. Production installation is outside this request.

## Future extensions

None required for this behavior change.
