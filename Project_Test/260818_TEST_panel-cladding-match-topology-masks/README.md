# Panel cladding match topology-mask smoke

This focused smoke verifies that `PCMatchSrf` ignores the two panel-level topology masks
while preserving target-owned H/V divider offsets. It also covers different source/target masks,
sources without masks, unrelated target-data preservation, and fail-closed grid-dimension compatibility.

Run both configurations:

```powershell
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-match-topology-masks\PanelCladdingMatchTopologyMasksSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-match-topology-masks\PanelCladdingMatchTopologyMasksSmoke.csproj -c Release
```

The smoke is planner-level and does not mutate Rhino, the plug-in installation, workbooks, or user
registry state.
