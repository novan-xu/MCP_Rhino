# Panel Cladding Surface Sync Execution Report

## Corresponding plan

- Plan: `Project_Plan/260807_PLAN_panel-cladding-surface-sync.md`
- Execution date: 2026-08-07

## Related artifacts

- Tests: `Project_Test/260807_TEST_panel-cladding-surface-sync/`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.16/`
- Installed RHP: `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.16\PanelCladdingEditor.rhp`
- Commit / PR: none created during this execution.

## Execution result / actual scope

Implemented `_PanelCladdingSyncFromSurfaces` with explicit command GUID
`15F873A5-5B00-4232-89DB-78C234E2DBBB`.

- The command accepts one or multiple preselected panel Breps and prompts when none are preselected.
- It asks the user to choose or create a closed `.xlsx` typology workbook.
- Panel ownership uses only canonical `CW_1.01_PID`; surface position uses only canonical
  `CW_1.02_CID` with exact `<PID>-<cell label>` values.
- Live discovery includes spawned material-root surfaces and any expected CID found outside the root,
  so an expected surface moved to an invalid layer produces a layer error instead of being silently
  treated as absent.
- Planning requires unique selected PIDs, one surface per expected cell, matching surface PID,
  expected CID membership, supported panel projection, and an exact
  `02_Material Surfaces::<family>::<material>` hierarchy.
- The uppercase material leaf is authoritative. The family must match the existing spawn routing
  rule for that material code.
- Only stale surface `Cladding` values are rewritten. Surface geometry, PID, CID, layer, and color are
  untouched.
- Panel cells are changed only when their normalized material differs from the layer-derived value.
  Only those panels receive a regenerated canonical `CW_1.10_CLADDING_TYPE` and `Signature`; legacy
  type/signature keys are removed.
- All changed identities and previews are prepared against one temporary workbook. A signature
  created earlier in the same batch is immediately reusable by a later panel, and the final workbook
  is replaced once.
- Rhino attributes and the document workbook path commit inside one Undo record named
  `Sync Panel Cladding From Surfaces`. A later Rhino or workbook failure restores prior object
  attributes and the prior document string where possible.

## Differences from plan

No functional scope changed. The command uses `PanelCladdingTypology.xlsx` as the file-picker default
name rather than first reading the document's stored workbook path in the UI layer. The selected
validated full path is still stored in the document after a changed type batch commits.

The standalone executable TEST folder was added to the existing exact exclusion list in
`src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`; MCP_Rhino.Server intentionally compiles other
`Project_Test/**/*.cs` Rhino smoke hooks, while standalone executables must remain separate.

## Issues found and fixed during construction

No product defects surfaced after the first compile. The implementation compiled immediately, and
the dedicated workflow/batch tests passed in both configurations. The package identity probe was run
before installation and again against the installed RHP.

## Test record

Dedicated surface-sync smoke:

```powershell
dotnet run --project .\Project_Test\260807_TEST_panel-cladding-surface-sync\PanelCladdingSurfaceSyncSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260807_TEST_panel-cladding-surface-sync\PanelCladdingSurfaceSyncSmoke.csproj -c Release
```

Both configurations passed six checkpoints:

- exact PID/CID mapping, layer authority, surface refresh, and panel change detection;
- surface-key-only refresh without unnecessary type regeneration;
- missing, duplicate, unexpected, wrong-PID, invalid-root, family-mismatch, and duplicate-selected-PID
  fail-closed behavior;
- end-to-end orchestration with final panel/surface writes and actual workbook commit;
- one prepared workbook batch with in-batch signature reuse, commit, and later reuse;
- standalone command/services, batch API, dependency boundary, and unique GUID.

Existing regressions passed in Debug and Release:

```powershell
dotnet run --project .\Project_Test\260804_TEST_standalone-panel-cladding-editor\PanelCladdingEditorSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260804_TEST_standalone-panel-cladding-editor\PanelCladdingEditorSmoke.csproj -c Release
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-spawn\PanelCladdingSpawnSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-spawn\PanelCladdingSpawnSmoke.csproj -c Release
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-match\PanelCladdingMatchSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-match\PanelCladdingMatchSmoke.csproj -c Release
dotnet run --project .\Project_Test\260807_TEST_panel-cladding-clear\PanelCladdingClearSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260807_TEST_panel-cladding-clear\PanelCladdingClearSmoke.csproj -c Release
```

Full solution validation:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug -m:1
dotnet build .\MCP_Rhino.sln -c Release -m:1
```

Both completed with zero warnings and zero errors.

Packaging and direct assembly identity verification:

```powershell
& .\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1 -Version 1.0.16
& .\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 `
  -Configuration Release -SkipBuild `
  -PluginPath @(
    '.\src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.rhp',
    '.\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.16\Plugin\PanelCladdingEditor.rhp')
```

The package build passed. The direct probe reported:

