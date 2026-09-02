# Panel Cladding Match Parent Cells Execution

## Corresponding Plan

- Plan: `Project_Plan/260818_PLAN_panel-cladding-match-parent-cells.md`
- Execution date: 2026-08-18

## Related Artifacts

- Focused tests: `Project_Test/260818_TEST_panel-cladding-match-parent-cells/`
- Updated broad match regression: `Project_Test/260805_TEST_panel-cladding-match/`
- Production implementation:
  `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingMatchPlanningService.cs`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.40/`
- Staged plug-in:
  `%LOCALAPPDATA%/PanelCladdingEditor/staged/1.0.40-20260819032418826/Plugin/PanelCladdingEditor.rhp`
- Commit / PR: none created in this execution.

## Execution Result / Actual Delivered Scope

`PCMatch` now treats source cell values as semantic cladding assignments rather than assuming each
nonblank value is a material code. `PanelCladdingMatchPlanningService` constructs a
`PanelCladdingRegionService` from its existing key service and resolves the complete source cell set
before target planning.

The planner writes `PanelCladdingRegionSet.NormalizedCellValues`. In this canonical representation:

- a region owner retains its material code;
- every child retains a parent-cell label pointing to the canonical owner; and
- blank cells, missing reference targets, references to blank cells, cycles, disconnected regions,
  and owners without materials fail source eligibility before any target plan is returned.

For the reported 2x2 case, every target plan contains:

```text
0A=MPL-001
0B=MPL-001
1A=0A
1B=0B
```

The existing `PCMatch` topology contract remains unchanged: the segment and merge masks follow the
source, target H/V offsets remain untouched, and the source/target logical grid dimensions must
match.

## Differences From Plan

The implementation followed the plan. No live Rhino adapter change was necessary because
`LivePanelCladdingMatchService` already writes the planner's values verbatim in one Undo-wrapped
attribute commit.

## Problems Found And Fixed During Construction

The match planner previously called the generic string normalizer per cell, named each result
`material`, and performed no parent-region validation. Parent references happened to share the same
storage field as material codes, but their semantic relationship was not part of the match planning
contract or regression suite. The planner now delegates that distinction to the existing region
service, and both the reported relationship and invalid-graph behavior are explicitly covered.

## Test Record

### Focused parent-cell smoke

Debug and Release both exited `0` and reported:

```text
[OK] planned writes preserve 0A/0B parent references instead of flattening them.
[OK] reparsed target reconstructs two MPL-001 regions owned by 0A and 0B.
[OK] invalid source parent-reference cycles fail before target mutation.
```

The focused test applies the real planner output to an in-memory target dictionary, reparses it
through `PanelCladdingKeyService`, resolves it through `PanelCladdingRegionService`, and confirms two
two-cell `MPL-001` regions owned independently by `0A` and `0B`. It also proves topology masks are
still written and target offset keys appear in neither deletes nor writes.

### Existing regressions

The following final runs exited `0`:

- `Project_Test/260805_TEST_panel-cladding-match`: Debug and Release;
- `Project_Test/260818_TEST_panel-cladding-match-topology-masks`: Release; and
- `Project_Test/260818_TEST_panel-cladding-topology-persistence`: Release.

The broad match regression now includes the reported parent-value pattern in addition to existing
multi-target, one-cell, topology-mask, target-offset, compatibility, eligibility, and command
identity coverage.

### Builds and identity

Commands:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug --nologo -m:1
dotnet build .\MCP_Rhino.sln -c Release --nologo -m:1
```

Both passed with zero warnings/errors. Direct Debug RHP, direct Release RHP, packaged Release RHP,
and staged Release RHP assembly-identity checks passed. PanelCladdingEditor declares the non-empty
manifest-matching id `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, distinct from MCP_Rhino.

`git diff --check` passed; output contained only the repository's existing Windows line-ending
notices.

## Package and Activation

- Version increment: `1.0.39` → `1.0.40`.
- Bundle build: PASS.
- Rhino process count before installation: `2`.
- The supported installer staged the bundle instead of overwriting the loaded plug-in.
- Packaged/staged RHP SHA-256:
  `3F22978D934CD035AC92D9C8F093BF9F9B09F119CCBA6C82094BEB57DF1BD85E`.
- Bundle and staged hashes match exactly.

After every Rhino process closes, activate the staged build with:

```powershell
powershell -ExecutionPolicy Bypass -File "C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.40-20260819032418826\Installer\Install-PanelCladdingEditor.ps1" -Mode Install -BundleRoot "C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.40-20260819032418826"
```

## Acceptance Criteria Alignment

- Exact reported assignments preserved: passed focused and broad regressions.
- Matched target reconstructs two parent regions: passed.
- Invalid parent cycles fail before target mutation: passed.
- Direct material values remain direct: passed.
- Segment/merge mask transfer remains intact: passed.
- Target H/V offsets remain untouched: passed.
- Debug/Release tests, solution builds, package, hashes, and RHP identity: passed.

## Rollback Verification

The change is isolated to application-level source assignment projection. The live adapter's
all-targets preparation, Undo record, commit, and rollback behavior is unchanged. Reverting the
planner and test changes restores the prior per-cell generic normalization behavior. No panel data
was mutated during automated validation.

## Current Remaining Item

Close all Rhino processes and run the staged installer command above. Then repeat `_PCMatch` with the
reported source configuration and open the target in `_PCEditor`; `1A` and `1B` should show parent
relationships to `0A` and `0B`, not independent material assignments.

## Conclusion

`PCMatch` now copies the complete canonical cladding relationship, including parent-cell ownership,
while retaining mask-only extrusion transfer and target-owned H/V offsets. The fix is regression
tested, packaged as 1.0.40, identity/hash verified, and safely staged for activation.
