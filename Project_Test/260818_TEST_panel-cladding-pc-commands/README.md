# Panel cladding PC command-name smoke

This smoke locks the complete Rhino command surface to the shortened `PC` prefix while preserving
the `PanelCladdingEditor` plug-in identity and every command class/GUID.

Expected commands:

- `PCEditor`
- `PCCreate`
- `PCClear`
- `PCCrvTemplate`
- `PCMatchSrf`
- `PCMatchCrv`
- `PCSpawn`
- `PCSync`

Run both configurations from the repository root:

```powershell
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-pc-commands\PanelCladdingPcCommandsSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-pc-commands\PanelCladdingPcCommandsSmoke.csproj -c Release
```
