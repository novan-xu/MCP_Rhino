# Print-Scale Image Export Execution

## 对应计划

- Plan: `Project_Plan/260729_PLAN_print-scale-image-export.md`
- Execution date: 2026-07-29

## 关联产物

- Test folder: `Project_Test/260729_TEST_print-scale-image-export/`
- Smoke source: `Project_Test/260729_TEST_print-scale-image-export/DeveloperCommandHandler.PrintScaleImageExportSmokeTest.cs`
- Live scale evidence: `Project_Test/260729_TEST_print-scale-image-export/live-scale-values.txt`
- Live export result: `Project_Test/260729_TEST_print-scale-image-export/live-export-result.txt`
- Live lot/group/color verification: `Project_Test/260729_TEST_print-scale-image-export/live-lot-group-verification.txt`
- Commit / PR: none created.

## 执行结果 / 实际落地范围

- Added `ExportPrintScaleImages` under `Tools/File/Export` with explicit MCP safety metadata: `ReadOnly = false`, `Destructive = true`, `OpenWorld = true`.
- Added request/response contracts, `PrintScaleImageMode`, normalized domain specs/results, `ILivePrintScaleImageExporter`, `RhinoPrintScaleImageExportService`, and `LiveRhinoPrintScaleImageExporter`.
- Registered live and CLI-fallback adapters plus the application service through dependency injection.
- Added unique smoke slug `print-scale-image-export-smoke-test` and Rhino command `_McpPrintScaleImageExportSmoke`.
- Extended `RhinoViewBitmapCapture` with print-settings capture and sampled uniform-image detection.
- Implemented batch validation, same-scale `FitAll` and `Explicit` modes, visible-extents mapping, margins, bounded media sizes, temporary PNG commits, overwrite checks, named-view restoration, and blank-image rejection.
- Updated the archived MCP tool-safety regression table with `ExportPrintScaleImages` as destructive open-world.
- Invoked the newly built exporter inside the active lot-model Rhino process through RhinoCode, without Computer Use, so captures used the current live unsaved state.
- Final live exports use 2400 × 2400 px, 300 DPI, 6 mm margins, a common same-unit model-scale denominator of `804.9790654462527`, and white background.
- Removed five untagged task-generated Breps from `01_SAMPLE-CW Panels::slab` after an exact-id preview, then removed the empty layer and regenerated the four images from the original 1,167 panel objects.

## 与计划的偏差

- Live verification established that Rhino reports model scale as a model-to-page denominator. The implementation therefore chooses the largest per-view fit denominator and uses a multiplier above `1.0` for padding. The original draft assumption used the opposite direction; the Plan carries a dated revision note and this EXET records the discovered behavior.
- The installed plugin loaded in Rhino did not yet contain the new tool. To complete live validation without restarting or closing the user's active model, the corrected Release assembly was loaded into the existing Rhino process through its RhinoCode script server and invoked by reflection. No mouse/keyboard automation or Computer Use was used.
- After live loading, Rhino retained a file lock on the normal Release output DLL. Final Release validation therefore built the Server project to an isolated output folder. A full solution Release build had already passed before the two-line scale-direction correction; the corrected Server Release build and all final smokes passed from the isolated output.

## 施工中发现并修复的问题

1. The existing viewport image exporter could write non-empty but entirely white PNGs. The new exporter samples bitmap pixels and fails with `PRINT_SCALE_IMAGE_BLANK` instead of reporting success for a uniform image.
2. The first live print-scale pass read the fit scale using millimeter page units against an inch model, selected the smallest value, and treated a denominator like magnification. This produced clipped, over-zoomed images. Live measurements showed same-unit fit denominators of `745.3509865243079`, `654.9239127377865`, `508.71931571227765`, and `745.3509865243079`. The exporter now reads same-unit denominators, selects the largest, and multiplies by `1.08`, yielding `804.9790654462527` for all four views.
3. Five untagged Breps on a task-created `slab` layer increased the model from 1,167 to 1,172 objects and appeared in visible extents. Exact-id preview confirmed only those five objects; they and the empty layer were removed. Final summary returned 1,167 Breps.
4. Failed and diagnostic PNGs from earlier capture attempts were removed. Only the four verified `_print.png` outputs remain in the model folder.

## 测试记录

### Build validation

