# PCCreate command smoke test

This test validates the pure grid-construction plan and the Rhino command contract for
`PCCreate`. The command now persists segment/merge masks and only the surviving logical blank cells inferred from complete on-panel atomic guide spans.

Coverage includes:

- panel-local H/V guide classification;
- bottom-up H offsets and left-right V offsets;
- duplicate, remote, exterior, and diagonal guide handling;
- canonical offset and cell user-text keys;
- authoritative reset of prior grid/type/signature metadata;
- preservation of unrelated panel metadata;
- multi-panel planning and duplicate panel selection;
- failure-before-mutation conditions; and
- the exact panel-first, curve-second Rhino prompt sequence.

Run with:

```powershell
dotnet run --project Project_Test/260818_TEST_panel-cladding-create-command/PanelCladdingCreateSmoke.csproj -c Debug
dotnet run --project Project_Test/260818_TEST_panel-cladding-create-command/PanelCladdingCreateSmoke.csproj -c Release
```
