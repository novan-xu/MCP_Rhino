# Cladding Surface Coverage Metadata EXET

## Corresponding plan

- Plan: `Project_Plan/260825_PLAN_cladding-surface-coverage-metadata.md`
- Execution date: 2026-08-25

## Related artifacts

- Tests: `Project_Test/260825_TEST_cladding-surface-coverage-metadata/`
- Test record: `Project_Test/260825_TEST_cladding-surface-coverage-metadata/RESULTS.md`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.64/`
- Installed RHP: `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.64\PanelCladdingEditor.rhp`
- Commit / PR: not created in this execution.

## Execution result / actual delivered scope

- Added canonical baked-surface user text `CW_1.03_CLADDING_CELLS` containing the complete logical coverage of each cladding Brep.
- Encodes a single-cell surface as `0A` and a surface owned by `0A` that also covers `1A` as `0A;1A`. The owner is first; remaining labels are uppercase and deterministically ordered.
- `PCSpawnSrf` now writes this key on every baked cladding surface, including single-cell surfaces, so a complete coverage partition can be verified later.
- `PCSyncSrf` reads and validates the complete baked partition before offset planning. A H/V track is protected as a hidden mullion only when the persisted surface coverage proves that the same baked surface owns both adjacent cells across the full track.
- Separate surface coverage does not protect a moved surface boundary. Current surface geometry replaces the old boundary offset, and stale curve evidence at that boundary is suppressed.
- All-legacy surface sets with no coverage key still use the saved panel owner graph for one migration sync, after which canonical coverage is written to the surfaces.
- Mixed, malformed, duplicate, unknown, overlapping, or incomplete coverage fails closed before any Rhino commit request is produced.
- Sync refreshes stale or non-canonical coverage values from geometry even when the other surface metadata is unchanged.
- Bumped, packaged, identity-validated, installed, and registry-validated PanelCladdingEditor version `1.0.64`.

## Differences from the plan

No implementation deviation. The metadata key was deliberately written to every cladding surface, not only merged surfaces, because absence on separate surfaces would make a complete-vs-partial partition impossible to distinguish safely.

## Problems found and fixed during construction

- The earlier offset-role fix depended on the panel owner graph. That graph explains the editor relationship (`1A` has parent `0A`) but does not independently prove which logical cells are physically carried by a particular baked Brep.
- The same owner graph could therefore preserve an obsolete boundary after geometry changed, or fail to distinguish that case from a legitimate hidden mullion inside a merged surface.
- Surface-owned complete coverage resolves the ambiguity: `0A;1A` protects their hidden internal track; separate `0A` and `1A` values make their boundary geometry-authoritative.
- Writing metadata only on merged surfaces was rejected because a partly marked model could not prove that unmarked surfaces were intentionally single-cell rather than legacy or damaged. Complete marking plus fail-closed validation makes the distinction auditable.
- Legacy projects required a controlled migration route. An all-unmarked set uses the existing owner graph once; a mixed set is rejected instead of combining incompatible authorities.

## Test record

Focused Debug and Release commands:

```powershell
dotnet run --project Project_Test/260825_TEST_cladding-surface-coverage-metadata/CladdingSurfaceCoverageMetadataSmoke.csproj -c Debug
dotnet run --project Project_Test/260825_TEST_cladding-surface-coverage-metadata/CladdingSurfaceCoverageMetadataSmoke.csproj -c Release
```

Both exited `0`, covering canonical encoding, spawn persistence, merged-vs-separate offset roles, invalid partition rejection, legacy migration, canonical refresh, and the live repository contract. The exact Level 5 regression returned only `19.625, 105.75, 169.09079`; `21.375` and `107.25` were absent.

Thirteen existing managed regression projects passed, covering spawn, surface sync, structural-grid preservation, parent and logic persistence, offset refresh, offset sync, region handling, extrusion sync, layout reconciliation, split spawn/sync, and spawn boundary ordering. Rhino-native Brep checks retained their documented outside-host skips.

Standalone plugin builds:

```powershell
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Debug --nologo
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Release --nologo
```

Both exited `0` with zero warnings and zero errors. Package `1.0.64` built successfully. Identity validation confirmed the PanelCladdingEditor assembly GUID `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, non-empty and distinct from MCP_Rhino.

The registry-only install and independent validation completed successfully after Rhino exited. Packaged and installed RHP SHA-256 both equal `10943E4C50BE4649324A93D82A85F2464E7887155B78D626926F75EEFCCFFFA3`. HKCU targets the `1.0.64` RHP with `LoadMode=1` and `DirectoryInstall=0`.

## Acceptance criteria alignment

- Spawn writes `CW_1.03_CLADDING_CELLS=0A;1A` for the merged example and `=0A` for a single-cell surface: passed.
- Encoding is deterministic, owner-first, normalized, and duplicate-free: passed.
- Complete merged coverage preserves the hidden mullion even if geometry has no boundary there: passed.
- Separate complete coverage replaces moved boundaries and prevents stale curves from re-adding old values: passed.
- Exact `21.375/107.25` to `19.625/105.75` refresh remains duplicate-free: passed.
- All-legacy surface sets migrate to the canonical key: passed.
- Mixed or invalid partitions fail before commit: passed.
- Coverage-only drift schedules a canonical metadata rewrite: passed.
- Existing managed regressions and Debug/Release plugin builds: passed, subject to documented native-host skips.
- Package identity, current-user installation, hashes, and registry ownership: passed.

## Rollback verification

The feature is isolated to the coverage codec, spawn metadata write, surface-sync read/validation/write path, domain snapshot/plan fields, and metadata-aware offset overload. Reverting those changes and reinstalling the previous RHP restores the prior owner-graph-only behavior. Rhino mutations remain inside the existing single Undo record, and invalid metadata exits before mutation.

## Current remaining items

- Native live verification against the user's Rhino model was not run because no Rhino-native host was invoked and Windows UI automation was not authorized.
- A model already saved with an expanded/corrupted grid cannot be repaired reliably from ambiguous historical offsets alone. It should be restored from a clean backup or explicitly repaired before relying on automatic migration.

## Conclusion

The merged-vs-separate ambiguity is now represented on the baked geometry itself and consumed by `PCSyncSrf`. Version `1.0.64` is regression-covered, built, identity-verified, installed, hash-matched, and registry-validated for the next Rhino launch.
