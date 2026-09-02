# Results — Extrusion assignment drop zone

Date: 2026-08-26

## Focused contract smoke

Command:

```powershell
dotnet run --project Project_Test/260826_TEST_extrusion-assignment-dropzone/ExtrusionAssignmentDropZoneSmoke.csproj
```

Result: exit code `0`.

Validated:

- the extrusion assignment section declares the only extrusion-profile drop handler;
- the canvas no longer exposes extrusion-profile drop assignment;
- ready catalogue tiles have no click-to-assign handler;
- assigned preview cards and inline modifier handlers exist;
- the available catalogue is refreshed against assigned codes;
- parent dependency and manual-removal safeguards remain in the assignment path.

## WPF interaction and render regression

Command:

```powershell
dotnet run --project Project_Test/260820_TEST_panel-extrusion-assignment/PanelExtrusionAssignmentSmoke.csproj -- "V:\01 Project Folders\P00020 BayHealth Kent Tower BKT\03-Design-Eng\04-Shop-DWGs\03-Cover-General-Notes\F0-020-EXTRUSION-SCHEDULE.pdf"
```

Result: exit code `0`.

Key UI assertions:

- selecting `FRM_0` renders its assigned `1D-H0651` image card;
- `1D-H0651` is absent from Drag Extrusions while assigned;
- assigning `1D-H0579` moves it into assignment cards and filters it from the available list;
- removing `1D-H0579` makes it available again without re-adding it;
- entering `+4` in the compact card input writes the shared `FRM_0` curve modifier;
- the six-page source schedule still imports 38 profiles and workbook thumbnails round-trip.

Visual artifact:

```text
Project_Test/260820_TEST_panel-extrusion-assignment/bin/Debug/net8.0-windows/qa/extrusion-assignment-selected.png
```

## Regression suites

All passed after an isolated rerun of one transient parallel XAML-cache lock:

- `260812_TEST_panel-cladding-wpf-ui`;
- `260813_TEST_panel-cladding-latest-ui`;
- `260813_TEST_panel-cladding-direct-interactions`;
- `260818_TEST_panel-cladding-extrusion-sync`.

## Build and package validation

- Direct Debug RHP build: exit `0`, zero warnings/errors.
- Direct Release RHP build: exit `0`, zero warnings/errors.
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.67/`.
- PanelCladdingEditor assembly GUID:
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35` (declared, non-empty, correct, and distinct).
- Packaged Skia/HarfBuzz runtime probe: passed.
- Packaged real PDF import: all 38 profiles across six pages.
- Packaged and authoritative staged RHP SHA-256:
  `8C86BAC88C68AD3218F102933E19E0DC1407E8D59658ED652A468CD8420B88E7`.

## Activation result

Two Rhino processes were initially open, so the production installer correctly staged rather than
overwrote the loaded plug-in. After Rhino was closed, the authoritative staged bundle was installed
registry-only at:

```text
C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.67
```

Installer `Validate` mode passed. The packaged and installed RHP hashes both equal
`8C86BAC88C68AD3218F102933E19E0DC1407E8D59658ED652A468CD8420B88E7`, and installed assembly
identity validation passed with the declared PanelCladdingEditor GUID.
