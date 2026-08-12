# Panel cladding match smoke

This standalone smoke verifies the pure configuration-transfer planner and assembly command
contract without requiring an active Rhino document. It covers multi-target ordering, exact logical
cell mapping, canonical `CW_1.10_CLADDING_TYPE` / `Signature` writes, target H/V preservation, stale
cladding cleanup, identity metadata preservation, one-cell panels, source/target eligibility,
topology mismatch, different-sized/profiled target acceptance with different valid target offsets,
command GUID uniqueness, and the standalone no-MCP dependency boundary.

Run both configurations:

```powershell
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-match\PanelCladdingMatchSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-match\PanelCladdingMatchSmoke.csproj -c Release
```

Live Rhino verification requires a saved document with one configured source panel, two
unconfigured panels of different supported sizes/profiles with the same logical cell labels but
different valid H/V values, and one panel with a different logical cell topology:

1. Run `_PanelCladdingMatch`, select the two compatible targets, then the configured source.
   Confirm the source prompt permits selecting ordinary Brep panel objects.
2. Confirm both targets receive identical cell, type, and `Signature` values while their own H/V
   values, PID, release, wall type, CID, name, layer, geometry, and unrelated user text remain
   unchanged.
3. Confirm stale cladding cell keys and both legacy type/signature keys are absent, while every target
   offset key/value is untouched.
4. Run `_Undo` once and confirm both targets return to their original attributes.
5. Repeat with the different-topology panel included and confirm the command reports
   `PANEL_CLADDING_MATCH_GEOMETRY_MISMATCH` without changing either target.
