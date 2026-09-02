# Panel Cladding Hide Mask Test Results

Date: 2026-08-19

## Focused smoke

Debug and Release both passed:

```powershell
dotnet run --project Project_Test/260819_TEST_panel-cladding-hide-mask/PanelCladdingHideMaskSmoke.csproj -c Debug
dotnet run --project Project_Test/260819_TEST_panel-cladding-hide-mask/PanelCladdingHideMaskSmoke.csproj -c Release
```

Validated:

- `CW_2.07_HIDE_MASK` binary round-trip;
- legacy segment/merge payloads decode with no hidden atoms;
- missing/hidden overlap and partially hidden merge runs fail closed;
- fully hidden merge runs round-trip without exploding;
- hidden atoms remain separate logical cladding cells;
- a complete hidden H track does not collapse or renumber cells;
- physical extrusion planning omits hidden atoms;
- `PCMatchCrv` writes exactly segment, merge, and hide masks;
- the PCEditor Hide/Unhide interaction keeps the B/C offsets and eight cell keys unchanged;
- hidden atoms render as a dashed, labeled editor state.

## Regression

All 40 Debug panel-cladding, `PCMatch*`, and `PCSync*` smoke projects passed on the final code.

The suite includes create, save scopes, topology persistence, full-track collapse, surface/curve spawn and sync, logical/parent cell matching, UI layout, zoom, extrusion visuals, and installation command-surface coverage.

## Build and package

- `dotnet build MCP_Rhino.sln -c Debug --no-restore -m:1`: passed, 0 warnings / 0 errors.
- `dotnet build MCP_Rhino.sln -c Release --no-restore -m:1`: passed, 0 warnings / 0 errors.
- Package `PanelCladdingEditor-1.0.56`: built successfully.
- Assembly identity validation: passed for both distinct RHP products.
- PanelCladdingEditor plug-in id: `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`.
- Packaged/installed RHP SHA-256: `F3E1F33CE34B66913368E55FA249595B9E49318ABE9F117F1EF69AADE9553C28`.
- Registry-only installation validation: passed.

## Live status

The package is installed for the next Rhino launch. Direct interaction against `PID_BKT_N1_01_07` remains a user-side Rhino verification step because Rhino was closed during installation.
