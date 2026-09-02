# PCSyncSrf Unmarked Offset Clearing EXET

## Corresponding plan

- Plan: `Project_Plan/260825_PLAN_pcsync-srf-unmarked-offset-clearing.md`
- Execution date: 2026-08-25

## Related artifacts

- Tests: `Project_Test/260825_TEST_pcsync-srf-unmarked-offset-clearing/`
- Test record: `Project_Test/260825_TEST_pcsync-srf-unmarked-offset-clearing/RESULTS.md`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.65/`
- Installed RHP: `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.65\PanelCladdingEditor.rhp`
- Commit / PR: not created in this execution.

## Execution result / actual delivered scope

- Removed the PCEditor parent-graph fallback from null/unmarked surface-coverage offset classification.
- An unmarked stored H/V track now has no preservation authority. If current cladding surface geometry does not report that coordinate, `PCSyncSrf` removes it.
- Stale associated curves aligned to an unprotected old boundary remain suppressed and cannot re-add the removed value.
- The coverage-blind surface-planning overload now follows the same rule rather than unioning all stored offsets with current geometry.
- A complete valid baked coverage partition remains the sole authority for hidden merged tracks. For example, `0A;1A` can preserve the internal 0A/1A mullion, while separate `0A` and `1A` marks cannot.
- Updated the prior structural-offset regression so its preservation cases must provide explicit merged coverage.
- Bumped, packaged, identity-validated, installed, and registry-validated PanelCladdingEditor version `1.0.65`.

## Differences from the plan

The implementation also corrected the older `ResolveEffectiveOffsets(Surfaces, ...)` compatibility entry point. Although the live `PCSyncSrf` surface branch already uses the metadata-aware overload, leaving the compatibility path as a stored/current union would retain an unsafe secondary contract. It now delegates to coverage-blind clearing behavior.

## Problems found and fixed during construction

- The all-unmarked branch interpreted the saved PCEditor parent graph as proof of a physical merge. That is precisely the ambiguous state the baked coverage key was introduced to eliminate.
- Removing only the live call-site fallback would have left a public coverage-blind planner capable of preserving old offsets. Both entry paths now share the same no-metadata/no-preservation policy.
- Historical regression wording still claimed that surface scope always preserved geometry-missing structural values. The fixture was updated to assert the new authority boundary: only complete baked merge coverage can justify that preservation.
- Parallel Debug regression runs briefly contended for the same WPF markup cache. The affected test was rerun sequentially and passed; no source or build correction was required for that infrastructure collision.

## Test record

Focused Debug and Release commands:

```powershell
dotnet run --project Project_Test/260825_TEST_pcsync-srf-unmarked-offset-clearing/PCSyncSrfUnmarkedOffsetClearingSmoke.csproj -c Debug
dotnet run --project Project_Test/260825_TEST_pcsync-srf-unmarked-offset-clearing/PCSyncSrfUnmarkedOffsetClearingSmoke.csproj -c Release
```

Both exited `0`. Assertions cover misleading parent relationships, geometry-missing unmarked tracks, explicit merged coverage, the coverage-blind overload, stale old curves, and the exact reported values. Stored `21.375` and `107.25` were removed; the final sequence was `19.625, 105.75, 169.09079`.

Ten related managed Debug regression projects passed. They cover baked coverage, original offset refresh, structural-grid behavior, spawn, surface sync, parent/logic persistence, layout reconciliation, and offset sync. Rhino-native Brep checks retained their documented outside-host skips.

Standalone builds:

```powershell
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Debug --nologo
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Release --nologo
```

Both exited `0` with zero warnings and zero errors. Package `1.0.65` built successfully. Assembly identity validation confirmed PanelCladdingEditor GUID `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, non-empty and distinct from MCP_Rhino.

Rhino was exited before installation. Registry-only `Install` and independent `Validate` both succeeded. Packaged and installed SHA-256 both equal `97000EBD103617741FBD06B924DECC6B429CBFE9529B7F3E70B328CDF71B8D67`. HKCU targets the `1.0.65` RHP with `LoadMode=1` and `DirectoryInstall=0`.

## Acceptance criteria alignment

- Unmarked mismatched `21.375` and `107.25` are removed: passed.
- A PCEditor parent relationship alone cannot preserve an old value: passed.
- Current values `19.625` and `105.75` replace the old coordinates: passed.
- Complete `0A;1A` coverage still protects its hidden merged track: passed.
- Complete separate-cell coverage clears moved boundaries: passed through the coverage regression.
- Mixed, overlapping, incomplete, or invalid marked sets still fail before mutation: passed through the coverage regression.
- Existing managed regressions and Debug/Release builds: passed, subject to documented native-host skips.
- Package identity, installation, registry values, and hash equality: passed.

## Rollback verification

The behavioral change is isolated to offset authority selection and test/package documentation. Restoring the former parent-graph and stored/current-union branches and reinstalling version `1.0.64` restores the previous behavior. Rhino sync mutations remain in the existing single Undo record, while invalid marked metadata exits before mutation.

## Current remaining items

- Native live verification against the user's Rhino model was not run because no Rhino-native host was invoked and Windows UI automation was not authorized.
- Legacy unmarked merged surfaces intentionally lose geometry-missing hidden offsets. They require explicit complete baked coverage if those tracks should be retained.

## Conclusion

`PCSyncSrf` no longer treats unmarked geometry or PCEditor parent relationships as permission to keep stale offsets. Version `1.0.65` is regression-covered, built, identity-verified, installed, hash-matched, and registry-validated for the next Rhino launch.
