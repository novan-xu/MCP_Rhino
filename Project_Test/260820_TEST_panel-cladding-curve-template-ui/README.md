# Panel cladding curve-template and responsive-UI smoke

This focused smoke verifies:

- H/V priority merge-run planning and canonical mask round-tripping;
- deleted/hidden segment preservation and priority reapplication idempotence;
- `PCCrvTemplate` command registration, selection-before-priority flow, batch Undo, and rollback
  source contracts;
- complete save-button labels at the editor's constrained supported width; and
- a taller material catalogue whose content-measured tiles wrap into as many columns as fit.

Run from the repository root in both configurations:

```powershell
dotnet run --project .\Project_Test\260820_TEST_panel-cladding-curve-template-ui\PanelCladdingCurveTemplateUiSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260820_TEST_panel-cladding-curve-template-ui\PanelCladdingCurveTemplateUiSmoke.csproj -c Release
```

The smoke writes two off-screen WPF QA renders into this folder:

- `panel-cladding-footer-980x700.png`
- `material-catalogue-responsive-720x740.png`
