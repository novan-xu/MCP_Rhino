# Panel cladding match smoke

This standalone smoke verifies the pure configuration-transfer planner and assembly command
contract without requiring an active Rhino document. It covers multi-target ordering, exact H/V and
cell mapping, canonical `CW_1.10_CLADDING_TYPE` / `Signature` writes, stale configuration cleanup,
identity metadata preservation, one-cell panels, source/target eligibility, geometry mismatch,
curved-profile tolerance, command GUID uniqueness, and the standalone no-MCP dependency boundary.

Run both configurations:

```powershell
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-match\PanelCladdingMatchSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-match\PanelCladdingMatchSmoke.csproj -c Release
```

Live Rhino verification requires a saved document with one configured source panel, two
geometrically matching unconfigured copies, and one incompatible panel:

1. Run `_PanelCladdingMatch`, select the two matching targets, then the configured source.
   Confirm the source prompt permits selecting ordinary Brep panel objects.
2. Confirm both targets receive identical H/V, cell, type, and `Signature` values while their PID,
   release, wall type, CID, name, layer, geometry, and unrelated user text remain unchanged.
3. Confirm stale higher-index offset/cell keys and both legacy type/signature keys are absent.
4. Run `_Undo` once and confirm both targets return to their original attributes.
5. Repeat with the incompatible panel included and confirm the command reports
   `PANEL_CLADDING_MATCH_GEOMETRY_MISMATCH` without changing either target.
