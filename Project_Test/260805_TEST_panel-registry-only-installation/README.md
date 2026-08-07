# PanelCladdingEditor registry-only installation smoke

Build the 1.0.6 bundle, then run:

```powershell
powershell -ExecutionPolicy Bypass -File `
  .\Project_Test\260805_TEST_panel-registry-only-installation\PanelRegistryOnlyInstallationSmoke.ps1 `
  -BundleRoot .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.6
```

The smoke uses isolated filesystem and HKCU test roots. It verifies migration of both historical
Package Manager names, one registry-owned active RHP, startup registration, hash validation,
rejection of a reintroduced Package Manager copy, and ownership-aware uninstall. Production Rhino
registration and user package files are not modified.
