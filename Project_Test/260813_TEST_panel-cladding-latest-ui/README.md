# Panel Cladding Latest UI Test

This focused smoke validates the corrected WPF implementation against the actual newest handoff,
`Design/PanelCladdingEditor/Panel-Cladding-Editor.zip!/panel-cladding-editor.html`.

## Run

```powershell
dotnet run --project .\Project_Test\260813_TEST_panel-cladding-latest-ui\PanelCladdingLatestUiSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260813_TEST_panel-cladding-latest-ui\PanelCladdingLatestUiSmoke.csproj -c Release
```

The smoke uses a production WPF window with a stub live repository. It asserts the non-web window
architecture, enabled cladding/extrusion views, structural action shelf, extrusion topology and
merge/undo behavior, latest offset list, and five-decimal dialogs. It creates deterministic visual
evidence without opening Rhino or automating Windows.

## Visual QA Artifacts

- `panel-cladding-latest-cladding-1440x900.png`
- `panel-cladding-latest-extrusion-1440x900.png`
- `panel-cladding-latest-extrusion-1024x768.png`
- `panel-cladding-latest-merged-1440x900.png`
- `panel-cladding-latest-mullion-dialog-430x272.png`

The two archive HTML timestamps and function/action counts are also recorded in
`design-source-audit.txt` so future work does not follow the stale manifest entry again.
