# File Export Reliability Plan

## Background

The live runtime audit and follow-up retest showed these remaining export problems:

- `capture_viewport_image`, `export_to_image`, and the JPG portion of `export_drawing_package` fail at runtime with a missing `ViewCapture.CaptureToBitmap(ViewCaptureSettings)` method.
- `export_to_pdf` passes after the Debug rebuild/reload.
- `export_to_dwg`, `export_to_dxf`, `export_to_stl`, and `export_to_ifc` still fail through the current `RhinoDoc.WriteFile(...)` path.
- `update_linked_block` is fixture-limited, not an export implementation failure.

Local reflection against `C:\Program Files\Rhino 8\System\netcore\RhinoCommon.dll` confirmed:

- RhinoCommon version: `8.30.26103.11001`.
- `Rhino.Display.ViewCapture.CaptureToBitmap(ViewCaptureSettings)` exists, but it returns `System.Drawing.Bitmap` from `System.Drawing.Common, Version=7.0.0.0`.
- `MCP_Rhino.Server` currently packages `System.Drawing.Common` `8.0.11`, so the compiled method signature does not match RhinoCommon's runtime signature.
- Rhino 8 exposes `RhinoDoc.Export(...)` and `RhinoDoc.ExportSelected(...)`, including overloads that accept `ArchivableDictionary` options.
- Rhino 8 exposes format-specific write option classes for at least DWG/DXF and STL: `FileDwgWriteOptions` and `FileStlWriteOptions`. Reflection did not show a `FileIfcWriteOptions` class in this RhinoCommon build.

Official Rhino developer docs align with this: Rhino 8 introduced code-driven file IO using `RhinoDoc.Export` plus format option dictionaries, while older workflows relied on scripting active-document export commands. The `ExportSelected(filePath, ArchivableDictionary)` API is documented as supporting all Rhino-exportable file formats.

## Goals

1. Make viewport bitmap capture work in the loaded Rhino 8 plugin.
2. Make image export work for PNG/JPG/BMP/TIFF without modal dialogs.
3. Keep the existing passing PDF path, but add output validation and better diagnostics.
4. Replace external DWG/DXF/STL/IFC export implementation with a Rhino 8-compatible, non-interactive path.
5. Preserve current tool contracts where possible.
6. Add targeted live smoke coverage for every previously failing tool.
7. Keep Debug and Release plugin builds passing.

## Non-Goals

- Do not introduce disk `.3dm` reads as fallback truth.
- Do not require human interaction in export dialogs during MCP tool execution.
- Do not add broad UI automation.
- Do not change user-facing tool names unless implementation proves a contract is impossible.

## Architecture Ownership

- `Tools/File/Export`: keep existing MCP tools and safety annotations.
- `Tools/Viewport`: no new tool needed unless capture behavior is split later.
- `Application/Services/RhinoFileExportService.cs`: keep request validation and response shaping.
- `Infrastructure/Rhino/Live/LiveRhinoFileExporter.cs`: main implementation change for file export and bitmap capture.
- `Infrastructure/Rhino/Live/LiveRhinoViewportCapture.cs`: bitmap capture implementation change.
- `Domain/Models/FileExportSpec.cs`: extend export options only if needed to represent typed options safely.
- `Project_Test/260508_TEST_file-export-reliability/`: new smoke/probe coverage.

## Key Design

### 1. Fix Bitmap Capture Assembly Compatibility

Primary fix:

- Change `MCP_Rhino.Server.csproj` from `System.Drawing.Common` `8.0.11` to a version compatible with RhinoCommon's bitmap signatures, expected to be `7.0.0`.
- Rebuild Debug and Release, then verify the output folder contains the compatible `System.Drawing.Common.dll`.
- Retest `capture_viewport_image`, `export_to_image`, and `export_drawing_package`.

Reasoning:

