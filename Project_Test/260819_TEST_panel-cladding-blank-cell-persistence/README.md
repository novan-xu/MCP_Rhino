# Panel cladding blank-cell persistence smoke

Run:

```powershell
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-blank-cell-persistence\PanelCladdingBlankCellPersistenceSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-blank-cell-persistence\PanelCladdingBlankCellPersistenceSmoke.csproj -c Release
```

The smoke verifies that PCEditor Save retains every surviving unassigned logical cladding key with a Rhino-storable blank value, that the parser and type identity treat the stored value as empty, and that hidden physical members of merged cells remain obsolete.
