# Panel Cladding Topology Persistence Smoke

Validates compact panel-level topology masks, sparse nondefault structural Save writes,
topology-sensitive v4 type identity, and editor Save/reload restoration.

Run:

```powershell
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-topology-persistence\PanelCladdingTopologyPersistenceSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-topology-persistence\PanelCladdingTopologyPersistenceSmoke.csproj -c Release
```

The smoke uses a stateful managed repository stub and does not mutate Rhino, a workbook, package,
registry entry, or installation.
