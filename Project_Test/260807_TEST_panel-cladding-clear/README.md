# Panel Cladding Clear Test

## Automated smoke

Run from the repository root:

```powershell
dotnet run --project .\Project_Test\260807_TEST_panel-cladding-clear\PanelCladdingClearSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260807_TEST_panel-cladding-clear\PanelCladdingClearSmoke.csproj -c Release
```

The smoke verifies exact generic cell-key recognition, canonical and legacy type/signature cleanup,
offset and identity preservation, multiple-panel deduplication, unconfigured-panel no-op behavior,
empty-selection rejection, standalone dependencies, the live service contract, and unique command
GUID registration.

## Live Rhino fixture

1. Open and save a Rhino document containing at least two panel Breps.
2. Assign these keys to the first panel: `CW_4.00_CLADDING_0A`,
   `CW_1.10_CLADDING_TYPE`, `Signature`, `CW_2.03_OFFSET_H0`, `CW_1.01_PID`, and one unrelated key.
3. Leave the second panel without cladding assignments but give it an offset and PID.
4. Run `_PanelCladdingClear`, select both Breps, and press Enter.
5. Confirm the first panel loses only the cell/type/signature keys; both panels retain offsets, PID,
   geometry, layer, object name, and unrelated user text.
6. Confirm the command reports one changed panel and the expected number of removed keys.
7. Run Rhino Undo once and confirm every removed key returns.
8. Re-run the command twice. The first run clears the keys; the second reports zero removed keys and
   creates no meaningful attribute change.
