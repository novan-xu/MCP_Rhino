# Panel Cladding Layout Reconciliation Execution Report

## Corresponding plan

- Plan: `Project_Plan/260819_PLAN_panel-cladding-layout-reconciliation.md`
- Execution date: 2026-08-19
- Status: implemented and installed; live command rerun pending Rhino restart

## Related artifacts

- Tests: `Project_Test/260819_TEST_panel-cladding-layout-reconciliation/`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.58/`
- Installed RHP: `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.58\PanelCladdingEditor.rhp`
- Commit / PR: none requested

## Execution scope

Implement the approved behavior as general panel-layout reconciliation. `Untitled 1.3dm` state 1/state 2 is a regression fixture only. Production logic must contain no fixture panel IDs, state values, coordinates, fixed dimensions, or literal curve-index transitions.

The execution includes:

- semantic `PCMatchSrf` feasibility across different raw grids;
- ordered H/V track correspondence for insertions, movement, and authoritative removal;
- topology-mask remapping and associated-curve metadata reindexing;
- unchanged `CW_4.xx` authority with derived material-free `CW_2.08`;
- retirement of persisted `Signature` and `CW_4.00_CLADDING_SIGNATURE` keys;
- focused, regression, build, package, identity, and live-model verification.

## Execution result / actual scope

### General reconciliation

- Added a Rhino-independent ordered axis-alignment service. Exact coordinate matches anchor identity; remaining tracks use deterministic monotonic alignment so insertions and moved tracks do not cause an index-based reset.
- Remapped missing, hidden, and merged atoms from old tracks/bays to their new coordinates. When a new perpendicular track divides an old bay, the old curve group expands across the child bays instead of being discarded.
- Added surface-boundary evidence for inserted tracks. Adjacent cells covered by different edited surfaces require a segment; cells covered by one spanning surface produce a missing segment unless associated curve geometry supplies the boundary.
- Separated surface-derived and curve-derived offset evidence. Surface scope preserves unrepresented stored structural tracks, recognizes moved tracks represented by current curves, and adds new surface boundaries. Curve scope remains authoritative for removal.
- Surface scope now infers associated curve coverage on the reconciled grid and regenerates desired `CRV`, `CID`, and Rhino object names without changing retained geometry or layers.

### Semantic `PCMatchSrf`

- Replaced raw generated-cell-label equality with a feasibility service.
- Resolved source material/parent assignments into logical regions, collapsed row/column tracks that do not separate regions, and searched deterministic order-preserving mappings onto the target grid.
- Target `CW_2.05` missing segments are treated as indivisible connections; a mapping is rejected only when one crosses a required source region boundary.
- Target `CW_2.06`, `CW_2.07`, and numeric offset magnitudes do not decide compatibility and remain unchanged.
- Extra target tracks are absorbed with parent references. Redundant source tracks can map to a smaller raw target grid when all essential boundaries remain representable.
- Equal physical grids retain exact source parent direction when their `CW_2.05` structure agrees; differing `CW_2.05` structures use the semantic mapping so target-visible cells receive the required parents.

### Signature retirement

- Removed every production write of panel user text `Signature` and `CW_4.00_CLADDING_SIGNATURE`.
- Create, editor save (all scopes), curve match, surface match, curve sync, surface sync, and clear recognize and remove legacy signature values on touched panels.
- `CW_1.10_CLADDING_TYPE` remains supported. The existing internal deterministic identity may still support type-code/workbook logic, but it is never persisted as a Rhino Signature user-text key.
- `CW_4.xx_CLADDING_*` remains authoritative and `CW_2.08_CLADDING_LOGIC` remains its material-free derived graph.

- Package version advanced from `1.0.57` to `1.0.58`.

## Deviations from plan

- The example model remains a regression fixture only; the implementation also covers multiple insertions, simultaneous axis changes, and moved retained tracks.
- Rhino was closed at installation time. The original model was not mutated; live command verification awaits the next Rhino restart.

## Problems found and fixed during construction

- The prior surface-only structural-grid safeguard preserved all old offsets but could not distinguish a moved curve-backed track from a new surface boundary. Separate curve and surface inference now supplies that distinction while still preserving offsets with no curve evidence.
- A raw-cell transfer cannot support targets with different grids. Canonical region bands plus target missing-segment constraints provide the broader achievable/not-achievable condition.
- When source `CW_2.05` collapses cells that are visible on the target, omitting those keys loses the source region. The semantic mapper now emits parent references for those target-visible cells.
- The solution builds the panel project twice with different output modes. Parallel MSBuild can race over the shared reference output, so final solution validation uses `-m:1`.

## Test record

See `Project_Test/260819_TEST_panel-cladding-layout-reconciliation/RESULTS.md`.

Focused Debug/Release, twenty related Debug regression suites, serialized Debug/Release solution builds, package build, assembly identity validation, current-user install validation, and installed/package hash comparison passed.

Package/installed RHP SHA-256:

```text
4C082A6006A88812AA0C7560E039127CEA6B33511FC68460F41CAE6261FB7D72
```

## Acceptance alignment

- Broader semantic match condition: passed.
- Numeric offsets and `CW_2.06` differences do not block compatible matches: passed.
- Target `CW_2.05` impossible boundary rejects before mutation: passed.
- General insertion/movement topology reconciliation: passed.
- Partial inserted-track `CW_2.05` inference from surface coverage: passed.
- Retained merged-curve reindex planning: passed.
- Unchanged `CW_4.xx -> CW_2.08` ownership: passed.
- No production Signature user-text writer: passed.
- Debug/Release builds, package, assembly GUID, registry install, and hash: passed.
- Installed live-command rerun: pending Rhino restart.

## Rollback verification

- Reinstall the prior registry-only `1.0.57` package to restore the previous command behavior.
- No bulk model migration occurred. Untouched panels retain legacy Signature values until a supported panel operation touches them.
- The original `Untitled 1.3dm` was inspected read-only and was not modified.

## Current remaining items

- Restart Rhino so it loads installed version `1.0.58`.
- Run `PCSyncSrf` and `PCMatchSrf` on a disposable copy of the example model for final live-command confirmation.

## Conclusion

Panel layout comparison and synchronization no longer depend on equal raw grids or old numeric indices. Matching now asks whether the target's `CW_2.05` topology can represent the source cladding regions; synchronization preserves unaffected extrusion intent while reindexing only the relationships changed by edited geometry. Persisted Signature keys have been retired, and package `1.0.58` is built, identity-verified, installed, and ready for the next Rhino launch.
