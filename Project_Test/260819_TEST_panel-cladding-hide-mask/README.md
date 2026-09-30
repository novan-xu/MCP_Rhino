# Panel Cladding Hide Mask Smoke

Validates the `CW_2.11_HIDE_MASK` contract:

- dimensioned binary hide-mask round-trip and legacy two-mask compatibility;
- hidden atomic segments remain logical cladding dividers;
- hidden atoms are omitted from physical extrusion curve plans;
- fully hidden H/V tracks do not collapse or renumber cells;
- `PCMatchCrv` writes only the nondefault hide mask for the hide-only fixture;
- PCEditor exposes a reversible Hide/Unhide action with a distinct dashed hidden state;
- invoking Hide on the B/C atoms keeps all offsets and logical cell keys unchanged;
- fully hidden merged runs retain their merge state, while ambiguous partially hidden runs fail validation.

Run:

```powershell
dotnet run --project Project_Test/260819_TEST_panel-cladding-hide-mask/PanelCladdingHideMaskSmoke.csproj -c Debug
dotnet run --project Project_Test/260819_TEST_panel-cladding-hide-mask/PanelCladdingHideMaskSmoke.csproj -c Release
```
