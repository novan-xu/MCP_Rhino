# Panel Cladding Surface Sync Structural Grid TEST

Validates the `PCSyncSrf` condition where a structural mullion remains in the panel but one cladding surface spans across that mullion.

The smoke test verifies:

- surface-plus-curve inference preserves the structural `V0` track;
- the live `PCSyncSrf` adapter supplies associated curves to that inference path;
- a spanning cladding still remains one surface region;
- the owner cell receives the material and the covered adjacent cell is reconstructed as `1A=0A`.

Run with:

```powershell
dotnet run --project Project_Test/260819_TEST_panel-cladding-surface-sync-structural-grid/PanelCladdingSurfaceSyncStructuralGridSmoke.csproj -c Debug
dotnet run --project Project_Test/260819_TEST_panel-cladding-surface-sync-structural-grid/PanelCladdingSurfaceSyncStructuralGridSmoke.csproj -c Release
```