- The method exists in RhinoCommon, but runtime lookup fails because the plugin's compiled call expects the `System.Drawing.Bitmap` identity from the wrong `System.Drawing.Common` assembly version.
- Aligning the package version should make the method signature match.

Fallback if package downgrade causes build/advisory issues:

- Isolate all bitmap capture in a small adapter and late-bind `ViewCapture.CaptureToBitmap(...)` through reflection against RhinoCommon's loaded runtime types.
- Keep returned object handling inside the same adapter and save through runtime-compatible `System.Drawing` APIs.
- Prefer package alignment first because it is simpler and less brittle.

### 2. Replace `WriteFile` With Rhino 8 Code-Driven Export

Replace this current path:

- select objects temporarily
- call `RhinoDoc.WriteFile(outputPath, FileWriteOptions)`

With this path:

- resolve selected object ids as today
- temporarily set Rhino selection only when `selectedObjectIds` is non-empty
- call `document.ExportSelected(outputPath, optionsDictionary)` for selected exports
- call `document.Export(outputPath, optionsDictionary)` for whole-document exports
- restore selection in `finally`

Format option strategy:

- DWG/DXF: use `Rhino.FileIO.FileDwgWriteOptions`, convert with `ToDictionary()`.
- STL: use `Rhino.FileIO.FileStlWriteOptions`, convert with `ToDictionary()`.
- IFC: first try `Export` / `ExportSelected` with an empty `ArchivableDictionary` because this RhinoCommon build exposes no `FileIfcWriteOptions`.
- Preserve the existing `formatOptions` request shape, but stop warning that all options are ignored once supported keys are mapped.

Initial supported `formatOptions`:

- DWG/DXF:
  - `Version`
  - `UseLWPolylines`
  - `Flatten`
  - `FullLayerPath`
  - `ExportSurfacesAs`
  - `ExportMeshesAs`
  - `ExportSplinesAs`
- STL:
  - `BinaryFile`
  - `ExportOpenObjects`
  - a conservative meshing preset if a simple request key is supplied
- IFC:
  - no mapped options initially; return a warning that IFC options are not mapped unless a working option class or dictionary keys are confirmed.

### 3. Scripted Export Fallback, Only Where Needed

Add a second export strategy behind `ILiveFileExporter` for formats where code-driven export still returns false:

- Use `RhinoApp.RunScript(...)` with command-line forms, not modal dialogs.
- Use hyphenated commands such as `_-Export` so Rhino uses command-line prompts where available.
- Use quoted absolute paths and `_Enter` to accept defaults.
- Keep selected-object export by selecting exactly the resolved object ids before running the script.
- Restore selection after the script.
- Confirm output file exists and has non-zero bytes before returning success.

This fallback should be format-gated and smoke-tested. It should not become the first path for DWG/DXF/STL if `ExportSelected` works with typed dictionaries.

### 4. Timeout And Dialog Safety

Current main-thread execution has a fixed 10 second timeout. Export may legitimately take longer, but a hidden modal dialog must not hang the MCP call indefinitely.

Plan:

- First attempt code-driven export with option dictionaries, which should avoid dialogs.
- If scripted fallback is needed, add an export-specific main-thread execution timeout, for example 120 seconds.
- Before returning, verify:
  - Rhino is not still running a command if that can be checked safely.
  - output file exists and has non-zero bytes.
  - selection state was restored.
- If a command does not complete within the export timeout, return a clear `EXPORT_COMMAND_TIMEOUT` message rather than `RHINO_MAIN_THREAD_BUSY`.

### 5. PDF Hardening

Keep `FilePdf.Create()` plus `AddPage(ViewCaptureSettings)` because retest passed.

Add:

- check the `AddPage(...)` return value
- check output existence and byte count after `Write(...)`
- return a specific failure if output is missing or empty

### 6. Drawing Package

No separate export path should be added for drawing package JPG/PDF.

Instead:

