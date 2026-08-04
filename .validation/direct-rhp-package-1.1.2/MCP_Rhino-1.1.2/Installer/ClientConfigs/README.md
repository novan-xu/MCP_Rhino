# MCP client entry

Configure an MCP server named `mcp-rhino` as a stdio process whose command is the absolute installed
path `%LOCALAPPDATA%\MCP_Rhino\bin\MCP_Rhino.Router.exe`. Do not start that executable manually: each
MCP client session launches and owns its own Router process.

`Install-McpRhino.ps1 -ConfigureClient -ClientConfigPath <path>` can merge this entry into a JSON MCP
configuration after explicit opt-in. The installer records the previous entry so uninstall can
restore only the setting it owns.
