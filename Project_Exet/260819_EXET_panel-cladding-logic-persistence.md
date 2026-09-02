# Panel Cladding Logic Persistence Execution Report

## Corresponding plan

- Plan: `Project_Plan/260819_PLAN_panel-cladding-logic-persistence.md`
- Execution date: 2026-08-19

## Associated artifacts

- Focused tests and results: `Project_Test/260819_TEST_panel-cladding-logic-persistence/`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.57/`
- Safely staged installer:
  `C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.57-20260819175439409`
- Activated install:
  `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.57\PanelCladdingEditor.rhp`
- Commit / PR: none requested or created.

## Execution result / actual delivered scope

- Added canonical panel user-text key `CW_2.08_CLADDING_LOGIC`.
- Added `PanelCladdingLogicService` for deterministic JSON encoding, strict decoding, graph
  validation, cycle detection, and conservative saved-owner resolution.
- Material values are replaced by empty owner values; parent-cell tokens are retained. For example,
  `0A=MPL-001, 1A=0A` becomes `{"0A":"","1A":"0A"}`.
- `PCSyncSrf` now reads the saved graph from the panel layout, combines it with current surface
  coverage, and preserves a saved owner only when every covered cell resolves to the same owner and
  that owner remains inside the edited coverage.
- Surface merges with multiple old owners and split pieces that no longer contain the old owner use
  the existing deterministic geometry owner. Current layer paths remain authoritative for material.
- Missing logic retains legacy geometry-only behavior. Malformed nonblank logic skips the affected
  panel before mutation with a `PANEL_CLADDING_SURFACE_SYNC_LOGIC_INVALID` diagnostic.
- Successful `PCSyncSrf` writes and read-back-verifies the new logic together with the exact cell
  graph, so legacy panels are backfilled on first successful sync.
- PCEditor cladding Save, `PCCreate`, and `PCMatchSrf` maintain the derived logic; `PCClear` removes
  it with the cladding assignment set.
- Package metadata and documentation were updated to standalone version `1.0.57`.

## Deviation from plan

- No semantic deviation from the planned encoding or resolution rules.
- The solution build revealed one repository wiring requirement not called out explicitly in the
  plan: standalone test `Program.cs` files must be excluded by exact folder path from
  `MCP_Rhino.Server`'s historical smoke compilation glob. The exact new folder was added.
- Because Rhino was running, installation completed as safe staging rather than active replacement.
  This preserves the currently loaded plug-in until all Rhino processes close.

## Problems found and fixed during construction

- Historical create/match/surface-sync tests initially assumed that every write was a cell key or
  asserted exact counts without a derived logic key. Their assertions were updated to admit exactly
  `CW_2.08_CLADDING_LOGIC` while preserving all prior cell/material/parent checks.
- The parent-persistence read-back fixture initially passed only `CellValues` into validation. It now
  supplies the committed logic key as the live repository does, retaining the original missing and
  replaced parent-value rejection checks.
- The first solution build compiled the new standalone test output into `MCP_Rhino.Server`, causing
  duplicate generated assembly attributes. A narrow test-folder exclusion fixed the integration;
  final Debug and Release solution builds are clean.

## Test record

Detailed output is recorded in
`Project_Test/260819_TEST_panel-cladding-logic-persistence/RESULTS.md`.

Key commands:

```powershell
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-logic-persistence\PanelCladdingLogicPersistenceSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-logic-persistence\PanelCladdingLogicPersistenceSmoke.csproj -c Release
dotnet build .\MCP_Rhino.sln -c Debug
dotnet build .\MCP_Rhino.sln -c Release
powershell -ExecutionPolicy Bypass -File .\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1 -Version 1.0.57
```

Results:

- Focused Debug: exit `0`.
- Focused Release: exit `0`.
- Affected historical Debug regressions: exit `0`.
- Solution Debug: `0 Warning(s)`, `0 Error(s)`.
- Solution Release: `0 Warning(s)`, `0 Error(s)`.
- Package build: exit `0`.
- Packaged RHP identity: explicit GUID
  `7c1a4d3b-5e29-4f68-9A72-1D8C6B0F4E35`, matching the manifest.
- Bundle/staged/installed RHP hash:
  `eb38281dabeb33b32b23e31567947199c77664ce806e2706acea4e0467c2153e`.

## Acceptance criteria alignment

- Material-free payload: passed with exact JSON and negative material-content assertion.
- Unchanged saved owner preservation: passed with saved owner `1A` over coverage `0A,1A`.
- Merge fallback: passed when multiple saved roots became one edited surface.
- Split fallback/preservation: passed for the piece outside the old owner and the piece retaining it.
- Missing logic compatibility: passed and scheduled a canonical backfill.
- Invalid/cyclic/unknown logic: rejected before mutation.
- Sync write and postcondition validation: passed; a changed committed logic value is rejected.
- Save/create/match/clear maintenance: passed.
- Existing parent persistence and structural-grid behavior: passed.
- Package identity and safe deployment: passed. The initial running-Rhino attempt staged safely;
  after Rhino closed, `Install` and `Validate` completed against the registry-owned `1.0.57` root.

## Rollback verification

- Deleting or omitting `CW_2.08_CLADDING_LOGIC` exercises the tested legacy geometry-only path.
- `PCClear` includes the key in its normal assignment cleanup.
- The installer did not overwrite the loaded version while Rhino was open. Closed-Rhino activation
  subsequently installed the identical staged hash under the registry-owned `1.0.57` root.
- Code rollback is isolated to the derived logic service/key, its mutation-path writes, and the
  saved-owner selection branch; existing material and cell attributes remain independently stored.

## Current remaining items

- Reopen Rhino to load `1.0.57`. A live Rhino check can then confirm the attribute in the Object
  Properties user-text table and run
  `PCSyncSrf` against edited production Breps. Console planning/contract coverage is complete; native
  Brep probes remain host-dependent by design.

## Conclusion

The panel now carries a material-independent cladding owner graph, and `PCSyncSrf` uses that graph as
a conservative hint alongside current Rhino geometry and material layers. Geometry remains decisive
for edited splits and merges, legacy panels remain compatible, every cladding mutation path maintains
the derived attribute, and package `1.0.57` is built, identity-verified, and safely installed under
the validated registry-owned root. It is ready for the next Rhino launch.
