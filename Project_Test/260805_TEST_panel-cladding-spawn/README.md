# Panel cladding spawn smoke

This standalone smoke verifies the pure spawn plan and assembly command contract without requiring
an active Rhino document. It covers PID-derived CID naming under `CW_1.02_CID`; exact use of
`CW_1.01_PID`, `CW_1.05_LOT`, and `CW_1.07_WALL_TYPE` while ignoring aliases; blank-cell handling; exact GL01
layer routing; additional material families/fallback; fail-closed metadata and layer validation;
deterministic category colors and subtle same-family variants; the multi-panel batch service
contract; unique Rhino command GUIDs; and the standalone no-MCP dependency boundary.

Run both configurations:

```powershell
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-spawn\PanelCladdingSpawnSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-spawn\PanelCladdingSpawnSmoke.csproj -c Release
```

Live geometry/Undo verification requires a saved Rhino document containing multiple authored
panels. Preselect two or more panels (or select them at the command prompt), run
`_PCSpawn`, confirm all reported CIDs and colored material layers, then run `_Undo` once
and confirm all spawned objects are removed together.
