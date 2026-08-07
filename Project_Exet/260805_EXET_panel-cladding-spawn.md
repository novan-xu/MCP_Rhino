# Panel cladding spawn execution

## Corresponding plan

- Plan: `Project_Plan/260805_PLAN_panel-cladding-spawn.md`
- Execution date: 2026-08-05

## Related artifacts

- Tests: `Project_Test/260805_TEST_panel-cladding-spawn/`
- Production project: `src/PanelCladdingEditor/PanelCladdingEditor.csproj`
- Commit / PR: none created in this execution

## Execution result / actual delivered scope

- Added the Rhino command `_PanelCladdingSpawn` with its own non-empty GUID.
- The command accepts every preselected Brep or prompts for an unlimited multiple-Brep selection,
  requires a saved active document, composes the existing layout reader plus the new spawn service,
  and reports created CIDs and source-panel count.
- Added a pure spawn-planning service which:
  - uses the existing H/V and cladding-key parser;
  - reads only the canonical `CW_1.01_PID`, `CW_1.05_RELEASE`, and `CW_1.07_WALL_TYPE` keys and
    deliberately ignores aliases;
  - preserves the source metadata key spelling and trimmed value;
  - creates `<PID>-<cell>` CIDs and assigns the same string as object name and canonical
    `CW_1.02_CID` user text;
  - skips blank cladding cells;
  - routes `GL*` to `02_Material Surfaces::Surfaces-Glass::<material>` and provides deterministic
    metal, stone, terracotta, concrete, wood, and `Surfaces-Other` routes;
  - assigns each material leaf layer a muted family color with deterministic HSL lightness variants
    for related codes such as `GL01` and `GL02`.
- Added the live Rhino adapter which:
  - prepares every selected panel before mutation and rejects unsupported projections, duplicate
    CIDs within the batch, and existing document CIDs;
  - rebuilds the same stable panel-local frame used by the editor;
  - trims a duplicate source Brep with local H/V planes for each populated logical cell;
  - rejects zero-piece or multi-piece cell results before document mutation;
  - creates/reuses material layers through `LayerTable.AddPath`, corrects the leaf layer color, and
    makes spawned objects use their layer color;
  - writes object name, CID, cladding, PID, release, and wall type attributes;
  - creates all objects inside one `Spawn Panel Cladding` Rhino Undo record and rolls back objects
    created earlier in the invocation if a later add fails.
- Extended `_PanelCladdingEditorSmoke` to verify that the spawn command is present.
- Added a narrow exclusion for the standalone test executable under the MCP Server's historical
  `Project_Test/**/*.cs` compile glob.

## Deviations from the plan

- The dedicated standalone smoke cannot execute Brep trimming outside Rhino because RhinoCommon's
  native geometry kernel (`rhcommon_c`) only initializes inside Rhino. The automated smoke therefore
  validates the complete pure plan and assembly contract; exact Brep split, document layer/object,
  and Undo behavior remain a documented live Rhino check.
- Full-solution builds use `-m:1`. The solution contains two build shapes of
  `PanelCladdingEditor` (direct `.rhp` and test-host `.dll`) sharing one intermediate directory; a
  parallel build can race over the reference assembly. Serial solution builds avoid that existing
  project-graph collision and completed successfully.

## Problems found and fixed during execution

- The first full solution build compiled the new standalone test `Program.cs` into
  `MCP_Rhino.Server` because the Server intentionally includes historical test partials with a broad
  glob. Added only `Project_Test/260805_TEST_panel-cladding-spawn/**/*.cs` to the existing exclusion
  list; the standalone test continues to build through its own project.
- An attempted parallel validation raced the plug-in's normal and `PanelCladdingTestHost=true`
  builds. Validation was rerun serially and passed.
- An exploratory out-of-process geometry assertion failed at native Rhino kernel initialization,
  not in production code. It was removed from the standalone test rather than recording a misleading
  geometry pass; the live check is retained in the test README.

## Test record

All final commands exited with code `0`.

### Standalone plug-in builds

```powershell
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Debug --nologo
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Release --nologo
```

Both builds completed with `0 Warning(s)` and `0 Error(s)` and produced
`PanelCladdingEditor.rhp`.

### New spawn smoke

```powershell
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-spawn\PanelCladdingSpawnSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-spawn\PanelCladdingSpawnSmoke.csproj -c Release
```

