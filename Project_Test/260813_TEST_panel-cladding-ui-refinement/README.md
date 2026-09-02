# Panel Cladding UI Refinement Smoke

Runs a headless WPF fixture for the ten refinements requested on 2026-08-13. It verifies the compact
panel-information card, exact five-decimal offsets, active-state colors, rounded assignment fields,
red translucent cell selection, inline dimension editing, panel-facing witness marks, redundant-UI
removal, and the continued absence of Eto/WebView dependencies.

Run from the repository root:

```powershell
dotnet run --project Project_Test/260813_TEST_panel-cladding-ui-refinement/PanelCladdingUiRefinementSmoke.csproj -c Release
```

The smoke writes three PNGs in this directory for visual inspection.
