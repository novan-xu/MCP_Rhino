# Panel Cladding Curve Display Colors Regression

This regression verifies the authoritative color mapping (Blue main frame, Purple horizontal
intermediate, DarkGreen vertical intermediate), propagation onto extrusion plans, existing-color
drift detection, and Rhino attribute writes across PCSpawnCrv, PCUpdate, and PCSyncCrv.

Run both configurations:

```powershell
dotnet run --project .\Project_Test\260903_TEST_pc-curve-display-colors\PCCurveDisplayColorsSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260903_TEST_pc-curve-display-colors\PCCurveDisplayColorsSmoke.csproj -c Release
```
