# Panel Cladding Clear Execution Report

## Corresponding plan

- Plan: `Project_Plan/260807_PLAN_panel-cladding-clear.md`
- Execution date: 2026-08-07

## Related artifacts

- Tests: `Project_Test/260807_TEST_panel-cladding-clear/`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.15/`
- Commit / PR: none created during this execution.

## Execution result / actual scope

Implemented the standalone Rhino command `_PanelCladdingClear` with explicit command GUID
`1BE39D1F-FE6E-4E82-8A2C-00E91485B71C`.

- The command selects one or multiple owning panel Breps with group selection enabled and subobject
  selection disabled.
- `PanelCladdingClearPlanningService` classifies the exact user-text deletion set before mutation.
- `LivePanelCladdingClearService` validates the saved active document and every selected Brep before
  modifying attributes.
- Actual changes are grouped into one Undo record named `Clear Panel Cladding`.
- Panels with no clearable assignments are reported as selected but skipped as no-ops.
- Successful mutation redraws once. A later attribute failure restores every earlier panel's
  duplicated original attributes before returning failure.
- The standalone editor smoke now verifies that `PanelCladdingClearCommand` is present.

The deletion contract is limited to:

- cell values shaped as `CW_<digits>.<two digits>_CLADDING_<numeric column><alpha row>`;
- `CW_1.10_CLADDING_TYPE` and legacy `CW_4.00_CLADDING_TYPE`;
- `Signature` and legacy `CW_4.00_CLADDING_SIGNATURE`.

Offsets, PID, CID, release, wall type, geometry, layer, object name, and unrelated user text are not
part of the deletion plan.

## Differences from plan

No behavioral scope changed. The standalone executable TEST folder required one exact exclusion in
`src/MCP_Rhino.Server/MCP_Rhino.Server.csproj` because that project intentionally compiles other
`Project_Test/**/*.cs` sources as Rhino smoke hooks. This follows the existing standalone editor,
spawn, match, and assembly-probe exclusions and does not suppress any other test folder.

## Issues found and fixed during construction

The first full Release solution build included the new standalone test executable's `Program.cs`
and generated `obj` sources in MCP_Rhino.Server, producing duplicate assembly attributes and missing
PanelCladdingEditor-reference errors. Added the exact
`Project_Test/260807_TEST_panel-cladding-clear/**/*.cs` exclusion and reran Debug and Release solution
builds successfully.

The package installer's `Validate` mode was also invoked before activation. It correctly reported
that the currently installed ownership manifest was version 1.0.14 rather than 1.0.15; this mode
validates an installed bundle, not a source package. Package integrity was instead checked through
the package build hashes and required direct RHP assembly-identity probe. No product change was
needed.

## Test record

Dedicated tests:

```powershell
dotnet run --project .\Project_Test\260807_TEST_panel-cladding-clear\PanelCladdingClearSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260807_TEST_panel-cladding-clear\PanelCladdingClearSmoke.csproj -c Release
```

Both configurations passed these four checkpoints:

- exact generic cell/type/signature key classification;
- multi-panel planning, duplicate collapse, no-op behavior, and metadata preservation;
- empty-selection fail-closed behavior;
- standalone command/live-service contract and unique command GUID.

Regression tests passed in Debug and Release:

```powershell
dotnet run --project .\Project_Test\260804_TEST_standalone-panel-cladding-editor\PanelCladdingEditorSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260804_TEST_standalone-panel-cladding-editor\PanelCladdingEditorSmoke.csproj -c Release
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-spawn\PanelCladdingSpawnSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-spawn\PanelCladdingSpawnSmoke.csproj -c Release
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-match\PanelCladdingMatchSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-match\PanelCladdingMatchSmoke.csproj -c Release
```

Full solution builds after the narrow test exclusion:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug -m:1
dotnet build .\MCP_Rhino.sln -c Release -m:1
```

Both completed with zero warnings and zero errors.

Packaging and identity verification:

```powershell
& .\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1 -Version 1.0.15
& .\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 `
  -Configuration Release -SkipBuild `
  -PluginPath @(
    '.\src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.rhp',
    '.\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.15\Plugin\PanelCladdingEditor.rhp')
```

The package build passed with zero warnings/errors. Direct metadata verification reported:

- MCP_Rhino: `7a3fc2f0-24a8-4b79-be58-5a08cfb0d10a`;
- PanelCladdingEditor: `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`;
- two products and two distinct non-empty plug-in IDs.

Rhino process 60852 was active, so the installer staged version 1.0.15 at
`C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.15-20260807164805366`.

## Acceptance criteria alignment

- `_PanelCladdingClear` and unique explicit GUID: met.
- One or multiple Brep panels and duplicate collapse: met by command/service contract and automated
  assertions.
- Canonical/legacy cell, type, and signature deletion: met by automated assertions.
- Exact offset, identity, and unrelated metadata preservation: met by automated assertions.
- Invalid/empty selection fail-closed before mutation: met in planning tests and live adapter
  preflight implementation.
- One Undo record, no-op skip, one redraw, and partial rollback: implemented and compile-validated;
  the documented live Rhino fixture remains.
- Debug/Release dedicated and regression smokes: met.
- Debug/Release full solution builds: met.
- Direct package RHP identity verification: met.

## Rollback verification

- A successful batch is contained in one Rhino Undo record named `Clear Panel Cladding`.
- If a later panel's attribute commit fails, the service restores earlier panels in reverse order
  from duplicated original attributes.
- No selected geometry is created, replaced, transformed, or deleted.
- Code rollback is isolated to the new clear models/interface/planner/live adapter/command, key
  classifier, smoke assertion, exact Server test exclusion, package version, and matching
  PLAN/TEST/EXET artifacts.

## Current remaining items

- Close every Rhino window and activate the staged 1.0.15 bundle.
- Run the live fixture in `Project_Test/260807_TEST_panel-cladding-clear/README.md` to verify the
  interactive multiple-selection flow and one-step Rhino Undo against a saved working document.

## Conclusion

`_PanelCladdingClear` is implemented, registered, regression-tested, packaged as
PanelCladdingEditor 1.0.15, and staged for activation. The command removes only assigned cladding
cell/type/signature metadata and intentionally preserves offsets and panel identity. Activation and
the documented live interaction check remain because Rhino is currently running.
