# Panel Cladding Offset Sync Execution

## Corresponding Plan

- Plan: `Project_Plan/260812_PLAN_panel-cladding-offset-sync.md`
- Execution date: 2026-08-12

## Related Artifacts

- Focused tests: `Project_Test/260812_TEST_panel-cladding-offset-sync/`
- Extended regressions:
  - `Project_Test/260805_TEST_panel-cladding-spawn/`
  - `Project_Test/260807_TEST_panel-cladding-surface-sync/`
  - `Project_Test/260812_TEST_panel-cladding-regions/`
- Commit / PR: none

## Execution Result / Actual Scope

- Added naked-edge H/V offset inference in the same panel-local frame used by spawn partitioning.
  Duplicate edge coordinates are clustered within model tolerance and panel-perimeter edges are
  excluded.
- Reordered live sync discovery so it first finds eligible STEP-root surfaces by PID, infers a new
  grid, builds atomic cell geometry, and then derives each surface object's covered cells.
- Rebuilt the layout preview, workbook upsert, and v3 type identity from inferred offsets and cells.
- Added `GridChanged` planning state so an offset-only change still writes panel attributes and the
  workbook.
- Extended panel commit writes with H/V offset lists. Commit now removes every old offset and
  cladding-cell key before writing canonical contiguous keys, normalized owner/reference values,
  type code, and signature inside the existing Rhino Undo transaction.
- Changed the sole material root to `03_Material Surfaces (STEP)`. Spawn plans use that root, and
  sync filters Breps by that root before reading PID/CID metadata. Both legacy roots are ignored.
- Advanced the package version from `1.0.21` to `1.0.22`.

## Deviation From Plan

- The focused native Brep probe cannot execute in a standalone `dotnet` host because Rhino native
  geometry libraries are loaded only inside Rhino. It remains an explicit `[SKIP]` outside Rhino;
  the managed planning/key/layer tests execute normally.
- Rhino was open at packaging time. The production installer therefore staged `1.0.22` instead of
  replacing the loaded `1.0.21` installation, as required by the installer contract. After Rhino
  was closed, the staged installer activated and validated `1.0.22`.

## Problems Found And Fixed During Construction

- The original surface-sync sequence built the coverage grid before discovering surfaces, making
  offset inference impossible. The repository read was converted to a two-pass discovery/inference
  flow.
- Replacing offset counts could leave obsolete `V1`, `H1`, or old cladding-cell attributes. Commit
  now clears both key families before canonical writes.
- Reusing version `1.0.21` would have assigned two binaries to one package version. Packaging now
  identifies this build as `1.0.22`.

## Test Record

Focused Debug and Release smoke:

```powershell
dotnet run --project .\Project_Test\260812_TEST_panel-cladding-offset-sync\PanelCladdingOffsetSyncSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260812_TEST_panel-cladding-offset-sync\PanelCladdingOffsetSyncSmoke.csproj -c Release
```

Both runs reported:

```text
[OK] STEP is the exclusive spawn and material-root contract
[OK] inferred offsets produce contiguous H/V and cell keys
[OK] offset-only changes force panel and workbook synchronization
[SKIP] Rhino Brep offset inference requires a running Rhino native host
```

Affected regression suites passed in both Debug and Release:

```powershell
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-spawn\PanelCladdingSpawnSmoke.csproj -c <Debug|Release>
dotnet run --project .\Project_Test\260807_TEST_panel-cladding-surface-sync\PanelCladdingSurfaceSyncSmoke.csproj -c <Debug|Release>
dotnet run --project .\Project_Test\260812_TEST_panel-cladding-regions\PanelCladdingRegionsSmoke.csproj -c <Debug|Release>
dotnet run --project .\Project_Test\260804_TEST_standalone-panel-cladding-editor\PanelCladdingEditorSmoke.csproj -c <Debug|Release>
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-match\PanelCladdingMatchSmoke.csproj -c <Debug|Release>
dotnet run --project .\Project_Test\260807_TEST_panel-cladding-clear\PanelCladdingClearSmoke.csproj -c <Debug|Release>
```

