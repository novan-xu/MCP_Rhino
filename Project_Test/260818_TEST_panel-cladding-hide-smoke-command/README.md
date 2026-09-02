# Panel cladding smoke-command retirement test

This focused smoke verifies that the internal `PCEditorSmoke` command is not part of the production
Rhino command table, package metadata, or package documentation. It also locks the production RHP
to the six supported `PC...` command classes and verifies their command GUIDs remain explicit and
unique.

Run both configurations from the repository root:

```powershell
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-hide-smoke-command\PanelCladdingHideSmokeCommandSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-hide-smoke-command\PanelCladdingHideSmokeCommandSmoke.csproj -c Release
```
