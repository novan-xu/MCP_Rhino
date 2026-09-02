# Panel Cladding Canvas Notification Test

This capability reuses the focused canvas-alignment smoke because notification placement and canvas
alignment are asserted from the same WPF layout fixture.

Run the shared smoke in both configurations:

```powershell
dotnet run --project Project_Test/260813_TEST_panel-cladding-canvas-alignment/PanelCladdingCanvasAlignmentSmoke.csproj -c Debug
dotnet run --project Project_Test/260813_TEST_panel-cladding-canvas-alignment/PanelCladdingCanvasAlignmentSmoke.csproj -c Release
```

The required notification assertions are:

- `ToastBorder` is owned by `CanvasWorkspace`.
- The toast midpoint matches the canvas midpoint at desktop and compact sizes.
- Pan and zoom do not move the toast.
- The toast remains above the extrusion action buttons with the required gap.

The execution evidence and render path are recorded in
`Project_Exet/260813_EXET_panel-cladding-canvas-notification.md`.