- Baseline `dotnet build MCP_Rhino.sln -c Debug --no-restore` before changes: exit `0`, 0 warnings, 0 errors.
- Post-implementation `dotnet build MCP_Rhino.sln -c Debug --no-restore`: exit `0`, 0 warnings, 0 errors.
- Post-implementation `dotnet build MCP_Rhino.sln -c Release --no-restore`: exit `0`, 0 warnings, 0 errors.
- Final corrected `dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj -c Debug --no-restore`: exit `0`, 0 warnings, 0 errors.
- Final corrected Release build to isolated output:
  `dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj -c Release --no-restore -o %USERPROFILE%\AppData\Local\Temp\MCP_Rhino_PrintScaleBuild_Final`
  → exit `0`, 0 warnings, 0 errors.
- A normal-path final Release copy attempt failed only because Rhino PID 4000 held the previously live-loaded DLL; compilation to the isolated output succeeded.

### CLI and safety smokes

- Debug `print-scale-image-export-smoke-test`: exit `0`; validation and CLI fallback checkpoints `[OK]`.
- Debug `mcp-tool-safety-annotations-smoke-test`: exit `0`; 157 tools verified and no bare method attributes.
- Final isolated Release `print-scale-image-export-smoke-test`: exit `0`; validation and CLI fallback checkpoints `[OK]`.
- Final isolated Release `mcp-tool-safety-annotations-smoke-test`: exit `0`; 157 tools verified and no bare method attributes.

### Live model verification

- Active document: `<PROJECT_ROOT>\06_exports\20260729_Lot visualization\20260729_Lot visualization.3dm`
- Model units: Inches.
- Final object count: 1,167 Breps.
- Named isometric views present: `MCP_Iso_NE`, `MCP_Iso_NW`, `MCP_Iso_SE`, `MCP_Iso_SW`.
- Ten effective lots each have exactly one shared Rhino group and exactly one lot color; group counts are 50, 60, 118, 118, 118, 118, 118, 118, 17, and 84.
- Ten distinct effective-lot colors were verified.
- Blank/TBD objects: 248; white objects: 248.
- Four final outputs are each 2400 × 2400 and have 45 sampled/full-image colors:
  - NE: 230,469 bytes; non-white bbox `(154, 529, 2246, 1871)`.
  - NW: 216,786 bytes; non-white bbox `(549, 281, 1851, 2119)`.
  - SE: 200,219 bytes; non-white bbox `(549, 486, 1851, 1914)`.
  - SW: 219,949 bytes; non-white bbox `(154, 568, 2246, 1832)`.
- All four images were visually inspected after final regeneration; each shows the full visible panel geometry on white with the expected lot colors and no clipping.

## 验收判据对齐

- Dedicated print-scale image tool: met.
- Named views determine angle only: met through named-view restoration plus print `ViewArea = Extents` and explicit scale application.
- One identical scale for every image: met; denominator `804.9790654462527` for all four.
- Visible geometry constrained: met; print extents recomputed from the cleaned 1,167 visible panel Breps.
- White background: met and visually verified.
- PNG output beside the `.3dm`: met for all four files.
- Blank output protection: met through uniform-image rejection and final non-white bounding-box checks.
- Debug/Release and safety validation: met, with isolated final Release output documented above.
- No Computer Use: met.

## 回退验证

- The existing `ExportToImage` and PDF/DWG/DXF/IFC/STL paths were not changed.
- Removing the new tool, contracts, service, adapter, DI registrations, smoke hook/command, and bitmap helper additions restores the prior capability surface.
- Rhino document changes use undo-capable MCP mutations. The five out-of-scope slab objects and their layer were removed explicitly; no requested panel objects were deleted.
- External outputs are isolated to the four named `_print.png` files in the model folder and can be removed independently of the `.3dm`.

## 当前遗留项

- The installed current-user plugin remains the pre-construction build until Rhino is restarted and the package is upgraded. The active task was completed by live-loading the built exporter through RhinoCode.
- The normal Release bin DLL remains locked until the live Rhino process releases the dynamically loaded assembly. This does not affect the isolated verified Release output or the exported images.

## 结论

The dedicated RhinoCommon print-scale exporter is implemented and verified. The active lot model is back to 1,167 panel Breps, its effective lots are grouped and distinctly colored, blank/TBD geometry is white, four isometric named views are present, and four visually verified PNGs were exported beside the model at one common print scale.
