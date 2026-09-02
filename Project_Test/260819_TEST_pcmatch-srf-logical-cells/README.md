# PCMatchSrf logical-cell smoke

Run:

```powershell
dotnet run --project .\Project_Test\260819_TEST_pcmatch-srf-logical-cells\PCMatchSrfLogicalCellsSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_pcmatch-srf-logical-cells\PCMatchSrfLogicalCellsSmoke.csproj -c Release
```

The smoke verifies the `PCMatchSrf` public command name, cell-code-only compatibility, missing-source-cell blank writes, exact parent graph transfer, preservation of target offsets/masks/type/signature, and fail-closed row/column or parent-reference mismatches.