Both configurations reported:

- `[OK] PID-derived CIDs, metadata inheritance, blank-cell handling, and material layers`
- `[OK] deterministic material-family routing, fallback, and related color variants`
- `[OK] canonical metadata is required, aliases are ignored, and invalid layer names fail closed`
- `[OK] standalone PanelCladdingSpawn Rhino command contract and unique GUID`

### Existing editor regression smoke

```powershell
dotnet run --project .\Project_Test\260804_TEST_standalone-panel-cladding-editor\PanelCladdingEditorSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260804_TEST_standalone-panel-cladding-editor\PanelCladdingEditorSmoke.csproj -c Release
```

Both configurations passed all seven existing checkpoints and cleaned their temporary artifacts.

### Full solution integration builds

```powershell
dotnet build .\MCP_Rhino.sln -c Debug --nologo -m:1
dotnet build .\MCP_Rhino.sln -c Release --nologo -m:1
```

Both builds completed with `0 Warning(s)` and `0 Error(s)`.

### Source hygiene

```powershell
git diff --check -- Project_Plan/260805_PLAN_panel-cladding-spawn.md `
  Project_Test/260805_TEST_panel-cladding-spawn src/PanelCladdingEditor `
  src/MCP_Rhino.Server/MCP_Rhino.Server.csproj
```

Result: exit code `0`; only the repository's existing CRLF conversion notices were emitted.

## Acceptance criteria alignment

- Command registration and unique GUID: met.
- Example CID `W3_06_42-0A`: met by automated assertion.
- PID, release-number, and wall-type key/value inheritance: met by automated assertions.
- Required `02_Material Surfaces::Surfaces-Glass::GL01` path: met by automated assertion.
- Same-material layer reuse: met by automated assertion.
- Multi-panel batch service contract: met by automated assertion and compile validation.
- Non-white category colors and distinct GL01/GL02 lightness variants: met by automated assertions.
- Blank cells and invalid/ambiguous input fail-closed behavior: met by automated assertions.
- Exact live cell Breps and one Rhino Undo record: implemented and compile-validated; live Rhino
  fixture verification remains.
- Debug/Release standalone and full-solution builds: met.
- No MCP dependency introduced: met by assembly-reference smoke.

## Rollback verification

- Successful execution is designed as one Rhino Undo record named `Spawn Panel Cladding`.
- A failed object-add sequence deletes objects created earlier in that invocation before returning.
- The command never modifies or deletes the source panel.
- Code rollback remains isolated to the spawn models, interface, planning service, live adapter,
  command, smoke extension, test artifact, and exact Server test-glob exclusion.

## Current remaining items

- In a saved live document, run `_PanelCladdingSpawn` on multiple authored planar or supported
  curved panels and verify cell geometry, colored layer paths, metadata, and one-step Undo.

## Post-install activation (2026-08-05)

After the user closed Rhino, the updated plug-in was packaged and activated through the repository's
registry-only installer.

```powershell
& .\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1
& .\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 `
  -Configuration Release -SkipBuild -PluginPath @( `
    '.\src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.rhp', `
    '.\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.7\Plugin\PanelCladdingEditor.rhp')
& .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.7\Installer\Install-PanelCladdingEditor.ps1 `
  -Mode Install -BundleRoot .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.7
& .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.7\Installer\Install-PanelCladdingEditor.ps1 `
  -Mode Validate -BundleRoot .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.7
