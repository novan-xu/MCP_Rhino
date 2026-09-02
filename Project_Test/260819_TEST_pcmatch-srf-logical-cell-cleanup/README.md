# PCMatchSrf Logical Cell Cleanup TEST

Runs a standalone regression for source merge topology that hides physical cells `0B` and `1C`.

```powershell
dotnet run --project .\Project_Test\260819_TEST_pcmatch-srf-logical-cell-cleanup\PCMatchSrfLogicalCellCleanupSmoke.csproj -c Debug
```

The test verifies that PCMatchSrf deletes stale hidden target keys without recreating them, retains surviving blank and parent-cell assignments, and preserves target-owned layout/topology metadata.

## Verified result

- Debug: exit 0
- Release: exit 0
- Hidden target keys `CW_4.00_CLADDING_0B` and `CW_4.01_CLADDING_1C` are deleted and absent after applying the plan.
- Target offsets, segment/merge masks, type/signature metadata, and unrelated user text remain unchanged.
