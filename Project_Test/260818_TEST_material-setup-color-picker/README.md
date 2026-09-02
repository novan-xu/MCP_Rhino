# Material Setup Color Picker Smoke

This focused WPF smoke verifies the custom-color swatch button without opening Rhino's modal picker
or using Windows UI automation. Injected picker fakes cover both confirmed and cancelled results.

Run from the repository root:

```powershell
dotnet run --project .\Project_Test\260818_TEST_material-setup-color-picker\MaterialSetupColorPickerSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_material-setup-color-picker\MaterialSetupColorPickerSmoke.csproj -c Release
```

The smoke verifies accessible button semantics and visual states, initial-color routing, confirmed
preview/hex/effective-state synchronization, palette deselection, cancellation preservation, and a
deterministic 720x740 off-screen render.
