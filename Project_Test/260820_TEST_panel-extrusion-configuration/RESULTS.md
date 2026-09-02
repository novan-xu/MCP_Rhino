# Results — Panel extrusion configuration

Date: 2026-08-20

## Focused command

```powershell
dotnet run --project Project_Test/260820_TEST_panel-extrusion-assignment/PanelExtrusionAssignmentSmoke.csproj -- "V:\01 Project Folders\P00020 BayHealth Kent Tower BKT\03-Design-Eng\04-Shop-DWGs\03-Cover-General-Notes\F0-020-EXTRUSION-SCHEDULE.pdf"
```

## Passed checks

- assignment schema v2 deterministic encode/decode with version-1 compatibility;
- 1D/0D definition persistence and merge consistency validation;
- `1D-H0579=(LL+4)*2`;
- `1D-H0651=LL+4`;
- `0D-H0652=2`;
- `0D-H0655=(LL+4)/24`;
- direct per-profile baked-curve user text plus the legacy `Extrusions` summary;
- all six real PDF pages and 38 profiles imported;
- category retention for FRAMING, both HOLLOW groups, both SOLID groups, and ASSEMBLIES;
- pool/ready workbook round-trip with four configured examples and 34 unconfigured entries;
- configured catalogue thumbnails in the main drag area;
- category-grouped pool and ready panes in the setup render;
- `PCSyncCrv` planning carries direct profile-value dictionaries;
- existing extrusion sync, spawn/sync, curve topology, full-track collapse, scoped save, surface
  structural-grid, and WPF UI regressions.

## Visual artifacts

- `Project_Test/260820_TEST_panel-extrusion-assignment/bin/Debug/net8.0-windows/qa/extrusion-assignment-setup.png`
- `Project_Test/260820_TEST_panel-extrusion-assignment/bin/Debug/net8.0-windows/qa/extrusion-assignment-main-extrusion-view.png`
- `Project_Test/260820_TEST_panel-extrusion-assignment/bin/Debug/net8.0-windows/qa/alu-h0651.png`
