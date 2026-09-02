# Panel extrusion assignment smoke test

Validates additive main-frame extrusion assignments, deterministic frame typology, exact Rhino
attribute contracts, the production PDF schedule importer against the BayHealth framing schedule,
and transactional round-tripping through the workbook `Extrusions` worksheet.

Run with:

```powershell
dotnet run --project Project_Test/260820_TEST_panel-extrusion-assignment/PanelExtrusionAssignmentSmoke.csproj -- "V:\01 Project Folders\P00020 BayHealth Kent Tower BKT\03-Design-Eng\04-Shop-DWGs\03-Cover-General-Notes\F0-020-EXTRUSION-SCHEDULE.pdf"
```

The test writes the imported `ALU-H0651` crop, a QA workbook, the main extrusion view, and the
secondary setup window under its build output `qa` directory for visual and data inspection.

After packaging, verify Rhino-style top-level native resolution with:

```powershell
dotnet run --project Project_Test/260820_TEST_panel-extrusion-assignment/NativeRuntimeProbe/PanelExtrusionNativeRuntimeProbe.csproj -- Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.62/Plugin "V:\project\extrusion-schedule.pdf"
```
