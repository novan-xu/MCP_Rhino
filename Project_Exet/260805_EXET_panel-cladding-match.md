# Panel cladding match execution

## Corresponding plan

- Plan: `Project_Plan/260805_PLAN_panel-cladding-match.md`
- Execution date: 2026-08-05

## Related artifacts

- Tests: `Project_Test/260805_TEST_panel-cladding-match/`
- Production project: `src/PanelCladdingEditor/PanelCladdingEditor.csproj`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.13/`
- Commit / PR: none created in this execution

## Execution result / actual delivered scope

- Registered `_PanelCladdingMatch` with explicit command GUID
  `998e28bc-e9b9-420d-a348-98a5f6b9c089`.
- Added a two-stage command-only selection workflow:
  - first prompt accepts multiple target Breps;
  - second prompt accepts exactly one configured source Brep and filters out all target ids.
- Added a pure planning service which:
  - requires canonical source `CW_1.10_CLADDING_TYPE` and `Signature` values;
  - parses the source's continuous `CW_2.03_OFFSET_H<n>` and `CW_2.04_OFFSET_V<n>` keys;
  - requires every logical source cladding cell to be populated;
  - writes normalized canonical cell materials and exact numeric H/V distances to each target;
  - rejects targets carrying nonblank canonical or legacy type/signature values or populated
    cladding cells;
  - removes stale offset/cell/type/signature keys while excluding target identity and unrelated
    metadata from both deletion and writes;
  - rejects unsupported projection, size mismatch, planar/curved mismatch, and curved fixed-depth
    profile mismatch before mutation.
- Extended the live panel repository with a geometry-only match snapshot. It reuses the editor's
  stable local-frame/mesh rules and the projection service's fixed 5 x 5 depth samples without
  depending on the target's existing H/V keys.
- Added a live match adapter which prepares every source/target read and proposed attribute set
  before mutation, applies the complete batch in one `Match Panel Cladding` Undo record, redraws
  once, and restores earlier targets if a later `ModifyAttributes` call fails.
- The command changes only Rhino object attributes. It creates no geometry and does not modify
  target PID, release, wall type, CID, object name, layer, object color, geometry, `Plane`, or
  unrelated user text.
- Extended `_PanelCladdingEditorSmoke` to verify the match command is registered.
- Advanced the standalone package version to 1.0.13.

## Deviations from the plan

- Geometry capture was added to the existing `ILivePanelCladdingRepository` rather than duplicating
  the stable local-frame and mesh logic inside the new live match service. The new live service still
  owns batch planning/mutation and Undo coordination as planned.
- The standalone automated test validates pure planning and assembly contracts. RhinoCommon's native
  document/geometry mutation kernel is only available inside Rhino, so the exact live selection,
  attribute commit, and one-step Undo verification remains the documented Rhino fixture check.
- Full solution builds run serially with `-m:1` because the solution builds the standalone plug-in in
  both direct-RHP and test-host-DLL shapes that share intermediate paths.

## Problems found and fixed during execution

- The MCP Server intentionally compiles historical `Project_Test/**/*.cs` files. The new standalone
  executable test would otherwise be compiled into the Server, so its exact folder was added to the
  existing exclusion list without broadening any other exclusion.
- No production compile or regression failures remained after implementation. The first dedicated
  match smoke passed in Debug, followed by the complete Release and integration validation matrix.

## Test record

All final commands exited with code `0`.

### Dedicated panel-cladding-match smoke

```powershell
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-match\PanelCladdingMatchSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-match\PanelCladdingMatchSmoke.csproj -c Release
```

Both configurations reported:

- `[OK] multi-target transfer, canonical writes, stale cleanup, and identity preservation`
- `[OK] one-cell configuration without divider offsets`
- `[OK] configured-target, source, overlap, and geometry failures are fail-closed`
- `[OK] deterministic curved-profile compatibility within tolerance`
- `[OK] standalone PanelCladdingMatch command/service contract and unique GUID`

### Existing standalone editor regression smoke

```powershell
dotnet run --project .\Project_Test\260804_TEST_standalone-panel-cladding-editor\PanelCladdingEditorSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260804_TEST_standalone-panel-cladding-editor\PanelCladdingEditorSmoke.csproj -c Release
```

Both configurations passed all seven existing checkpoints and cleaned their temporary artifacts.

### Existing panel spawn regression smoke

```powershell
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-spawn\PanelCladdingSpawnSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-spawn\PanelCladdingSpawnSmoke.csproj -c Release
```

Both configurations passed all four existing spawn checkpoints.

### Full solution integration builds

```powershell
dotnet build .\MCP_Rhino.sln -c Debug --nologo -m:1
dotnet build .\MCP_Rhino.sln -c Release --nologo -m:1
```

Both builds completed with `0 Warning(s)` and `0 Error(s)`.

### Source hygiene

```powershell
git diff --check -- Project_Plan/260805_PLAN_panel-cladding-match.md `
  Project_Test/260805_TEST_panel-cladding-match src/PanelCladdingEditor `
  src/MCP_Rhino.Server/MCP_Rhino.Server.csproj `
  Packaging/PanelCladdingEditor/package-manifest.json
```

Result: exit code `0`; only the repository's existing CRLF conversion notices were emitted.

### Package, identity, and installation verification

```powershell
& .\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1
& .\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 `
  -Configuration Release -SkipBuild -PluginPath @( `
    '.\src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.rhp', `
    '.\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.13\Plugin\PanelCladdingEditor.rhp')
