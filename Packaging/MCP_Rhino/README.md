# MCP_Rhino current-user package

Build the Release-only bundle:

```powershell
.\Packaging\MCP_Rhino\Build-McpRhinoPackage.ps1
```

Install from the generated `MCP_Rhino-<version>` directory:

```powershell
.\Installer\Install-McpRhino.ps1
```

The installer validates every bundle hash before making changes. Rhino and Router processes must be
closed for an actual current-user upgrade; if they are active, the bundle is staged and no installed
component is replaced. `-Mode Validate` checks an installation and `-Mode Repair` performs a complete
compatible-bundle replacement.

The Release plug-in is installed at
`%APPDATA%\McNeel\Rhinoceros\packages\8.0\MCP_Rhino\<version>` and loads at Rhino startup. External
executables are installed at `%LOCALAPPDATA%\MCP_Rhino\bin`. An MCP client launches
`MCP_Rhino.Router.exe` over stdio for each session; the Router is not a daemon.

Client configuration is opt-in:

```powershell
.\Installer\Install-McpRhino.ps1 -ConfigureClient -ClientConfigPath C:\path\to\mcp-config.json
```

Only the `mcp-rhino` entry is merged, and its previous value is recorded. Uninstall restores that
entry only when it still matches the installer-owned value; unrelated settings and all `.3dm` files
are left untouched.
