# PCSyncSrf Offset Refresh Regression

Validates that `PCSyncSrf` replaces moved cladding-boundary offsets while preserving only proven non-splitting structural tracks.

Run from the repository root:

```powershell
dotnet run --project Project_Test/260825_TEST_pcsync-srf-offset-refresh/PCSyncSrfOffsetRefreshSmoke.csproj -c Debug
dotnet run --project Project_Test/260825_TEST_pcsync-srf-offset-refresh/PCSyncSrfOffsetRefreshSmoke.csproj -c Release
```

The smoke covers the reported `21.375/107.25` to `19.625/105.75` refresh, stale-curve suppression, non-splitting structural preservation, structural curve movement, and unmatched structural-curve insertion.
