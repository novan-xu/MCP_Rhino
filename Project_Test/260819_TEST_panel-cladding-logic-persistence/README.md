# Panel Cladding Logic Persistence Smoke

Validates the material-free `CW_2.13_CLADDING_LOGIC` contract and its use by `PCSyncSrf`.

Coverage includes:

- deterministic material-free JSON encoding and strict graph validation;
- saved-owner preservation for unchanged geometry;
- geometry-authoritative owner fallback for surface merges and splits;
- legacy panels with no logic attribute and fail-closed malformed payloads;
- sync backfill plus commit postcondition validation;
- editor Save, `PCCreate`, `PCMatchSrf`, and `PCClear` maintenance of the attribute.

Run from the repository root:

```powershell
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-logic-persistence\PanelCladdingLogicPersistenceSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-logic-persistence\PanelCladdingLogicPersistenceSmoke.csproj -c Release
```
