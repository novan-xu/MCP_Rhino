# Print-Scale Image Export Plan

## Background

The existing `ExportToImage` path captures a viewport at a requested pixel size. It does not expose Rhino Print model-scale controls and, in the current live model, produced uniform white PNGs even though native `ViewCaptureToFile` output was valid. The requested workflow needs named views to define camera direction only, while one print-scale value controls framing consistently across four isometric outputs.

RhinoCommon provides the programmatic print pipeline through `ViewCaptureSettings` and `ViewCapture`: fixed media size and DPI, visible-geometry extents mapping, scale-to-fit measurement, explicit model scale, margins, and raster image capture. A dedicated multi-view exporter is therefore warranted instead of adding more viewport-camera behavior to the existing image exporter.

## Goal

- Add a live-only MCP tool that exports one or more named Rhino views to PNG through RhinoCommon print settings.
- Treat every named view as camera orientation only.
- Map the printable area to visible geometry extents.
- Apply one common model scale to every requested view.
- Support either a caller-supplied explicit Rhino model scale or a computed common fit scale with a multiplier for controlled padding.
- Detect and reject uniform/blank captures before committing output files.
- Preserve the active viewport state and avoid document mutation.

## Architecture

- `Tools/File/Export`: add thin `ExportPrintScaleImagesTool` MCP wrapper.
- `Contracts/Requests`: add request DTOs for view/output pairs, image media size, DPI, margins, common scale mode, and overwrite policy.
- `Contracts/Responses`: add a response carrying the applied common model scale and per-image file metadata.
- `Domain/Models`: add a normalized print-scale export spec and execution result models.
- `Application/Services`: add `RhinoPrintScaleImageExportService` for validation, live-document dispatch, timeout mapping, and response shaping.
- `Application/Interfaces`: add `ILivePrintScaleImageExporter` so RhinoCommon remains behind the live adapter boundary.
- `Infrastructure/Rhino/Live`: add the RhinoCommon implementation using `ViewCaptureSettings` and `ViewCapture.CaptureToBitmap`.
- `Server/DependencyInjection.cs`: register the service and adapter in live and CLI-fallback compositions.
- `Infrastructure/CLI` and `Infrastructure/Plugin`: add a unique capability smoke route and Rhino command.
- `Project_Test/260729_TEST_print-scale-image-export/`: add CLI fallback, validation, and live smoke coverage.
- `Project_Exet/260729_EXET_print-scale-image-export.md`: record implementation and verification evidence after execution.

## Key Design

### Request Contract

`ExportPrintScaleImages` accepts:

- `filePath`: saved live Rhino document path.
- `views`: one or more `{ viewName, outputPath }` pairs; output paths must be absolute `.png` paths with existing parent folders.
- `imageSizePx`: fixed output width and height in pixels.
- `dotsPerInch`: print density used by `ViewCaptureSettings`.
- `scaleMode`: `FitAll` or `Explicit`.
- `modelScale`: required for `Explicit`; this is Rhino Print's model-to-page denominator in a same-unit ratio and is passed to `SetModelScaleToValue` (for example, `100` represents 1:100).
- `fitScaleMultiplier`: optional factor from `1.0` through `10.0` applied to the most restrictive per-view fit denominator. Values above `1.0` add padding without changing view angles.
- `marginMm`: equal printable margin on all sides.
- `backgroundTransparent`: defaults false so the document's white background is drawn.
- `overwriteExisting`: defaults false for the dedicated exporter.

### Common-Scale Algorithm

For `FitAll`, the adapter creates print settings for each named view with identical media, DPI, margins, and `ViewArea = Extents`, calls Rhino's scale-to-fit calculation, and reads the resulting scale as a same-unit model-to-page denominator. It selects the largest valid fit denominator and multiplies it by `fitScaleMultiplier`. It then recreates every capture with that one explicit scale. Reading the fit result in same units avoids converting an inch-based model through millimeter page units before passing the value back to Rhino.

For `Explicit`, the adapter applies the caller's one positive model-scale value to every view. No viewport zoom or camera targeting is used to determine framing.

### Image Commit And Blank Detection

