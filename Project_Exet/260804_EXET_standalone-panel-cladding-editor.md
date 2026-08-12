# Standalone panel cladding editor execution

## Result

The panel cladding editor was separated from `MCP_Rhino.Server` into its own Rhino 8 plug-in,
assembly, GUID, command surface, package, installer, and test project.

## Implemented architecture

- Production project: `src/PanelCladdingEditor/PanelCladdingEditor.csproj`
- Plug-in GUID: `DAD9330F-4C98-49D8-8962-13BC357461D9`
- Commands: `_PanelCladdingEditor` and `_PanelCladdingEditorSmoke`
- Explicit local composition; no Microsoft DI container or MCP service host.
- Direct active-document adapter with document/object pinning, fingerprint checks, Undo support,
  workbook preflight, and rollback on external commit failure.
- Independent package under `Packaging/PanelCladdingEditor`.

## MCP_Rhino removal

- Removed the MCP-hosted editor command and Rhino smoke command.
- Removed panel editor domain, application, Open XML, rendering, live adapter, Eto UI, DI
  registrations, and CLI test hook from `MCP_Rhino.Server`.
- Removed the editor-introduced Open XML, Rhino.UI, and Eto references from the Server project.
- Preserved the earlier PLAN/TEST/EXET as historical records but excluded the superseded smoke
  source from active Server compilation.

## Verification

- Standalone Debug build: passed, zero warnings/errors.
- Standalone Debug smoke: passed all seven checkpoints.
- Standalone Release smoke: passed all seven checkpoints.
- Complete Release solution build: passed, zero warnings/errors.
- Package build: passed.
- Isolated package install and validate: passed.
- `dotnet list reference`: no project-to-project references.
- Production NuGet dependencies: only `DocumentFormat.OpenXml` and `System.Drawing.Common`.
- Packaged DLL/deps binary scan: no `MCP_Rhino`, Router, Transport, MCP SDK, old command, or
  MCP chat strings.
- Server source scan: no retired panel editor command or registration.
- The Grasshopper mullion-attribute script beside `test.3dm` was not modified by this split.

## Installation state

The initial 1.0.0 installer placed the `.rhp` in a nested `Plugin` folder and omitted the package
root `manifest.txt`. Rhino therefore did not discover it and removed the invalid package on startup.
The installer was corrected and regression-tested against the layout of working Rhino packages.

After Rhino was closed, corrected version 1.0.1 was installed and hash-validated at:

`%USERPROFILE%\AppData\Roaming\McNeel\Rhinoceros\packages\8.0\PanelCladdingEditor\1.0.1`

The package root now contains `manifest.txt` selecting `1.0.1`, while the `.rhp`, dependencies, and
`manifest.yml` are directly inside the selected version directory. Isolated installation and real
installation validation both passed. The installed file-name audit found no MCP_Rhino, Router,
Transport, Companion, Bridge, or MCP SDK binary. The plug-in uses `PlugInLoadTime.AtStartup`, so it
loads automatically on the next Rhino startup. No MCP_Rhino process, Router, configuration, or
connection is involved.

### Duplicate plug-in identity correction

Rhino then reported `ID already in use`. Inspection confirmed the 1.0.1 package contained identical
`PanelCladdingEditor.dll` and `PanelCladdingEditor.rhp` files. McNeel documents this exact package
condition as a cause of the error: Rhino discovers both assemblies with the same plug-in GUID.
Packaging version 1.0.2 now ships only `PanelCladdingEditor.rhp`; the builder and installer validation
both fail if the duplicate `.dll` is present. The corrected 1.0.2 layout passed isolated install and
validation. After Rhino closed, 1.0.2 was installed at
`%USERPROFILE%\AppData\Roaming\McNeel\Rhinoceros\packages\8.0\PanelCladdingEditor\1.0.2`.
The root `manifest.txt` selects `1.0.2`; final validation reported `RHP_PRESENT=True` and
`DUPLICATE_DLL_PRESENT=False`.

