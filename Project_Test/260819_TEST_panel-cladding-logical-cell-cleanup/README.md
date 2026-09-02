# Panel cladding logical-cell cleanup smoke

Verifies that structural Save collapses physical cells hidden by missing extrusion segments into deterministic logical representatives.

Run:

```powershell
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-logical-cell-cleanup\PanelCladdingLogicalCellCleanupSmoke.csproj -c Debug
```

The smoke asserts that a 2x4 physical grid with both C/D horizontal segments removed writes only six logical assignment keys, deletes stale `0D`/`1D`, preserves representative assignments, and remaps parent labels that point to hidden members.
