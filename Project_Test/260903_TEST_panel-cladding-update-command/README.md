# Panel Cladding Update Command Smoke

Validates deterministic create/update/delete reconciliation, duplicate expected-CID rejection,
the `PCUpdate` batch command contract, managed-root/PID/CID mutation boundaries, one Undo record, and
package registration.

Run from the repository root:

```powershell
dotnet run --project .\Project_Test\260903_TEST_panel-cladding-update-command\PanelCladdingUpdateCommandSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260903_TEST_panel-cladding-update-command\PanelCladdingUpdateCommandSmoke.csproj -c Release
```
