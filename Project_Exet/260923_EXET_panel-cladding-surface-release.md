# Cladding surface release inheritance execution

## Corresponding plan

[PLAN](../Project_Plan/260923_PLAN_panel-cladding-surface-release.md).
Execution date: 2026-09-23.

## Related artifacts

[TEST and results](../Project_Test/260923_TEST_panel-cladding-surface-release/README.md).
No commit or PR was created. Existing role-CID and curve-release work in the working
tree was preserved.

## Implemented scope

- Live surface snapshots now read `CW_1.05_RELEASE` case-insensitively.
- Surface sync plans carry the owning panel's trimmed release and detect release-only
  drift through `ReleaseChanged` and `MetadataChanged`.
- Existing application filtering consequently includes surfaces whose only stale
  attribute is release, without regenerating the panel's type.
- The live commit removes prior release-key case variants, then writes the canonical
  current value or leaves the key absent when the panel has no release. Writes remain
  inside the existing attribute transaction and Undo/rollback boundary.
- PCSpawnSrf and PCUpdate already inherit the panel release through shared surface
  plans; focused tests now cover merged and single-cell regions in both paths.
- Curve-only scope and per-panel ownership remain intact.

## Deviations

None in implementation scope. Full solution validation required `-m:1` after default
worker-count Debug builds exited unsuccessfully without compiler diagnostics.

## Issues found and fixed

Surface spawn/update plans already copied release metadata, but sync snapshots and
commits omitted it. This allowed an existing surface to retain an obsolete release
after the source panel changed. Reading and writing alone would not repair a
release-only difference because application orchestration filters unchanged plans;
release change detection is therefore included in the surface metadata flag.

Two errors in the new test fixture (a property name and column-key spelling) were
corrected before the successful test runs. No unrelated production changes were
needed.

## Test record

Exact commands and fixture coverage are recorded in the linked TEST folder.

- Focused surface-release suite: Debug and Release, exit 0.
- Existing curve-release, role-CID, spawn, surface-sync, surface-coverage, and update
  command suites: Debug, all exit 0.
- Full solution Debug and Release builds with one worker: exit 0, zero warnings/errors.
- Diff whitespace check: exit 0.

The tests use in-memory fixtures and a capturing repository. They do not prove a live
Rhino attribute commit or Undo operation.

## Acceptance alignment

All source/application acceptance checks pass: merged and single surfaces inherit
release on spawn/update; sync repairs missing/stale/non-normalized values, preserves
leading zeros and arbitrary release text, clears removed source values, and leaves
equal values alone. Only changed surfaces are committed; panel types and curve-only
scope stay stable. Batch tests confirm each panel supplies its own release.

## Rollback verification

No deployment or document mutation occurred. The change is confined to surface
snapshot/plan fields, planning, live attribute plumbing, and test registration.
Existing native transaction rollback and Undo were retained but not exercised in
Rhino. Source rollback should remove only this follow-up's additions, preserving
the pre-existing working tree.

## Remaining items

Live Rhino persistence/Undo verification and deployment remain separate. No package
was rebuilt or installed by this follow-up. The previously staged curve-release
1.0.77 bundle predates these surface-sync changes and must be rebuilt before it can
deliver them.

## Conclusion

Release inheritance for cladding surfaces is implemented in source and validated by
focused/regression tests and Debug/Release builds.