### Repeated startup-load correction

On the next launch, process-module inspection showed 1.0.2 was already loaded successfully before
Rhino displayed another `ID already in use` dialog. This proved the dialog came from a second load
attempt, not from failure to load the package. Version 1.0.3 changes the plug-in load policy from
`AtStartup` to Rhino's default `WhenNeeded`: the commands remain registered and invoking either
command loads the `.rhp` once. Release smoke and isolated package validation passed. After Rhino
closed, 1.0.3 was installed and validated at
`%USERPROFILE%\AppData\Roaming\McNeel\Rhinoceros\packages\8.0\PanelCladdingEditor\1.0.3`.
The root manifest selects 1.0.3, the `.rhp` exists, and no same-name `.dll` exists. Obsolete 1.0.1
and 1.0.2 directories were moved out of Rhino's discovery tree to the recoverable backup
`%USERPROFILE%\AppData\Local\PanelCladdingEditor\obsolete\20260804-161149`; the package root now
contains only version 1.0.3.

### Direct RHP runtime-identity correction

After 1.0.3, Rhino mapped the assembly but did not register either command. The build was compiling
`PanelCladdingEditor.dll` and mechanically copying it to `.rhp`; its `.deps.json` consequently still
identified the runtime assembly as `PanelCladdingEditor.dll`. Version 1.0.4 compiles directly with
`TargetExt=.rhp`, assigns explicit unique GUIDs to both Rhino command classes, and packages the
dependency manifest from the direct Release build rather than the publish manifest. Builder and
installer validation now require `PanelCladdingEditor.rhp` in `.deps.json` and reject any self DLL.
Debug and Release behavioral smokes passed. After Rhino closed, 1.0.4 was installed and validated at
`%USERPROFILE%\AppData\Roaming\McNeel\Rhinoceros\packages\8.0\PanelCladdingEditor\1.0.4`.
The package root selects and contains only 1.0.4; final checks reported RHP present, DLL absent,
RHP runtime manifest true, and DLL runtime manifest false. Obsolete 1.0.3 was moved to the
recoverable backup `%USERPROFILE%\AppData\Local\PanelCladdingEditor\obsolete\20260804-162126`.

### Clean product identity

Rhino continued reporting `ID already in use` after the binary/runtime-identity corrections, which
isolated the remaining conflict to Rhino's persisted development plug-in identity. Version 1.0.5
uses the clean package identity `PanelCladdingEditor` and plug-in GUID
`7C1A4D3B-5E29-4F68-9A72-1D8C6B0F4E35`; command names, command GUIDs, document keys, and workbook
schema are unchanged. Debug/Release smoke and isolated package validation passed. After Rhino
closed, the complete old `PanelCladdingEditor` package root was moved to the recoverable backup
`%USERPROFILE%\AppData\Local\PanelCladdingEditor\obsolete-identity\20260804-162751` and is no longer
discoverable. Version 1.0.5 was installed and validated at
`%USERPROFILE%\AppData\Roaming\McNeel\Rhinoceros\packages\8.0\PanelCladdingEditor\1.0.5`.
Final checks reported the new RHP present, self DLL absent, active version 1.0.5, and old package
discoverability false.

## Remaining live acceptance

After restart, run `_PanelCladdingEditorSmoke`, then preselect one prepared panel in
`test.3dm` and run `_PanelCladdingEditor`. This live UI check cannot be completed while the new
`.rhp` is not loaded in the current Rhino process.

## Canonical cladding type-key revision (2026-08-05)

- Changed the panel type-code user-text key from `CW_4.00_CLADDING_TYPE` to
  `CW_1.10_CLADDING_TYPE`.
- Kept `CW_4.00_CLADDING_SIGNATURE` unchanged. Its `v1:sha256:<digest>` value fingerprints the
  normalized panel geometry class, dimensions, grid topology, H/V offsets, cell materials, and
  curved depth samples. The workbook index uses the full digest to reuse identical types and avoid
  type-code collisions; the stored panel value provides a traceable configuration identity.
