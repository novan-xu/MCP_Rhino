# EXET — Panel extrusion assignment

Date: 2026-08-20

Plan: `Project_Plan/260820_PLAN_panel-extrusion-assignment.md`

## Execution

- Added a production PDF schedule importer based on PdfPig/Skia. It identifies positioned `ALU-*`
  die-number headers, prefers schedule pages marked `FRAMING`, renders each selected page once, and
  produces tightly cropped, bounded PNG thumbnails.
- Added the workbook `Extrusions` catalogue with code, source code, description, source PDF, page,
  and thumbnail payload. Catalogue replacement is prepared and committed transactionally.
- Added an extrusion setup window and image-over-code tiles in the main extrusion view. Catalogue
  items support click or drag assignment, and multiple `1D-*` codes can be assigned to one curve.
- Added persistent frame/segment assignment state under `CW_2.09_FRAME_ASSIGNMENTS`. Assignments are
  stable by `FRM_0`–`FRM_3` or topology segment coordinates and are remapped across mullion insertion,
  track collapse, merge/explode, delete, and undo.
- Added deterministic frame typology under the exact key `CW_1.5D_FRAME TYPOLOGY`. The identity
  includes the full segment, merge, and hide masks plus every assigned extrusion code, and remains
  absent when no extrusion assignment exists.
- Added baked-curve `Extrusions` user text containing sorted semicolon-delimited codes. `PCSyncCrv`
  now refreshes or removes this take-off metadata on existing baked curves.
- Updated package construction to retain only the Windows x64 native Skia/HarfBuzz runtime assets.

## Packaging outcome

- Built and validated `PanelCladdingEditor` version `1.0.61`.
- Verified assembly plug-in GUID `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35` directly from the packaged RHP.
- Passed registry-only installation, validation, stray-package rejection, and uninstall smoke tests.
- Rhino PID 7412 was running at the first activation attempt, so the production installer staged the
  bundle at `C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.61-20260820185744388`.
- After Rhino was closed, version `1.0.61` was activated registry-only at
  `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.61`. Production validation confirmed
  the registered RHP path, startup load mode, owned-file hashes, and Windows x64 Skia runtime.

## Result

Implementation, automated/visual verification, and production activation completed. Restart Rhino
to load version `1.0.61` and register its commands.

## 1.0.62 native-resolution follow-up

The first Rhino-hosted PDF import exposed a deployment difference that the original test host did
not reproduce: `SkiaSharp.dll` loaded, but its P/Invoke dependency `libSkiaSharp.dll` was present
only under `runtimes/win-x64/native`. Rhino's plug-in load context did not resolve the nested native
asset and returned `0x8007007E`.

This is a packaging-only deviation from the plan; the PDF parser/crop implementation did not change.
The package builder now requires and copies `libSkiaSharp.dll` and `libHarfBuzzSharp.dll` beside the
RHP while retaining the `.deps.json` runtime copies. A new isolated native-runtime probe first
reproduced the installed `1.0.61` failure, then passed against `1.0.62` by constructing an actual
`SKBitmap`, loading HarfBuzz, loading the packaged RHP, and importing all 10 framing profiles from
the real project schedule.

Release build, direct assembly GUID verification, and registry-only install/repair/uninstall smoke
all passed for `1.0.62`. Rhino PID 59060 was running, so the fixed package was staged at
`C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.62-20260820195637115`. Close Rhino and
activate that staged bundle to replace `1.0.61`.

After the remaining Rhino shutdown processes exited, `1.0.62` was activated registry-only at
`C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.62`. Installed-hash and registry
validation passed, followed by an installed-folder native/PDF probe that constructed `SKBitmap`,
loaded HarfBuzz, and imported all 10 framing profiles from the real schedule.
