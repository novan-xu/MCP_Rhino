# PanelCladdingEditor package

This plug-in is independent of MCP_Rhino. Its product identity is
`PanelCladdingEditor`; build it with `Build-PanelCladdingEditorPackage.ps1`, then install or validate
it with `Install-PanelCladdingEditor.ps1`.

When Rhino is open, the production installer stages the bundle under LocalAppData instead of
overwriting a loaded plug-in. Close Rhino and run the installer inside the reported staged bundle to
activate it.

The installed RHP and its private dependencies live at
`%LOCALAPPDATA%\PanelCladdingEditor\plugin\<version>`. The installer registers that exact RHP under
the Rhino 8 current-user plug-in key with startup loading enabled. It removes the legacy
`PanelCladdingEditor` and `BayHealthPanelCladdingEditor` roots from Rhino Package Manager discovery.
Do not place the RHP under `%APPDATA%\McNeel\Rhinoceros\packages`; doing so gives Rhino two loaders
for one plug-in identity and produces `ID already in use`.

Only `PanelCladdingEditor.rhp` is shipped for the plug-in assembly. Do not also ship the identical
`.dll`; Rhino interprets both files as plug-ins with the same ID.

The installer writes `%LOCALAPPDATA%\PanelCladdingEditor\install-manifest.json` for hash validation
and ownership-aware uninstall. Run `Uninstall-PanelCladdingEditor.ps1` only while Rhino is closed.
