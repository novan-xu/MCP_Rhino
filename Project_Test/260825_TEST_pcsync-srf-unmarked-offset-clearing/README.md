# PCSyncSrf Unmarked Offset Clearing Regression

Validates that unmarked cladding surfaces cannot preserve geometry-missing stored offsets, while complete baked merged coverage remains authoritative.

Run from the repository root:

```powershell
dotnet run --project Project_Test/260825_TEST_pcsync-srf-unmarked-offset-clearing/PCSyncSrfUnmarkedOffsetClearingSmoke.csproj -c Debug
dotnet run --project Project_Test/260825_TEST_pcsync-srf-unmarked-offset-clearing/PCSyncSrfUnmarkedOffsetClearingSmoke.csproj -c Release
```
