# Panel cladding wheel-zoom aspect smoke

This focused WPF smoke reproduces and prevents the preview-stretch bug reported for panel
`E1_05_48` (`18.75 × 199.5`).

It verifies:

- the former independent 80-DIP width/60-DIP height clamps reproduce height-only growth;
- 80%, 100%, and 150% use the model aspect ratio;
- both axes receive the same zoom factor;
- positive and negative wheel detents change zoom by 5% and are handled;
- zero-pan centering remains stable;
- cladding hit rectangles and extrusion view share the corrected bounds; and
- deterministic 80%/150% preview renders.

Run with:

```powershell
dotnet run --project Project_Test/260818_TEST_panel-cladding-wheel-zoom-aspect/PanelCladdingWheelZoomAspectSmoke.csproj -c Debug
dotnet run --project Project_Test/260818_TEST_panel-cladding-wheel-zoom-aspect/PanelCladdingWheelZoomAspectSmoke.csproj -c Release
```
