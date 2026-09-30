# Panel Cladding Layout Reconciliation TEST

Focused regression coverage for the general layout reconciliation contract:

- coordinate/order-based topology remapping across inserted and moved H/V tracks;
- preservation and expansion of missing, hidden, and merged atoms;
- semantic `PCMatchSrf` matching across different raw grid dimensions;
- rejection when target `CW_2.12_DELETE_MASK` makes a required cladding boundary impossible;
- retirement of canonical and legacy persisted Signature keys;
- an explicit guard against fixture-specific production branches.

Run:

```powershell
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-layout-reconciliation\PanelCladdingLayoutReconciliationSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-layout-reconciliation\PanelCladdingLayoutReconciliationSmoke.csproj -c Release
```
