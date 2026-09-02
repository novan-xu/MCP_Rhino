# Extrusion assignment drop-zone smoke

Validates that extrusion catalogue tiles are drag-only, the assignment section is the only profile
drop target, assigned cards carry profile previews and inline curve modifiers, and the available
catalogue is filtered against the selected curves' assigned codes.

Run:

```powershell
dotnet run --project Project_Test/260826_TEST_extrusion-assignment-dropzone/ExtrusionAssignmentDropZoneSmoke.csproj
```

The richer WPF interaction and render fixture remains in
`Project_Test/260820_TEST_panel-extrusion-assignment` and is run alongside this focused contract test.
