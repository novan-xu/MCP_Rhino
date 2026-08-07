# PanelCladdingEditor Registry-Only Installation Plan

## Background

Rhino loads the MCP_Rhino RHP successfully from its registry-only LocalAppData location, but reports
`ID already in use` for `PanelCladdingEditor.rhp` during the Package Manager installation pass. The
installed panel RHP is currently discoverable under
`%APPDATA%\McNeel\Rhinoceros\packages\8.0\BayHealthPanelCladdingEditor`, even though the repository
installer and compiled product identity use `PanelCladdingEditor`. The assembly is mapped into Rhino,
but plug-in and command registration do not complete, so `_PanelCladdingEditor` is unavailable.

## Goal

- Give PanelCladdingEditor one installation owner and one Rhino discovery path.
- Move the RHP and its private dependencies outside Rhino Package Manager discovery.
- Register the exact RHP under HKCU and load it automatically at Rhino startup.
- Remove both known legacy Package Manager roots after a safe migration.
- Validate that only one discoverable PanelCladdingEditor RHP remains and that the plug-in and command
  GUIDs stay unique.

## Architecture ownership

- `Packaging/PanelCladdingEditor/`: build, registry-only install, migration, validation, ownership,
  and uninstall behavior.
- `src/PanelCladdingEditor/PanelCladdingEditorPlugin.cs`: startup load policy only.
- `Project_Guides/MCP_Rhino Architecture.md`: standalone panel deployment contract.
- `Project_Test/260805_TEST_panel-registry-only-installation/`: isolated migration, registry,
  duplicate-discovery, validation, and uninstall regression.

No MCP tool, Router, transport, document routing, cladding business logic, or command name changes are
in scope.

## Key design

1. Install versioned plug-in files under
   `%LOCALAPPDATA%\PanelCladdingEditor\plugin\<version>`.
2. Register plug-in GUID `7C1A4D3B-5E29-4F68-9A72-1D8C6B0F4E35` under the Rhino 8 current-user
   plug-in registry with top-level and detailed `FileName` values, `LoadMode=1`,
   `IsDotNETPlugIn=1`, and `DirectoryInstall=0`.
3. Change the compiled load policy to `PlugInLoadTime.AtStartup`, so the two commands are registered
   before the user invokes them.
4. Treat both `PanelCladdingEditor` and `BayHealthPanelCladdingEditor` Package Manager directories as
   legacy discovery roots and remove them only after the new files and registration validate.
5. Write an installer ownership manifest containing installed hashes, active plug-in path, registry
   root, and migrated legacy roots.
6. Refuse production replacement while Rhino is running; stage the built bundle and report the exact
   follow-up installer command instead.
7. Add an ownership-aware uninstaller that removes only unchanged installer-owned files and registry
   values.
8. Advance the product version from 1.0.5 to 1.0.6.

## Involved files

- `src/PanelCladdingEditor/PanelCladdingEditorPlugin.cs`
- `Packaging/PanelCladdingEditor/Build-PanelCladdingEditorPackage.ps1`
- `Packaging/PanelCladdingEditor/Install-PanelCladdingEditor.ps1`
- `Packaging/PanelCladdingEditor/Uninstall-PanelCladdingEditor.ps1`
- `Packaging/PanelCladdingEditor/package-manifest.json`
- `Packaging/PanelCladdingEditor/README.md`
- `Project_Guides/MCP_Rhino Architecture.md`
- `Project_Test/260805_TEST_panel-registry-only-installation/`
- `Project_Exet/260805_EXET_panel-registry-only-installation.md`

## Usage

1. Build version 1.0.6 with `Build-PanelCladdingEditorPackage.ps1`.
2. Run the installer against the generated bundle.
3. If Rhino is open, close Rhino and run the installer from the reported staged bundle.
4. Restart Rhino; the plug-in loads once from LocalAppData and registers
   `_PanelCladdingEditor` and `_PanelCladdingEditorSmoke`.

## Acceptance criteria

- PanelCladdingEditor Debug and Release builds pass with zero warnings and errors.
- Existing standalone behavioral smoke passes in Debug and Release.
- The package contains a direct `PanelCladdingEditor.rhp`, no self DLL, and a matching deps manifest.
- Isolated installer smoke migrates both legacy package-name variants to one registry-only RHP.
- Installer validation rejects any PanelCladdingEditor RHP left under Rhino Package Manager discovery.
- HKCU registration points to the single LocalAppData RHP with startup loading enabled and package
  directory mode disabled.
- Production installation validates after Rhino closes.
- On the following Rhino launch, no PanelCladdingEditor `ID already in use` error appears and both
  panel commands are registered.

## Risks and rollback plan

- A loaded RHP cannot be safely replaced. Production installation therefore stages while Rhino is
  running and does not mutate the active installation.
- Legacy package roots are moved to an installer-owned recoverable backup before removal from Rhino
  discovery; a failed activation restores the prior roots and registry state.
- Registry ownership checks reject an unrelated product already using the plug-in GUID.
- The uninstaller leaves modified files or externally changed registry values untouched for manual
  review.

## Future extensions

- Add a signed Yak distribution that installs a small bootstrapper rather than placing the RHP itself
  inside Package Manager discovery.
- Add a non-UI Rhino startup harness capable of asserting command registration automatically.