```

Results:

- Package build passed with zero warnings/errors.
- Direct PE metadata validation reported PanelCladdingEditor plug-in id
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, matching the manifest and distinct from MCP_Rhino.
- Version `1.0.7` installed at
  `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.7`.
- Registry-only validation passed with `LoadMode=1`, `DirectoryInstall=0`, and
  `IsDotNETPlugIn=1`.
- The installed RHP metadata contains `PanelCladdingSpawnCommand` / `PanelCladdingSpawn`.

## Multi-panel and material-color revision (2026-08-05)

- Changed the service contract from one object id to `IReadOnlyList<Guid>` and changed the command
  selection adapter to consume all valid preselected Breps or prompt with `GetMultiple`.
- All selected panels and their exact cell Breps are prepared before any document mutation. Batch
  and document CID collisions fail before object creation, and a successful batch uses one Undo
  record.
- Added deterministic category-suitable material colors. Numeric variants cycle small HSL lightness
  offsets, so GL01 and GL02 remain related but visibly distinct. Existing leaf layers are corrected
  when used, and spawned objects explicitly use `ColorFromLayer`.
- Re-ran the new smoke and existing editor smoke in Debug and Release, plus full serial Debug and
  Release solution builds. All passed with zero warnings/errors.
- Built package 1.0.9 and verified its assembly GUID directly as
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, distinct from MCP_Rhino.
- Rhino processes 3096 and 42284 were running, so the installer safely staged the verified package
  at `C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.9-20260805202400424` rather than
  replacing the loaded plug-in. Intermediate staged 1.0.8 is superseded by 1.0.9.

## Canonical CID-key revision (2026-08-05)

- Changed spawned CID user text and existing-object duplicate lookup from the `CID` alias to the
  canonical panel key `CW_1.02_CID`.
- The CID value and Rhino object name remain `<PID>-<cell>`, for example `W3_06_42-0A`.
- Added regression assertions that `CW_1.02_CID` is written and `CID` is absent.
- Re-ran the spawn smoke and full solution build in Debug and Release. All commands passed with
  zero warnings/errors.
- Advanced the package version to 1.0.10 and verified the packaged RHP assembly GUID directly as
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`.
- Rhino processes 3096 and 42284 remained active, so version 1.0.10 was staged at
  `C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.10-20260805202647415`. The staged
  1.0.9 package is superseded.

## Version 1.0.10 activation (2026-08-05)

- After Rhino closed, activated the staged 1.0.10 package and ran installer validation successfully.
- The registry-owned RHP is installed at
  `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.10\PanelCladdingEditor.rhp`.
- Registry verification passed with `LoadMode=1`, `IsDotNETPlugIn=1`, and `DirectoryInstall=0`.
- Direct PE metadata verification of the installed RHP reported plug-in id
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, matching the manifest and remaining distinct from
  MCP_Rhino.

## Conclusion

The no-UI panel-cladding spawn capability is implemented and registered as `_PanelCladdingSpawn`.
Its planning, exact canonical metadata, CID, multi-panel batch contract, material routing/colors,
dependency boundary, Debug/Release builds, and prior editor regression surface are verified. The
Version 1.0.10 is installed and validated. Only the documented live Rhino geometry/Undo check
remains.

## Stronger material-color revision (2026-08-07)

Live viewport feedback showed that same-family leaf colors were too subtle.

- Calibrated glass exactly to `GL01 = #598390` and `GL02 = #466873`.
- Added stronger lighter/darker glass variants for later numeric material codes while retaining the
  same cool hue family.
- Darkened the base palettes for metal, stone, terracotta, concrete, wood, and fallback materials
  and increased their alternating HSL lightness separation.
- Kept deterministic, case-insensitive numeric cycling and existing-layer color correction.
- Added exact glass-color assertions and minimum same-family color-distance assertions for glass and
  stone.
- Re-ran spawn, standalone editor, and surface-sync smokes in Debug and Release; all passed.
- Re-ran full solution builds in Debug and Release; both completed with zero warnings/errors.
- Built PanelCladdingEditor 1.0.18 and directly verified package plug-in id
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, matching the manifest and remaining distinct from
  MCP_Rhino.
- Rhino process 42032 was active, so the installer staged version 1.0.18 at
  `C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.18-20260807183637097`.

The stronger deterministic palette is ready. Close every Rhino process to activate version 1.0.18.

## Version 1.0.18 activation (2026-08-07)

After all Rhino processes were closed, the staged palette update was activated successfully.

- Installed RHP: `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.18\PanelCladdingEditor.rhp`.
- Installer validation passed.
- Rhino 8 HKCU registration points to the installed 1.0.18 RHP with `LoadMode=1`,
  `IsDotNETPlugIn=1`, and `DirectoryInstall=0`.
- Independent installed-assembly verification reported PanelCladdingEditor plug-in id
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, matching the manifest and remaining distinct from
  MCP_Rhino plug-in id `7a3fc2f0-24a8-4b79-be58-5a08cfb0d10a`.

Version 1.0.18 is active for the next Rhino launch. Spawned material layers will use the stronger
palette, including `GL01 = #598390` and `GL02 = #466873`.
