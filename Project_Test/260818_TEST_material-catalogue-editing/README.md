# Material Catalogue Editing Smoke

This focused WPF smoke verifies the Material Setup add/edit workflow and requested layout without UI
automation. Injected color/category adapters make modal outcomes deterministic.

Run from the repository root:

```powershell
dotnet run --project .\Project_Test\260818_TEST_material-catalogue-editing\MaterialCatalogueEditingSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_material-catalogue-editing\MaterialCatalogueEditingSmoke.csproj -c Release
```

The smoke covers catalogue-to-form loading, exact custom-color preservation, case-insensitive
Save edits/Add material switching, immutable in-place replacement, custom category addition,
three-column catalogue tiles, multiline description alignment, unclipped palette item bounds,
equal footer actions, and 720x740 plus 620x640 visual evidence.
