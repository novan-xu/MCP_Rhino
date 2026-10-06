# Panel Cladding Cell Topology Smoke

Focused managed/WPF regression coverage for `260818_PLAN_panel-cladding-cell-topology`.

The smoke verifies:

- deleting the INT boundary between `0C` and `0D` creates one logical/rendered `0C` cell;
- connected horizontal, vertical, and non-rectangular deleted-boundary groups use stable canonical labels;
- deletion clears the complete affected logical group and undo restores it;
- Add H creates and renumbers rows while unsegmented neighboring bays remain merged;
- Add V creates and renumbers columns while unsegmented neighboring bays remain merged;
- shifted parent references follow the new labels;
- deterministic deletion, Add H, and Add V off-screen WPF renders are non-empty.

The 2026-10-06 owner-boundary follow-up reconstructs the reported 3-column/4-row
layout and inspects the actual WPF dashed drawing segments. It checks spanning
cells, indirect and hidden-member parent references, alternative owner labels,
separate same-material regions, an L-shaped logical cell, deleted interiors,
invalid cycles, and blank assignments. Its seven PNGs are written under
`Project_Test/261006_TEST_pc-editor-owner-boundaries/`.

Run:

```powershell
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-cell-topology\PanelCladdingCellTopologySmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-cell-topology\PanelCladdingCellTopologySmoke.csproj -c Release
```
