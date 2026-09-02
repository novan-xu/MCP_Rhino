# Panel Cladding Sparse Topology Persistence Execution

## Corresponding Plan

- Plan: `Project_Plan/260820_PLAN_panel-cladding-sparse-topology.md`
- Execution date: 2026-08-20

## Associated Artifacts

- Focused test: `Project_Test/260820_TEST_panel-cladding-sparse-topology/`
- Updated regressions:
  - `Project_Test/260819_TEST_panel-cladding-scoped-save/`
  - `Project_Test/260820_TEST_panel-cladding-curve-template-ui/`
  - `Project_Test/260818_TEST_panel-cladding-create-command/`
  - `Project_Test/260818_TEST_panel-cladding-topology-persistence/`
  - `Project_Test/260819_TEST_panel-cladding-curve-topology/`
  - `Project_Test/260819_TEST_panel-cladding-hide-mask/`
- Commit / PR: none; work remains in the user's existing dirty working tree.

## Execution Result / Actual Delivered Scope

### Sparse independent topology masks

- Kept `PanelCladdingKeyService.EncodeTopology` as the unchanged full canonical binary codec. Type
  identity therefore continues to include canonical segment, merge, and hide payloads regardless
  of which attributes are physically present.
- Added `EncodeNonDefaultTopology`, which validates through the canonical codec and emits only:
  - `CW_2.05_SEGMENT_MASK` when at least one segment is missing;
  - `CW_2.06_MERGE_MASK` when at least one merge run exists;
  - `CW_2.07_HIDE_MASK` when at least one present segment is hidden.
- Added sparse-persistence comparison that distinguishes an absent default key from a present blank
  or explicit-default key, allowing authoritative reconciliation to clean old redundant storage.

### Writers and readers

- Updated editor save, `PCCreate`, `PCMatchCrv`, and panel surface-sync commits to delete prior
  topology attributes and write only the nondefault subset.
- Updated surface-sync change detection to schedule old explicit-default or blank masks for cleanup.
- Removed the former `PCSpawnCrv` planning/live precondition that required explicit segment and
  merge attributes. Missing masks now use the decoder's established all-present, all-atomic,
  all-visible defaults.
- Cladding-only save remains scope-safe and does not alter extrusion topology attributes.

### `PCCrvTemplate`

- Added merge-code presence to the planning snapshot and reject the complete selected batch before
  mutation if any panel has a nonblank `CW_2.06_MERGE_MASK`.
- Absent and blank merge values are eligible; a generated nondefault merge replaces a blank key
  with the canonical value.
- A priority/template combination with no mergeable run produces an empty merge write, no panel
  mutation, no Undo entry, and no signature invalidation. This covers one-column H-priority and
  one-row V-priority defaults.
- Existing segment and hide topology remains preserved during eligible template planning.

### Documentation

- Updated the package README to document the template precondition and independent sparse mask
  semantics.
- Migrated older test descriptions that previously required explicit default mask pairs.

## Variance From Plan

- No architecture variance. The implementation followed the planned centralized sparse encoder,
  writer cleanup, default-aware readers, and batch template guard.
- A blank merge attribute is treated as semantically absent for `PCCrvTemplate`, while surface sync
  and an extrusion save will remove it when no merge is required. This retains compatibility with
  documents that contain an empty legacy value without treating it as configured topology.
- Initial closure did not package or install because Rhino was running. After the user confirmed
  Rhino was closed, the verified capability was packaged and installed as `1.0.59`. No live Rhino
  UI automation was performed.

## Problems Found And Fixed During Construction

- The old curve-spawn guard used the physical presence of both segment and merge masks as a proxy
  for configured extrusion topology. That conflicts with sparse defaults, so both the pure planner
  and live adapter were changed to rely on normal topology decoding.
- Surface sync initially compared stored strings to all three full payloads and would have
  continuously reintroduced explicit defaults. Both comparison and commit paths now share sparse
  semantics.
- Several existing tests encoded full payloads as fixtures, which remains supported, but asserted
  that all payloads must be rewritten. Those assertions were narrowed to the semantic nondefault
  keys while retaining full-payload backward-compatibility coverage.
- The inherited topology-persistence test also asserted against the intentionally empty
  `PanelCladdingSaveResult.StoredSignature`. It now verifies the v4 identity through the canonical
  signature service, which is the current source of that identity contract.
- The first packaged-RHP identity invocation passed both file paths through a child PowerShell
  process as one argument and stopped before installation. It made no changes. The probe was rerun
  with a native PowerShell string array, passed, and installation then proceeded.

## Test Record

All commands below completed with exit code `0`.

### Focused sparse topology smoke