- Added save-time migration: a successful editor save deletes the legacy
  `CW_4.00_CLADDING_TYPE` user string before writing `CW_1.10_CLADDING_TYPE`, preventing both keys
  from remaining on the panel.
- Added regression assertions for the new type key, explicit legacy key, and unchanged signature
  key. Standalone editor and spawn smokes passed in Debug and Release.
- Full serial solution builds passed in Debug and Release with zero warnings/errors.
- Built PanelCladdingEditor 1.0.11 and directly verified its assembly plug-in id as
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, distinct from MCP_Rhino.
- Rhino processes 10132 and 71016 were active, so installation safely staged at
  `C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.11-20260805213516016`.

The schema change is implemented and verified. Version 1.0.11 must be activated after all Rhino
processes close.

### Version 1.0.11 activation

- Activated the staged package after Rhino closed and ran installer validation successfully.
- Installed RHP:
  `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.11\PanelCladdingEditor.rhp`.
- Registry verification passed with `LoadMode=1`, `IsDotNETPlugIn=1`, and `DirectoryInstall=0`.
- Direct metadata verification of the installed RHP reported plug-in id
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, matching the manifest and remaining distinct from
  MCP_Rhino.

Version 1.0.11 is installed and ready to load on the next Rhino start.

## Rhino-only Signature key revision (2026-08-05)

- Superseded the prior signature-key decision and renamed Rhino user text from
  `CW_4.00_CLADDING_SIGNATURE` to the exact key `Signature`.
- Preserved the `v1:sha256:<digest>` value, digest inputs, workbook index, identical-type reuse, and
  collision handling unchanged.
- Successful editor saves now delete both legacy schema keys—`CW_4.00_CLADDING_TYPE` and
  `CW_4.00_CLADDING_SIGNATURE`—before writing `CW_1.10_CLADDING_TYPE` and `Signature`.
- Added key/migration regression assertions. Standalone editor and spawn smokes passed in Debug and
  Release, and full serial solution builds passed in both configurations with zero warnings/errors.
- Built and directly verified PanelCladdingEditor 1.0.12. Rhino was closed, so the package was
  installed and registry-validated at
  `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.12\PanelCladdingEditor.rhp`.
- Registry values are `LoadMode=1`, `IsDotNETPlugIn=1`, and `DirectoryInstall=0`. Direct installed
  RHP metadata reports plug-in id `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, matching the manifest
  and remaining distinct from MCP_Rhino.

Version 1.0.12 is installed and ready for the next Rhino start.

## Cladding-only v2 identity revision (2026-08-07)

- Replaced geometry/grid-position signature schema v1 with cladding-only schema v2.
- The canonical digest payload now contains only ordered logical cell labels and normalized material
  values. Width, height, planar/curved classification, depth samples, units, tolerance, and all H/V
  offsets are excluded.
- New stored signatures use `v2:sha256:<digest>` and new type codes use the neutral
  `<system>-CL-<columns>X<rows>-<digest>` form. Grid counts are derived from logical cells rather
  than offset arrays.
- Existing v1 workbook entries remain historical records and are not silently rewritten. Saving a
  panel with version 1.0.20 creates/assigns its v2 identity.
- Updated the standalone smoke to prove identical materials produce the same type/signature across
  different offset values, dimensions/units, geometry classes, and depth profiles, while a material
  change still changes identity.

Validation completed:

- standalone editor, match, surface-sync, spawn, and clear smokes passed in Debug and Release;
- full serial solution builds passed in Debug and Release with zero warnings/errors;
- PanelCladdingEditor 1.0.20 package build passed;
- packaged and installed RHP identity verification reported plug-in id
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, matching the manifest and remaining distinct from
  MCP_Rhino.

Rhino was closed, so version 1.0.20 installed directly at
`C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.20\PanelCladdingEditor.rhp`.
Registry-only installer validation passed. Version 1.0.20 is active for the next Rhino launch.
