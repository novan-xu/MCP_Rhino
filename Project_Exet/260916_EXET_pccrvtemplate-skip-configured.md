# PCCrvTemplate: configured panels no longer block a batch

## Corresponding plan

- [Plan](../Project_Plan/260916_PLAN_pccrvtemplate-skip-configured.md)
- Execution date: 2026-09-16.
- Authorization: the user's explicit request to ignore already-configured panels and apply to the rest.

## Related artifacts

- [Validation record](../Project_Test/260916_TEST_pccrvtemplate-skip-configured/README.md)
- Regression implementations: `Project_Test/260820_TEST_panel-cladding-curve-template-ui/Program.cs`
  and `Project_Test/260820_TEST_panel-cladding-sparse-topology/Program.cs`.
- No commit or pull request created.

## Implemented scope

The application planner records distinct configured panel IDs in `SkippedPanelIds` and omits those
panels from mutation plans. Unconfigured panels retain the existing H/V template generation and
validation. All-configured selections return a successful empty plan.

The live adapter propagates skipped IDs while preserving the full selected-ID list. Its existing
empty-prepared-list path returns success before opening an Undo record. Only planned eligible
panels can reach attribute writes or signature invalidation. The command reports the skipped count
after its existing updated-panel/merge-run summary. Package usage documentation describes mixed
and all-configured selections.

## Deviations from plan

No production-scope deviations. Regression cases extend existing test projects, with this change's
TEST folder recording their commands and results. A focused command-line switch avoids running
unrelated WPF layout checks. One stale sparse-topology fixture invocation was adapted to the current
spawn-planner signature by passing its existing source layer path.

## Issues found and fixed

- Existing merge masks were treated as fatal panel-plan errors. They are now configured skips.
- Older curve-template/sparse-topology assertions expected rejection; they now require successful
  no-op plans and explicit skipped IDs.
- Sparse-topology smoke initially failed with CS7036 for missing `sourcePanelLayerPath`; the fixture
  now passes `layout.LayerFullPath`. No spawn production behavior changed.

## Test record

The [TEST record](../Project_Test/260916_TEST_pccrvtemplate-skip-configured/README.md) lists exact
commands. The focused curve-template regression failed against the original planner (exit 1), then
passed in Debug and Release (exit 0). The corrected sparse-topology smoke also passed in both
configurations (exit 0). Standalone plugin Debug/Release builds passed with zero warnings/errors.
`git diff --check` passed; Git emitted only normal LF-to-CRLF normalization notices.

Standalone builds are appropriate because there are no MCP server/host, Router, transport, tool
registration, or document-session changes. No new live CLI smoke or command registration is needed.

## Acceptance alignment

- Both priorities and configured-panel selection positions are covered by passing mixed-batch tests.
- All-configured, duplicate-ID, empty-selection, invalid-priority, and invalid-eligible-panel cases pass.
- No-run eligible panels remain mutation-free without being classified as configured skips.
- Existing topology preservation and codec round trips pass.
- Live service review confirms skipped panels never enter prepared writes, skipped IDs reach the
  result, and all-configured selection returns before Undo/mutation. Actual Rhino writes and Undo
  were not exercised by these standalone tests.

## Rollback verification

The failing pre-change regression reproduces the prior rejection behavior. Restoring this change's
planner/result/reporting edits would restore that behavior; no production rollback was necessary
or performed. Existing live rollback logic was not changed. Unrelated working-tree edits were preserved.

## Remaining items

Optional live Rhino acceptance is documented in TEST and was not executed. No package/version bump,
production install, registry activation, or installed-plugin replacement was performed.

## Conclusion

The requested batch behavior is implemented and passes focused automated regressions and both
standalone plugin builds. Already-configured panels are skipped; eligible panels retain batch validation
and mutation behavior.
