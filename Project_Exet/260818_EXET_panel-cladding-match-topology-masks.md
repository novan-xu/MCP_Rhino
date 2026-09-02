# Panel Cladding Match Topology Masks Execution

## Corresponding Plan

- Plan: `Project_Plan/260818_PLAN_panel-cladding-match-topology-masks.md`
- Execution date: 2026-08-18

## Related Artifacts

- Focused tests: `Project_Test/260818_TEST_panel-cladding-match-topology-masks/`
- Updated match regression: `Project_Test/260805_TEST_panel-cladding-match/`
- Production implementation:
  `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingMatchPlanningService.cs`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.39/`
- Staged plug-in:
  `%LOCALAPPDATA%/PanelCladdingEditor/staged/1.0.39-20260819031644073/Plugin/PanelCladdingEditor.rhp`
- Commit / PR: none created in this execution.

## Execution Result / Actual Delivered Scope

`PCMatch` now adds exactly two extrusion-topology values to its established cladding transfer:

- `CW_2.05_SEGMENT_MASK`
- `CW_2.06_MERGE_MASK`

The source panel is parsed through `PanelCladdingKeyService`, and its decoded topology is re-encoded
into the canonical versioned payload pair. Every compatible target plan removes any existing values
for those two keys and writes the source pair. A legacy source without stored masks therefore writes
an explicit canonical all-segments-present/no-merges pair instead of leaving the target's prior masks
in place.

No H/V offset key is added to the transfer or cleanup set. Target H/V offset distances remain
target-owned. The existing cell-label comparison is retained, so source and target must have the
same horizontal/vertical track counts before the source masks can be planned for the target. Targets
with the same grid dimensions and different valid offset distances remain compatible. Targets with
different grid dimensions still fail with `PANEL_CLADDING_MATCH_GEOMETRY_MISMATCH` before mutation.

Mask keys alone do not classify a target as an already-configured cladding target. This allows
`PCMatch` to replace pre-existing topology on an otherwise unassigned panel while retaining the
existing configured-target protection for cladding cell, type, and signature values.

## Differences From Plan

The implementation followed the mask-only plan. No live adapter change was needed because
`LivePanelCladdingMatchService` already applies the planner's delete/write collections atomically in
one Rhino Undo record.

## Test Record

### Focused topology-mask transfer smoke

Debug and Release both exited `0` and reported:

```text
[OK] PCMatch copies segment/merge masks and preserves target H/V offsets.
[OK] legacy sources emit canonical complete/unmerged topology masks.
[OK] mask transfer remains fail-closed for different grid dimensions.
```

The smoke proves that a non-default missing-segment/merged-run topology round-trips through the
planned writes, stale valid target masks are scheduled for deletion, target offsets appear in
neither deletes nor writes, mask-only targets remain eligible, unrelated target data is preserved,
legacy source defaults are materialized, and a different target lattice is rejected.

### Existing regressions

- `Project_Test/260805_TEST_panel-cladding-match`: Debug and Release PASS.
- `Project_Test/260818_TEST_panel-cladding-topology-persistence`: Release PASS.

The broad match regression now uses a source with a non-default topology and confirms canonical
mask transfer, stale-mask cleanup, target offset preservation, one-cell default masks, source/target
eligibility, different offset-distance acceptance, and different grid-dimension rejection.

### Builds and plug-in identity

Serial solution builds passed with zero warnings/errors:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug --nologo -m:1
dotnet build .\MCP_Rhino.sln -c Release --nologo -m:1
```

The initial default-parallel Debug solution build encountered the repository's existing shared
PanelCladdingEditor output race: one test-host build requested `PanelCladdingEditor.dll` while a
concurrent plug-in build had renamed the same output to `.rhp`. The prescribed serial rerun passed;
this was not a code or test failure.

Direct Debug RHP, direct Release RHP, packaged Release RHP, and staged Release RHP assembly identity
checks all passed. PanelCladdingEditor declares the non-empty manifest-matching plug-in id
`7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, distinct from MCP_Rhino.

`git diff --check` passed; output contained only the repository's existing Windows line-ending
notices.

## Package and Activation

- Version increment: `1.0.38` → `1.0.39`.
- Bundle build: PASS.
- Packaged/staged RHP SHA-256:
  `F86CF2D7764E65DD78AF5A756013BB2DF5C56F000F11754F22DE53562EF88BBD`.
- Rhino process count before installation: `2`.
- The supported installer staged 1.0.39 instead of overwriting the loaded plug-in.
- Bundle and staged RHP hashes are identical.

After all Rhino processes close, activate the staged build with:

```powershell
powershell -ExecutionPolicy Bypass -File "C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.39-20260819031644073\Installer\Install-PanelCladdingEditor.ps1" -Mode Install -BundleRoot "C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.39-20260819031644073"
```

## Acceptance Criteria Alignment

- Copies source segment mask: passed focused and broad regressions.
- Copies source merge mask: passed focused and broad regressions.
- Replaces stale target masks: passed.
- Does not copy or delete H/V offsets: passed.
- Same-grid/different-distance targets remain compatible: passed.
- Different grid dimensions fail before mutation: passed.
- Legacy source defaults become canonical mask writes: passed.
- Debug/Release tests, builds, package, hashes, and RHP identity: passed.

## Rollback Verification

The production change is isolated to planner write/delete projection. The existing live match service
continues to commit all target attribute changes inside one Rhino Undo record. Reverting the planner
change restores the previous cladding-only transfer behavior; the extra mask keys remain safe opaque
panel attributes.

## Current Remaining Item

Close every Rhino window and run the staged installer command above so Rhino loads version 1.0.39.
Then use `_PCMatch` on one same-grid target with different H/V distances and confirm the target keeps
those distances while the extrusion view reconstructs the source's missing and merged segments.

## Conclusion

`PCMatch` now matches the source panel's segment and merge topology without matching its H/V offset
distances. The change is regression-tested, packaged as 1.0.39, identity-verified, hash-verified, and
staged safely for activation after Rhino closes.
