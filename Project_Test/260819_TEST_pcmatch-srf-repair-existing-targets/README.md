# PCMatchSrf Repair Existing Targets TEST

Validates that PCMatchSrf can overwrite a partially configured compatible target, restore a missing parent value such as `1A=0A`, remove stale target-only cell keys, preserve all non-cell attributes, and detect incomplete Rhino attribute commits before reporting success.

Run with:

```powershell
dotnet run --project .\Project_Test\260819_TEST_pcmatch-srf-repair-existing-targets\PCMatchSrfRepairExistingTargetsSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_pcmatch-srf-repair-existing-targets\PCMatchSrfRepairExistingTargetsSmoke.csproj -c Release
```