```powershell
dotnet run --project .\Project_Test\260820_TEST_panel-cladding-sparse-topology\PanelCladdingSparseTopologySmoke.csproj -c Debug
dotnet run --project .\Project_Test\260820_TEST_panel-cladding-sparse-topology\PanelCladdingSparseTopologySmoke.csproj -c Release
```

Key assertions:

- default topology produces zero topology writes;
- missing-only, merge-only, and hide-only fixtures each produce exactly their corresponding key;
- hide-only Save Extrusions deletes stale explicit segment/merge defaults and writes only `2.07`;
- absent masks decode, match, and spawn as defaults;
- default curve match cleans stale target masks without writing replacements;
- one-column H priority and one-row V priority produce no merge payload/signature invalidation;
- existing nonblank merge code rejects `PCCrvTemplate` planning;
- surface sync uses sparse persistence and type signatures retain full canonical payloads.

### Updated behavior regressions, Debug and Release

```powershell
dotnet run --project .\Project_Test\260820_TEST_panel-cladding-curve-template-ui\PanelCladdingCurveTemplateUiSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260820_TEST_panel-cladding-curve-template-ui\PanelCladdingCurveTemplateUiSmoke.csproj -c Release
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-scoped-save\PanelCladdingScopedSaveSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-scoped-save\PanelCladdingScopedSaveSmoke.csproj -c Release
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-create-command\PanelCladdingCreateSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-create-command\PanelCladdingCreateSmoke.csproj -c Release
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-topology-persistence\PanelCladdingTopologyPersistenceSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-topology-persistence\PanelCladdingTopologyPersistenceSmoke.csproj -c Release
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-curve-topology\PanelCladdingCurveTopologySmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-curve-topology\PanelCladdingCurveTopologySmoke.csproj -c Release
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-hide-mask\PanelCladdingHideMaskSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-hide-mask\PanelCladdingHideMaskSmoke.csproj -c Release
```

Additional Debug regressions passed:

- `260818_TEST_panel-cladding-match-topology-masks`
- `260819_TEST_panel-cladding-surface-sync-structural-grid` (managed assertions pass; native Brep
  case remains an expected non-Rhino-host skip)
- `260819_TEST_pcsync-srf-preserve-structural-grid`
- `260805_TEST_panel-cladding-spawn`

### Debug / Release builds

```powershell
dotnet build .\MCP_Rhino.sln -c Debug --nologo
dotnet build .\MCP_Rhino.sln -c Release --nologo
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Debug --nologo
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Release --nologo
```

Each build completed with `0 Warning(s)` and `0 Error(s)`. Direct RHP outputs:

- `src/PanelCladdingEditor/bin/Debug/net8.0-windows/PanelCladdingEditor.rhp`
- `src/PanelCladdingEditor/bin/Release/net8.0-windows/PanelCladdingEditor.rhp`

### Plug-in assembly identity

```powershell
powershell -ExecutionPolicy Bypass -File .\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Debug -SkipBuild
powershell -ExecutionPolicy Bypass -File .\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Release -SkipBuild
```

Both configurations reported two declared, distinct, non-empty IDs. PanelCladdingEditor remained
`7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, matching its package manifest.

### Package and registry-only installation

After the user confirmed Rhino was closed, the package version advanced from installed `1.0.58`
to `1.0.59` and the following production workflow completed:

```powershell
powershell -ExecutionPolicy Bypass -File .\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1
& .\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Release -PluginPath @($mcpRhp, $panelRhp)
& $installer -Mode Install -BundleRoot $bundleRoot
& $installer -Mode Validate -BundleRoot $bundleRoot
```

Verified state:

- Bundle: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.59/`
- Installed root: `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.59`
- Registry file: installed `PanelCladdingEditor.rhp`
- Registry flags: `LoadMode=1`, `DirectoryInstall=0`, `IsDotNETPlugIn=1`
- Installed/bundle RHP SHA-256:
  `ABBF22019ED168AC555DE14765A7949C2269062506B29E41BC972B80579F7907`
- Package Manager-discovered copies: `0`
- Prior `1.0.58` tree preserved under the installer rollback root.
- Final installer Validate: PASS.

### Diff hygiene

```powershell
git diff --check
```

Exit code `0`; only existing Windows line-ending conversion warnings were printed.

## Acceptance Alignment

- Default and one-row/one-column cases persist no unnecessary masks: PASS.
- Hide-only edit persists only `CW_2.07_HIDE_MASK`: PASS.
- Missing attributes remain valid defaults for parse and curve spawn: PASS.
- Save/create/match/sync converge on sparse topology storage: PASS.
- `PCCrvTemplate` does not overwrite an existing merge code: PASS.
- No-run template operation creates no merge attribute: PASS.
- Full canonical signature identity is unchanged: PASS.
- Regression, build, assembly identity, and diff checks: PASS.
- Package `1.0.59`, registry-only activation, installed hashes, and single-discovery validation:
  PASS.
