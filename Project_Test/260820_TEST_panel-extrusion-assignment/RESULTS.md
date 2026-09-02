# Results — Panel extrusion assignment

Date: 2026-08-20

## Focused smoke result

Command:

```powershell
dotnet run --project Project_Test/260820_TEST_panel-extrusion-assignment/PanelExtrusionAssignmentSmoke.csproj -- "V:\01 Project Folders\P00020 BayHealth Kent Tower BKT\03-Design-Eng\04-Shop-DWGs\03-Cover-General-Notes\F0-020-EXTRUSION-SCHEDULE.pdf"
```

Passed assertions:

- additive `1D-*` assignment serialization and deterministic round-trip;
- merge-run assignment consistency validation;
- frame typology changes for segment, merge, hide, and assigned-code changes;
- exact `CW_2.09_FRAME_ASSIGNMENTS` and `CW_1.5D_FRAME TYPOLOGY` save writes;
- baked frame and merged curves receive sorted `Extrusions` take-off metadata;
- real schedule import finds the ten page-one framing profiles;
- `ALU-H0651` becomes `1D-ALU-H0651` with a visually verified profile/dimension crop;
- transactional `Extrusions` worksheet round-trip preserves all thumbnail bytes;
- off-screen 1440×900 main extrusion-view render and 900×760 setup render contain all ten images.

## Regression result

The following existing smoke suites also passed:

- panel cladding sparse topology;
- curve topology and `PCCrvTemplate` compatibility;
- full-track collapse;
- extrusion spawn/sync, including updated assignment metadata propagation;
- scoped save;
- surface sync;
- WPF UI rendering.

Package validation passed for direct RHP GUID identity and registry-only installation behavior.

## 1.0.62 native-runtime regression

- The probe failed against installed `1.0.61` because top-level `libSkiaSharp.dll` was absent,
  reproducing the Rhino error's package condition.
- The probe passed against packaged `1.0.62`: top-level SkiaSharp P/Invoke constructed `SKBitmap`,
  top-level HarfBuzz loaded, and the packaged RHP imported 10 profiles from the supplied schedule.
- Release build, direct RHP GUID identity, and registry-only installer regression passed.