- Resolve and render every requested view before writing final files.
- Reject a capture whose sampled pixels are uniform, including all-white or all-transparent output, with `PRINT_SCALE_IMAGE_BLANK`.
- Save each bitmap to a temporary PNG in the target directory, validate non-zero output, then move it to the requested path.
- If validation or rendering fails, dispose captures and remove temporary files. Existing final files are not touched before all images have rendered successfully.
- Block duplicate output paths and output paths equal to the source `.3dm` path.

### View-State Safety

Named views are restored temporarily into the active model viewport using the established `ViewInfo` pattern. The original active viewport state is restored in `finally`/`Dispose`, even on capture failure. The exporter does not open an undo record or mutate model geometry, attributes, layers, groups, display settings, or named-view definitions.

### MCP Safety

The tool is annotated `ReadOnly = false, Destructive = true, OpenWorld = true` because it writes external files and can overwrite them when explicitly permitted. The tool description states that Rhino document state is not mutated.

## Files To Add Or Modify

- `src/MCP_Rhino.Server/Tools/File/Export/ExportPrintScaleImagesTool.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/ExportPrintScaleImagesRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/PrintScaleImageExportResponse.cs`
- `src/MCP_Rhino.Server/Domain/Models/PrintScaleImageExportSpec.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILivePrintScaleImageExporter.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoPrintScaleImageExportService.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoPrintScaleImageExporter.cs`
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpPrintScaleImageExportSmokeCommand.cs`
- `Project_Test/260729_TEST_print-scale-image-export/DeveloperCommandHandler.PrintScaleImageExportSmokeTest.cs`
- `Project_Archive/Project_Test/260506_TEST_mcp-tool-safety-annotations/DeveloperCommandHandler.McpToolSafetyAnnotationsSmokeTest.cs`
- `Project_Exet/260729_EXET_print-scale-image-export.md`

## Usage

For the current model, call `ExportPrintScaleImages` once with the four named isometric views and four PNG output paths beside the `.3dm`. Start with `scaleMode = FitAll` and a conservative common multiplier such as `1.08` to create even padding. The response reports the exact applied model scale; a subsequent call can use `scaleMode = Explicit` with that value adjusted numerically for tighter or looser output while retaining the same four angles and scale.

## Acceptance Criteria

- Debug and Release builds succeed without warnings.
- The MCP safety smoke recognizes `ExportPrintScaleImages` as destructive open-world in both configurations.
- CLI fallback returns `LIVE_RHINO_REQUIRED` without writing output.
- Validation rejects missing views, non-PNG paths, duplicate paths, invalid media/DPI/margins/scales, and blocked overwrite.
- A live smoke exports at least two named isometric views with identical pixel dimensions and the same reported applied model scale.
- Live output files exist, are non-empty, and pass uniform-image rejection.
- The active viewport is restored after success and failure.
- The current model's four isometric PNGs are exported beside the `.3dm` after the newly built plugin is loaded in Rhino.

## Risks And Rollback

- Rhino's fit-scale calculation may depend on the active display pipeline. The live smoke will verify actual image content, not only file existence.
- Very large output media can consume substantial bitmap memory. Validation will impose bounded pixel dimensions and total pixel count.
- Multi-file overwrite cannot be fully transactional across arbitrary filesystem failures. Rendering and temporary-file validation happen before any final replacement to minimize partial commits.
- Rollback is limited to removing the new files and registrations; existing `ExportToImage` behavior remains unchanged.

## Extensions

- Add per-side margins or page-size-in-millimeters input if a physical-sheet workflow needs it.
- Add optional line-weight and output-color controls from Rhino Print.
- Add a preview-only mode returning fit scales without writing images.
- Generalize the atomic commit helper for other multi-file exporters.

## 修订记录（2026-07-29）

Live Rhino verification showed that `GetModelScale(pageUnits, modelUnits)` returns a model-to-page denominator, not a direct magnification factor. The plan and implementation were corrected to read the fit denominator in the same unit system, select the largest per-view denominator, and use multipliers above `1.0` for padding. This revision records the live-discovered Rhino Print unit semantics before execution closeout.
