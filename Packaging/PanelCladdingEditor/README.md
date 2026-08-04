# PanelCladdingEditor package

This package is independent of MCP_Rhino. Its Rhino package identity is
`PanelCladdingEditor`; build it with `Build-PanelCladdingEditorPackage.ps1`,
then install or validate it with `Install-PanelCladdingEditor.ps1`.

When Rhino is open, the default-root installer stages the package under LocalAppData instead of
overwriting a loaded plug-in. Close Rhino and rerun the installer to activate it.

The installed Rhino package uses the required layout: `PanelCladdingEditor/manifest.txt` selects
the active version, and the `.rhp`, dependencies, and `manifest.yml` sit directly inside that
version directory.

Only `PanelCladdingEditor.rhp` is shipped for the plug-in assembly. Do not also ship the identical
`PanelCladdingEditor.dll`; Rhino interprets both files as plug-ins with the same ID.
