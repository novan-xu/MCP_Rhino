# Panel Cladding Type Code Format Execution

## Corresponding Plan

- Plan: `Project_Plan/260820_PLAN_panel-cladding-type-code-format.md`
- Execution date: 2026-08-20

## Associated Artifacts

- Focused test: `Project_Test/260820_TEST_panel-cladding-type-code-format/`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.60/`
- Staged bundle:
  `C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.60-20260820174714197`
- Commit / PR: none; work remains in the user's existing dirty working tree.

## Execution Result / Actual Delivered Scope

- Changed generated cladding type codes from
  `<system>-CL-<columns>X<rows>-<digest>` to
  `<system>-<columns>X<rows>-<digest>`.
- Updated both standard eight-character generation and the length-limited rebuild path.
- Updated collision expansion to accept the new three-part minimum and continue replacing only the
  final digest segment.
- Updated the editor's `PENDING` fallback preview to the same marker-free format.
- Kept system normalization, grid count, v4 canonical payload, full SHA-256 digest, topology and
  material identity inputs, and 31-character limit unchanged.
- Updated current test fixtures and package documentation to use marker-free examples.
- Existing Rhino attributes are not batch-migrated. `Save Cladding`, `Save Both`, `PCSyncSrf`, or
  `PCSyncCrv` recomputes a selected panel's code under normal workflows.

## Variance From Plan

- No production variance.
- Rhino was running at packaging time. The validated `1.0.60` bundle was therefore staged by the
  production installer instead of replacing the loaded `1.0.59` plug-in.

## Problems Found And Fixed During Construction

- The material-catalog smoke still expected the retired Rhino `Signature` write. Production has
  intentionally deleted current/legacy signatures since the Rhino-only persistence migration. The
  stale test assertion and README were corrected to require type-code writing plus signature
  cleanup; no production behavior changed for this issue.

## Test Record

All listed completed commands exited `0` unless explicitly noted as staged behavior.

### Focused type-code format

```powershell
dotnet run --project .\Project_Test\260820_TEST_panel-cladding-type-code-format\PanelCladdingTypeCodeFormatSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260820_TEST_panel-cladding-type-code-format\PanelCladdingTypeCodeFormatSmoke.csproj -c Release
```

Observed codes:

- `WT01-2X2-244FA121`
- `PANEL-2X2-244FA121`
- `WT01-2X2-244FA1215C` (ten-character collision suffix)

### Relevant regressions

Debug and Release passed:

- `260804_TEST_standalone-panel-cladding-editor`
- `260807_TEST_panel-cladding-surface-sync`
- `260813_TEST_panel-cladding-material-catalog`

Additional Debug passes:

- `260805_TEST_panel-cladding-match`
- `260819_TEST_panel-cladding-scoped-save`
- `260820_TEST_panel-cladding-sparse-topology`

### Builds and identity

```powershell
dotnet build .\MCP_Rhino.sln -c Debug --nologo
dotnet build .\MCP_Rhino.sln -c Release --nologo
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Debug --nologo
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Release --nologo
powershell -ExecutionPolicy Bypass -File .\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Debug -SkipBuild
powershell -ExecutionPolicy Bypass -File .\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Release -SkipBuild
```

- Every build: `0 Warning(s)`, `0 Error(s)`.
- PanelCladdingEditor plug-in ID remained
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35` in Debug, Release, and the packaged RHP.

### Package staging

```powershell
& .\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1
& $identityProbe -Configuration Release -PluginPath @($mcpRhp, $panelRhp)
& $installer -Mode Install -BundleRoot $bundleRoot
```

- Package version: `1.0.60`.
- Bundle/staged RHP SHA-256:
  `DEA8F0705463E5ED60259AD4CD3271046020773A98FEEE978D1B5130EEAA5340`.
- The installer detected one running Rhino process and staged without mutating the active install.
- Active installed version remains `1.0.59` until Rhino is closed and the staged installer runs.

### Diff hygiene

```powershell
git diff --check
```

Exit code `0`; existing Windows line-ending conversion warnings only.

## Acceptance Criteria Alignment

- Generated code contains no cladding marker: PASS.
- Visible system/grid/hash structure is retained: PASS.
- Collision expansion is marker-free: PASS.
- Editor pending preview matches production format: PASS.
- Full v4 fingerprint semantics are unchanged: PASS.
- Debug/Release regression, build, and identity checks: PASS.
- Versioned production package: PASS; safely staged because Rhino is running.

## Rollback Verification

- The change is isolated to two production format strings and the minimum-part validation used by
  collision expansion.
- Existing full digests and canonical payloads are unchanged, so reverting the display format does
  not require identity-data conversion.
- The active `1.0.59` installation was not touched during staging.

## Current Legacy Items

- `1.0.60` is staged but not active. Close every Rhino window and run the staged installer, or tell
  Codex once Rhino is closed so activation and Validate can be completed.
- Existing panels keep their old `-CL-` code until a normal cladding save/sync recomputes them.

## Conclusion

Marker-free cladding type-code generation is implemented and fully verified. The release bundle is
ready and staged; only activation after Rhino closes remains.
