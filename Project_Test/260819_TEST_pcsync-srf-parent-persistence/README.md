# PCSyncSrf Parent Persistence Smoke

Validates the complete surface-sync path from a spanning cladding surface through the final panel commit request.

- the exact geometry-derived graph persists as `0A=MPL-001`, `1A=0A`;
- signature normalization cannot replace the persisted parent reference;
- every logical cell is required and blank cells use the persisted-blank sentinel;
- postcondition validation rejects a missing or changed parent value.

Run:

```powershell
dotnet run --project Project_Test/260819_TEST_pcsync-srf-parent-persistence/PCSyncSrfParentPersistenceSmoke.csproj -c Debug
```