& .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.13\Installer\Install-PanelCladdingEditor.ps1 `
  -Mode Install -BundleRoot .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.13
& .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.13\Installer\Install-PanelCladdingEditor.ps1 `
  -Mode Validate -BundleRoot .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.13
```

Results:

- Package build passed with zero warnings/errors.
- Direct package and installed-RHP metadata verification reported PanelCladdingEditor plug-in id
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, matching the manifest and distinct from MCP_Rhino.
- Rhino was closed, so version 1.0.13 installed directly at
  `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.13\PanelCladdingEditor.rhp`.
- Registry-only validation passed with `LoadMode=1`, `IsDotNETPlugIn=1`, and
  `DirectoryInstall=0`.

## Acceptance criteria alignment

- Saved document and no-new-UI command surface: implemented and compile-validated.
- Multiple targets from one configured source: met by planner and service contract assertions.
- Exact H/V, cell, type, and `Signature` transfer: met by automated assertions.
- Target-specific identity/unrelated metadata preservation: met by delete/write-set assertions.
- Configured target, invalid source, source/target overlap, unsupported/mismatched geometry: met by
  fail-closed automated assertions.
- Stale canonical and legacy configuration cleanup: met by automated assertions.
- One Undo record and partial-commit restoration: implemented and compile-validated; live fixture
  check remains.
- Unique command GUID and no MCP dependency: met by assembly assertions.
- Debug/Release dedicated and prior regression smokes: met.
- Debug/Release full solution builds: met.
- Direct RHP identity verification before and after install: met.

## Rollback verification

- A successful match is contained in one Rhino Undo record named `Match Panel Cladding`.
- If a later target attribute commit fails, the service restores every earlier target's original
  duplicated attributes before returning failure.
- The source panel is read-only, and no target geometry is created, replaced, transformed, or
  deleted.
- Code rollback is isolated to the match models/interface/planner/live adapter/command, smoke hook,
  exact Server test exclusion, package version, TEST folder, and this EXET file.

## Current remaining items

- Restart Rhino to load version 1.0.13.
- In a saved live document, execute the fixture steps in
  `Project_Test/260805_TEST_panel-cladding-match/README.md` to confirm the two-stage selection,
  target attributes, one-step Undo, and incompatible-panel zero-mutation behavior.

## Conclusion

The planned `_PanelCladdingMatch` capability is implemented, registered, regression-tested,
packaged, installed, and identity-validated as PanelCladdingEditor 1.0.13. Its exact transfer,
eligibility, geometry compatibility, cleanup, metadata-preservation, and standalone dependency
contracts are verified. Only the documented live Rhino interaction/Undo fixture remains.

## Source-selection correction (2026-08-06)

Live use of version 1.0.13 showed that the second prompt displayed
`Select one configured source panel Brep` but rejected every model object. The source getter already
used Rhino's `ObjectType.Brep` filter; a redundant custom callback additionally required the
callback geometry argument itself to be a `Brep`. Rhino may supply component geometry to that
callback, so valid owning Breps could fail the callback before selection.

- Removed the redundant custom geometry callback.
- Retained Rhino's Brep filter, disabled subobject selection, the owning-object Brep check after
  selection, and source-is-target rejection in both the UI adapter and planning/live services.
- Updated the live TEST instructions to require confirmation that ordinary Brep panels are
  selectable at the source prompt.
- Re-ran the dedicated match, standalone editor, and spawn smokes in Debug and Release. All passed.
- Re-ran full serial solution builds in Debug and Release; both completed with zero warnings/errors.
- Built PanelCladdingEditor 1.0.14 and directly verified package plug-in id
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, matching the manifest and remaining distinct from
  MCP_Rhino.
- Rhino processes 21532, 65920, 69308, and 72372 were active, so the installer staged version
  1.0.14 at `C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.14-20260806195055222`
  rather than replacing the loaded RHP.

The source-selection defect is corrected and regression-verified. Close every Rhino process to
activate version 1.0.14, then repeat the live source-selection check.

## Version 1.0.14 activation (2026-08-06)

After Rhino was closed, the staged bundle was activated and validated successfully.

- Installed RHP: `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.14\PanelCladdingEditor.rhp`
- Rhino 8 current-user registry entry points to the installed 1.0.14 RHP with `LoadMode=1`,
  `IsDotNETPlugIn=1`, and `DirectoryInstall=0`.
- Independent installed-assembly verification reported PanelCladdingEditor plug-in id
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, matching the manifest and remaining distinct from
  MCP_Rhino plug-in id `7a3fc2f0-24a8-4b79-be58-5a08cfb0d10a`.

Version 1.0.14 is active for the next Rhino launch. The remaining validation is the documented live
selection check: run `_PanelCladdingMatch`, select target panel Breps, and confirm an ordinary
configured panel Brep can be selected at the source prompt.

## Shareable ZIP package (2026-08-06)

Created the complete versioned distribution archive at
`Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.14.zip`.

- Re-verified the packaged RHP assembly identity before compression.
- Confirmed the archive opens successfully and contains all 12 expected entries, including the
  installer, uninstaller, RHP, dependency assemblies, and three manifests.
- Archive size: 2,314,267 bytes.
- SHA-256: `DC7CC7E70BDA0D39850B86B2C4CA6358AF53E80D6E834F46F2036A45A4102268`.
