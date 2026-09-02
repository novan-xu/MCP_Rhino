# Cladding Merge Mark Key Regression

Validates canonical `Merge_Mark` spawn/sync persistence, legacy-key migration, conflict rejection, and removal of `CW_1.03_CLADDING_CELLS` from new baked output.

Run from the repository root:

```powershell
dotnet run --project Project_Test/260825_TEST_cladding-merge-mark-key/CladdingMergeMarkKeySmoke.csproj -c Debug
dotnet run --project Project_Test/260825_TEST_cladding-merge-mark-key/CladdingMergeMarkKeySmoke.csproj -c Release
```
