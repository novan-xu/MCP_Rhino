# PCSyncSrf Preserve Structural Grid Smoke

Reproduces the saved BKT condition where a panel contains `V0=22.5` and `1A=0A`, while its spanning cladding surface exposes no vertical boundary and no associated extrusion curve exists.

Validates:

- `PCSyncSrf` preserves stored structural offsets;
- newly inferred surface boundaries are unioned without duplicates;
- `PCSyncCrv` remains authoritative for offset removal;
- the preserved two-column grid reconstructs `1A=0A`;
- the live repository uses the scope-aware offset set for grid construction.

Run:

```powershell
dotnet run --project Project_Test/260819_TEST_pcsync-srf-preserve-structural-grid/PCSyncSrfPreserveStructuralGridSmoke.csproj -c Debug
```
