# Panel Cladding Surface Sync Test

## Automated smoke

Run from the repository root:

```powershell
dotnet run --project .\Project_Test\260807_TEST_panel-cladding-surface-sync\PanelCladdingSurfaceSyncSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260807_TEST_panel-cladding-surface-sync\PanelCladdingSurfaceSyncSmoke.csproj -c Release
```

The smoke verifies exact canonical PID/CID mapping, layer-derived material values, surface-key and
panel-change separation, per-panel mapping/layer error isolation, hidden-object enumeration, mixed
valid/invalid batch continuation, one prepared batch workbook with in-batch signature reuse, later
workbook reuse, model-wide used-type retention, unused managed-sheet/index pruning, unrelated-sheet
preservation, standalone dependencies, service contracts, and unique Rhino command GUID registration.

## Live Rhino fixture

1. Open and save a Rhino document with at least two configured panel Breps and their spawned
   cladding surfaces.
2. Move at least one surface per panel from its existing material leaf layer to another valid leaf
   beneath `03_Material Surfaces (STEP)::<matching family>`; for example move a surface from `GL01` to
   `GL02` below `Surfaces-Glass`.
3. Hide one changed surface (or its material layer), and optionally make another surface's
   `Cladding` user text stale without changing its layer.
4. Select both owning panels before starting the command.
5. Run `_PanelCladdingSyncFromSurfaces` and type or paste the full path to a closed `.xlsx` typology
   workbook at the Rhino command line. If the document already stores a workbook path, press Enter
   to accept it.
6. Confirm the command finds every expected surface, including the hidden surface, by canonical
   `CW_1.01_PID` and `CW_1.02_CID`.
   In particular, `PID_BKT_W3_05_25` must map to CIDs such as `CID_BKT_W3_05_25-0A`.
7. Confirm each matched surface's `Cladding` value equals its current layer leaf and that no surface
   geometry, layer assignment, color, PID, or CID changes.
8. Confirm changed panel cell keys reflect the surface layers and receive new canonical
   `CW_1.10_CLADDING_TYPE` and `Signature` values; unchanged panel configurations retain their type.
9. Open the workbook and confirm every changed type has a typology sheet, while equal signatures
   reuse one type/sheet.
10. Run Rhino Undo once and confirm the Rhino surface/panel/document metadata batch returns to its
    prior state. The external workbook update is intentionally not controlled by Rhino Undo.
11. Close the workbook and rerun without layer changes. Confirm zero panels are updated and no new
    typology type is created. Also confirm any managed type no longer assigned to a panel anywhere
    in the Rhino model is removed from both its worksheet and `_CLADDING_INDEX`, while unrelated
    project worksheets remain.
12. Mixed-batch checks: remove one expected CID from the first panel and duplicate one expected CID
    on another run. Confirm unaffected panels still synchronize and export, the affected panel has no
    partial writes, every issue is printed, and the skipped panel is selected when the command ends.
13. Lock the workbook in Excel and confirm the external commit still fails without leaving partial
    Rhino metadata changes.