- MCP_Rhino: `7a3fc2f0-24a8-4b79-be58-5a08cfb0d10a`;
- PanelCladdingEditor: `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`;
- two products and two distinct non-empty plug-in IDs.

Rhino was closed at deployment time. Version 1.0.16 installed directly, installer validation passed,
the Rhino 8 HKCU registry entry points to the installed RHP with `LoadMode=1`,
`IsDotNETPlugIn=1`, and `DirectoryInstall=0`, and the installed RHP passed the same identity probe.

## Acceptance criteria alignment

- Discoverable command, preselection, multiple panels, and unique GUID: met.
- Canonical PID/CID ownership and exact expected cell mapping: met by automated assertions.
- Missing/duplicate/unexpected/wrong-PID/invalid-layer/unsupported-panel fail-closed checks: met by
  automated assertions and live preflight implementation.
- Surface `Cladding` follows material leaf without layer/geometry changes: met by plan assertions and
  bounded live writes.
- Only changed panel cells regenerate canonical type/signature: met by automated assertions.
- User-selected typology workbook, one prepared batch, equal-signature reuse, and actual Open XML
  commit: met by automated filesystem smoke.
- One Rhino Undo record and in-process rollback: implemented and compile-validated; live fixture
  remains.
- Debug/Release dedicated and regression tests: met.
- Debug/Release full solution builds: met.
- Package and installed-RHP identity verification: met.

## Rollback verification

- All Rhino object attribute edits and the document workbook-path string are grouped in one Undo
  record named `Sync Panel Cladding From Surfaces`.
- A later object-attribute or workbook commit failure restores previously modified objects in reverse
  order and restores the prior document workbook path.
- The final external workbook replacement is one commit and is intentionally outside Rhino Undo.
- No panel or surface geometry is created, deleted, replaced, transformed, or moved between layers.
- Code rollback is isolated to the sync models/interfaces/planner/workflow/live adapter/command,
  batch workbook method, smoke assertion, exact Server test exclusion, package version, and matching
  PLAN/TEST/EXET artifacts.

## Current remaining items

- Restart Rhino to load PanelCladdingEditor 1.0.16.
- Run the live fixture in `Project_Test/260807_TEST_panel-cladding-surface-sync/README.md` to verify
  preselection, file-picker behavior, live attributes, one-step Undo, and the negative mapping checks
  in a saved working document.

## Conclusion

`_PanelCladdingSyncFromSurfaces` is implemented, registered, regression-tested, packaged, installed,
and identity-validated as PanelCladdingEditor 1.0.16. Material surface layers are now an authoritative
editing path back to surface `Cladding`, panel cell/type/signature metadata, and the typology workbook.
Only the documented live Rhino interaction fixture remains.

## Live correction: CID prefix and command-line workbook path (2026-08-07)

Live execution against panel PID `PID_BKT_W3_05_25` showed that version 1.0.16 searched for
`PID_BKT_W3_05_25-0A`, while the canonical spawned surface CID is
`CID_BKT_W3_05_25-0A`.

- Added one shared `BuildSurfaceCid` rule used by both spawn and sync.
- A leading `PID_` is replaced with `CID_` before appending the cell label.
- Plain identifiers retain the prior behavior: `W3_06_42` still produces `W3_06_42-0A`.
- Updated live discovery and pure sync planning to use the shared rule.
- Replaced the Eto file explorer with Rhino `GetLiteralString` command-line input so paths containing
  spaces can be pasted. The stored document workbook path is supplied as the Enter-key default.
- Updated the spawn and surface-sync tests with explicit prefixed and plain-ID assertions.
- Re-ran spawn, sync, standalone editor, match, and clear smokes in Debug and Release; all passed.
- Re-ran full solution builds in Debug and Release; both completed with zero warnings/errors.
- Built PanelCladdingEditor 1.0.17 and directly verified package plug-in id
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, matching the manifest and remaining distinct from
  MCP_Rhino.
- Rhino process 71568 was active, so the installer staged version 1.0.17 at
  `C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.17-20260807172941741`.

The CID lookup and command-line input defects are corrected and regression-verified. Close every
Rhino process to activate version 1.0.17, then repeat the live sync check.

## Version 1.0.17 activation (2026-08-07)

After all Rhino processes were closed, the staged bundle was activated successfully.

- Installed RHP: `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.17\PanelCladdingEditor.rhp`.
- Installer validation passed.
- Rhino 8 HKCU registration points to the installed 1.0.17 RHP with `LoadMode=1`,
  `IsDotNETPlugIn=1`, and `DirectoryInstall=0`.
- Independent installed-assembly verification reported PanelCladdingEditor plug-in id
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, matching the manifest and remaining distinct from
  MCP_Rhino plug-in id `7a3fc2f0-24a8-4b79-be58-5a08cfb0d10a`.

Version 1.0.17 is active for the next Rhino launch. Repeat surface spawn/sync with a production PID
to confirm `PID_BKT_W3_05_25` produces and resolves `CID_BKT_W3_05_25-0A`.
