# PanelCladdingEditor plug-in registration regression

`Test-PanelCladdingPluginRegistration.ps1` exercises the packaged installer against isolated
filesystem roots and an isolated current-user registry key.

It verifies:

- complete installer-owned registration after install;
- exact startup, managed plug-in, path, and command-list metadata;
- rejection of the shorthand-only state that Rhino 8.33 did not consume;
- rejection of an incomplete command list;
- repair of either incomplete state back to the complete registration.

Run after building the package:

```powershell
powershell -ExecutionPolicy Bypass -File .\Project_Test\260826_TEST_panel-cladding-plugin-registration\Test-PanelCladdingPluginRegistration.ps1 -BundleRoot .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.69
```