- Keep using `ILiveFileExporter`.
- Once image export works, package export should pass for PDF+JPG.
- Preserve the existing restore-in-finally behavior.
- Add assertions that a partial package failure restores drawing state and reports which item failed.

### 7. Linked Block Retest Fixture

`update_linked_block` did not fail because of export code. It needs a true linked-block fixture.

Plan:

- Create a small temporary source `.3dm` for a linked block fixture during live smoke.
- Insert it as a linked block using the least interactive reliable path available:
  - preferred: RhinoCommon instance definition API if it can create linked definitions directly
  - fallback: command-line `_Insert` script with linked-block options, if scriptable non-interactively
- Run `update_linked_block` against that linked definition.
- Keep the current local-block negative-path assertion.

## Involved Files

- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoViewportCapture.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoFileExporter.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoFileExportService.cs`
- `src/MCP_Rhino.Server/Domain/Models/FileExportSpec.cs`
- `src/MCP_Rhino.Server/Tools/File/Export/*.cs`
- `src/MCP_Rhino.Server/Tools/Drawing/ExportDrawingPackageTool.cs`
- `Project_Test/260508_TEST_file-export-reliability/`
- `Project_Exet/260508_EXET_file-export-reliability.md` after implementation

## Use Flow After Change

Existing tool calls remain valid:

- `capture_viewport_image`
- `export_to_image`
- `export_to_pdf`
- `export_to_dwg`
- `export_to_dxf`
- `export_to_stl`
- `export_to_ifc`
- `export_drawing_package`
- `update_linked_block`

No new user workflow should be required for normal export. Users should not need to respond to Rhino export dialogs.

## Acceptance Criteria

Build:

- `dotnet build .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug`
- `dotnet build .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release`

Live retest with Rhino 8:

- `capture_viewport_image` returns base64 PNG bytes.
- `export_to_image` writes non-empty PNG and JPG outputs.
- `export_to_pdf` writes a non-empty PDF.
- `export_drawing_package` writes both PDF and JPG for at least one named drawing view and restores state.
- `export_to_dwg` writes a non-empty DWG.
- `export_to_dxf` writes a non-empty DXF.
- `export_to_stl` writes a non-empty STL.
- `export_to_ifc` either writes a non-empty IFC or returns a precise capability message identifying missing Rhino IFC export support on the host.
- `update_linked_block` has both:
  - local-block negative test: reports not linked
  - linked-block positive test: refreshes or reports a precise stale/missing source status

Regression:

- Existing full runtime tool audit should improve from the previous 119 pass / 8 fail / 1 blocked baseline.
- Tool safety annotations remain valid.
- Existing PDF passing behavior remains passing.

## Risks And Mitigations

- `System.Drawing.Common` 7 may trigger NuGet advisory warnings under `TreatWarningsAsErrors`.
  - Mitigation: first verify actual build behavior. If unacceptable, use a targeted reflection adapter and document the reason in EXET.
- IFC export support depends on installed Rhino export plugins.
  - Mitigation: make IFC failure diagnostic explicit and test on the user's installed Rhino.
- Script fallback may still open a modal options window for some formats.
  - Mitigation: prefer code-driven `Export` / `ExportSelected`; only add scripted fallbacks after proving non-interactive command macros in live Rhino.
- Export commands may take longer than 10 seconds on large models.
  - Mitigation: add export-specific timeout only for export execution, not global mutation tools.
- Selection changes during export could leak into the user's document state.
  - Mitigation: preserve and restore selection in `finally`, with smoke assertions.

## Follow-Up Extensions

- Add typed request DTOs for DWG/DXF/STL options instead of relying only on stringly `formatOptions`.
- Add richer export result diagnostics, including strategy used: `CodeDrivenExport`, `ScriptedExport`, or `PdfFileApi`.
- Add optional preview/validate export capability that reports whether a format is supported on the current Rhino installation before attempting export.
- Add a reusable linked-block fixture builder for future external-reference tests.
