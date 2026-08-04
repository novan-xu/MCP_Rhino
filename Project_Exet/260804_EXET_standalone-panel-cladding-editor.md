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

`C:\Users\nxu\AppData\Roaming\McNeel\Rhinoceros\packages\8.0\PanelCladdingEditor\1.0.1`

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
`C:\Users\nxu\AppData\Roaming\McNeel\Rhinoceros\packages\8.0\PanelCladdingEditor\1.0.2`.
The root `manifest.txt` selects `1.0.2`; final validation reported `RHP_PRESENT=True` and
`DUPLICATE_DLL_PRESENT=False`.

### Repeated startup-load correction

On the next launch, process-module inspection showed 1.0.2 was already loaded successfully before
Rhino displayed another `ID already in use` dialog. This proved the dialog came from a second load
attempt, not from failure to load the package. Version 1.0.3 changes the plug-in load policy from
`AtStartup` to Rhino's default `WhenNeeded`: the commands remain registered and invoking either
command loads the `.rhp` once. Release smoke and isolated package validation passed. After Rhino
closed, 1.0.3 was installed and validated at
`C:\Users\nxu\AppData\Roaming\McNeel\Rhinoceros\packages\8.0\PanelCladdingEditor\1.0.3`.
The root manifest selects 1.0.3, the `.rhp` exists, and no same-name `.dll` exists. Obsolete 1.0.1
and 1.0.2 directories were moved out of Rhino's discovery tree to the recoverable backup
`C:\Users\nxu\AppData\Local\PanelCladdingEditor\obsolete\20260804-161149`; the package root now
contains only version 1.0.3.

### Direct RHP runtime-identity correction

After 1.0.3, Rhino mapped the assembly but did not register either command. The build was compiling
`PanelCladdingEditor.dll` and mechanically copying it to `.rhp`; its `.deps.json` consequently still
identified the runtime assembly as `PanelCladdingEditor.dll`. Version 1.0.4 compiles directly with
`TargetExt=.rhp`, assigns explicit unique GUIDs to both Rhino command classes, and packages the
dependency manifest from the direct Release build rather than the publish manifest. Builder and
installer validation now require `PanelCladdingEditor.rhp` in `.deps.json` and reject any self DLL.
Debug and Release behavioral smokes passed. After Rhino closed, 1.0.4 was installed and validated at
`C:\Users\nxu\AppData\Roaming\McNeel\Rhinoceros\packages\8.0\PanelCladdingEditor\1.0.4`.
The package root selects and contains only 1.0.4; final checks reported RHP present, DLL absent,
RHP runtime manifest true, and DLL runtime manifest false. Obsolete 1.0.3 was moved to the
recoverable backup `C:\Users\nxu\AppData\Local\PanelCladdingEditor\obsolete\20260804-162126`.

### Clean product identity

Rhino continued reporting `ID already in use` after the binary/runtime-identity corrections, which
isolated the remaining conflict to Rhino's persisted development plug-in identity. Version 1.0.5
uses the clean package identity `BayHealthPanelCladdingEditor` and plug-in GUID
`7C1A4D3B-5E29-4F68-9A72-1D8C6B0F4E35`; command names, command GUIDs, document keys, and workbook
schema are unchanged. Debug/Release smoke and isolated package validation passed. After Rhino
closed, the complete old `PanelCladdingEditor` package root was moved to the recoverable backup
`C:\Users\nxu\AppData\Local\PanelCladdingEditor\obsolete-identity\20260804-162751` and is no longer
discoverable. Version 1.0.5 was installed and validated at
`C:\Users\nxu\AppData\Roaming\McNeel\Rhinoceros\packages\8.0\BayHealthPanelCladdingEditor\1.0.5`.
Final checks reported the new RHP present, self DLL absent, active version 1.0.5, and old package
discoverability false.

## Remaining live acceptance

After restart, run `_PanelCladdingEditorSmoke`, then preselect one prepared panel in
`test.3dm` and run `_PanelCladdingEditor`. This live UI check cannot be completed while the new
`.rhp` is not loaded in the current Rhino process.
