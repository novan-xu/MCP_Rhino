# Panel Cladding Scoped Save Smoke

Validates absent topology masks as the default `PCSpawnCrv` state, sparse default persistence, and
the editor's three save scopes. Every scope removes all four exact retired cladding
type aliases (including case variants), returns no type code, and preserves unrelated
CAD type metadata. The UI exposes no cladding type preview and retains frame typology.

Run:

```powershell
dotnet run --project Project_Test/260819_TEST_panel-cladding-scoped-save/PanelCladdingScopedSaveSmoke.csproj -c Debug
dotnet run --project Project_Test/260819_TEST_panel-cladding-scoped-save/PanelCladdingScopedSaveSmoke.csproj -c Release
```