Serial solution and final direct RHP builds passed with 0 warnings and 0 errors:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug -m:1 --nologo
dotnet build .\MCP_Rhino.sln -c Release -m:1 --nologo
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Debug --nologo
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Release --nologo
```

The Debug, Release, and packaged RHP metadata probes all reported assembly-level plug-in GUID
`7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35` with `declared=True`. The `1.0.22` bundle contains no
duplicate `PanelCladdingEditor.dll`; packaged RHP SHA-256 is
`046569dab089039a94745049049bb31a21f975a29e23c1b3f7d0bc08e9028fa4`.

After Rhino was closed, installation and validation completed successfully:

```text
Installed version: 1.0.22
Installed RHP: %LOCALAPPDATA%\PanelCladdingEditor\plugin\1.0.22\PanelCladdingEditor.rhp
Registry mode: LoadMode=1, DirectoryInstall=0
Installed SHA-256: 046569dab089039a94745049049bb31a21f975a29e23c1b3f7d0bc08e9028fa4
Assembly GUID: 7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35, declared=True
```

## Acceptance Alignment

- Canonical inferred H/V key generation and changed-grid write planning: passed.
- Obsolete offset/cell key removal: implemented in the transactional live commit path; final live
  object verification remains pending.
- Merged-boundary removal and partial-boundary inference: implemented and covered by the native
  Brep probe; standalone execution is skipped until run in Rhino.
- Inferred layout propagation through panel writes, preview, workbook, and v3 signature: passed by
  managed workflow and regression smokes.
- STEP-only spawn root and rejection of both legacy roots: passed.
- STEP-root-before-PID/CID discovery filtering: implemented in the live repository and verified by
  code-path inspection/build; final live wrong-layer fixture remains pending.
- Debug/Release builds, regressions, package shape, and assembly identity: passed.

## Rollback Verification

- Source rollback consists of reverting the files listed in the plan and restoring package version
  `1.0.21`.
- `1.0.23` is active. The installer preserved previous active files below
  `%LOCALAPPDATA%\PanelCladdingEditor\rollback`.
- Runtime sync remains one Rhino Undo record and restores prepared Rhino attributes if workbook
  finalization fails.

## Current Remaining Items

- In Rhino, move the test cladding surfaces below `03_Material Surfaces (STEP)`, run
  `_PanelCladdingSyncFromSurfaces`, and verify changed H/V keys, removed obsolete keys, normalized
  owner references, and ignored matching PID/CID objects on legacy or unrelated layers.

## Conclusion

Geometry-derived offset synchronization and exclusive STEP-root discovery are implemented, covered
by managed Debug/Release regressions, packaged, installed, and validated as `1.0.23` with
five-decimal offset precision. Live Rhino acceptance remains required for native Brep edge
inference and final attribute inspection.

## Five-Decimal Precision Revision

The 2026-08-12 follow-up applies one canonical offset precision throughout the capability:

- `PanelCladdingKeyService` rounds offsets to five decimal places with
  `MidpointRounding.AwayFromZero` before extent and monotonic validation.
- Panel attributes, workbook offset metadata, and v3 signature offsets serialize with `0.#####`.
- Numerically equal but noncanonical stored values such as `35.123460` force a sync rewrite to
  `35.12346`.
- Two boundaries that collapse to the same five-decimal coordinate fail with the existing
  non-monotonic-offset diagnostic.
- Package version advanced to `1.0.23`.

Focused Debug and Release smoke runs passed with the additional result:

```text
[OK] offsets round, serialize, and validate at five-decimal precision
```

The assertion covers `35.123456 -> 35.12346`, compact integer serialization, canonical-storage
detection, rounded-boundary collision rejection, and the v3 canonical signature payload. The
standalone editor, region, and surface-sync regression suites also passed in Debug and Release.
Serial Debug/Release solution builds and direct Debug/Release RHP builds completed with 0 warnings
and 0 errors.

The `1.0.23` package contains no duplicate DLL, declares assembly GUID
`7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, and has RHP SHA-256
`e3c18c1ce072df6036525677652a4f074c777868be9603316ff5c2a6eb57cee0`. Rhino was running, so the
installer staged `1.0.23` at
`%LOCALAPPDATA%\PanelCladdingEditor\staged\1.0.23-20260812181109564`. After Rhino was closed, the
staged installer activated `1.0.23`; installer validation passed with `LoadMode=1`,
`DirectoryInstall=0`, and the same packaged SHA-256.

Post-deployment cleanup removed all Git-ignored `.tmp*`, `.validation`, `_validation`, `bin`, `obj`,
test/package `artifacts`, and Python bytecode-cache trees from the repository. The targeted cleanup
preview is empty afterward, no `tmp`/`temp` directories remain, and repository files excluding
`.git` total approximately 5.67 MB. `.mcp.json`, `Runtime_Log/`, and
`Runtime_Test/MCP_rhino_test.3dmbak` were intentionally preserved.
