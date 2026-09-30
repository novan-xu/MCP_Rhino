# Curve release inheritance execution

## Corresponding plan

[PLAN](../Project_Plan/260923_PLAN_panel-cladding-curve-release.md).
Execution date: 2026-09-23.

## Related artifacts

[TEST and results](../Project_Test/260923_TEST_panel-cladding-curve-release/README.md).
No commit or PR was created. Previous role-CID work remains in the same working tree.

## Implemented scope

- Extrusion plans accept the source release string and write canonical
  `CW_1.05_RELEASE` on all frame, intermediate, and merged curves when populated.
- Combined spawn and the live curve-only generator pass the panel's release. PCUpdate
  reuses this same generator for both existing and new curve dependencies.
- Live sync reads existing curve release metadata; both sync planner branches compare
  it with the panel's normalized release and include release-only drift in mutation plans.
- Sync removes case variants of the old key, then writes the canonical current release,
  or leaves it absent when the panel has no release. Existing Undo/rollback paths apply.
- PID, role CID suffixes, surface metadata, curve topology, colors, and geometry rules
  are unchanged by this follow-up.
- Package version advanced to 1.0.77 and a fresh bundle was built under
  `Packaging/PanelCladdingEditor/artifacts/curve-release-1.0.77/PanelCladdingEditor-1.0.77`.

## Deviations

None. Missing release metadata remains non-blocking for curve-only bake, as before.

## Issues found and fixed

Curves previously received PID/CID/CRV and extrusion assignment metadata but no release.
Adding a write only at bake time would leave existing curves stale; sync change detection
and commits now use the panel's value as well. PCUpdate already rebuilds attributes from
the shared curve plan, so no separate update-specific metadata writer is necessary.

## Test record

Commands and outputs are recorded in TEST. Focused Debug/Release runs and four affected
regression suites passed. Full solution Debug/Release builds passed with zero warnings
and errors. Publish/direct Release rebuild and pre/post-package compiled GUID gates
passed. Diff whitespace check passed.

## Acceptance alignment

All curve kinds inherit the release as text, including leading zeros. Case-insensitive
canonical-key reads and trimmed values match the existing surface convention. Missing,
stale, whitespace-only, and unchanged values are covered. Sync clears obsolete release
metadata when it is removed from the panel. Role suffix regressions remain passing.

## Rollback verification

No new mutation transaction was introduced. Source rollback is limited to the release
argument, snapshot/plan fields, and their plumbing. Native failure rollback/Undo was
not executed. Installed 1.0.76 remains the prior product until activation succeeds.

## Remaining items

Package 1.0.77 is built and ready. Two Rhino sessions were open; the user was asked to
save/close them before installation. The independently verified same-user WMI host
installation route from the prior update is prepared for reuse after fresh preflight.
Live functional release-inheritance verification remains pending.

## Conclusion

Release inheritance is implemented and tested in source and packaged for installation.
