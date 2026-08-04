# File Export Reliability EXET

## Corresponding Plan

- Plan: `Project_Plan/260508_PLAN_file-export-reliability.md`
- Execution date: 2026-05-09
- Capability name: `file-export-reliability`

## Associated Artifacts

- Test folder: `Project_Test/260508_TEST_file-export-reliability/`
- Runtime audit report removed by request: `Project_Plan/260508_RUNTIME_TOOL_AUDIT_live-rhino.md`
- Commit / PR: not created in this execution

## Execution Result / Actual Scope

- Downgraded `System.Drawing.Common` from `8.0.11` to `7.0.0` so the packaged plugin matches RhinoCommon 8.30 bitmap method signatures.
- Added `RhinoViewBitmapCapture`, a reflection-based bitmap capture adapter that late-binds `ViewCapture.CaptureToBitmap(ViewCaptureSettings)` and saves images through the runtime bitmap type.
- Updated `LiveRhinoViewportCapture` and `LiveRhinoFileExporter` to use the shared bitmap adapter.
- Replaced external DWG/DXF/STL/IFC export from `RhinoDoc.WriteFile(...)` to Rhino 8 `RhinoDoc.Export(...)` / `ExportSelected(...)` with `ArchivableDictionary` options.
- Added typed option mapping through `FileDwgWriteOptions` and `FileStlWriteOptions`, with warnings for unsupported or unapplied keys.
- Added IFC-specific unavailable diagnostics when Rhino does not produce a non-empty IFC output.
- Added non-empty output validation for image, PDF, DWG, DXF, STL, and IFC exports.
- Added export-specific 120 second live document accessor timeout for file export and drawing package export paths, mapped to `EXPORT_COMMAND_TIMEOUT`.
- Added a selected-scope `_ -Export` scripted fallback when code-driven external export fails to produce output.
- Added dedicated smoke slug `file-export-reliability-smoke-test`.
- Added Rhino command `_McpFileExportReliabilitySmoke`.

## Deviations From Plan

- The primary package-alignment fix was applied and builds cleanly, but the reflection adapter was also retained. This keeps bitmap capture resilient if RhinoCommon's compile-time and loaded runtime assembly identities diverge again.
- The scripted fallback is selected-scope only. Whole-document fallback remains code-driven to avoid broad scripted selection side effects.
- Live Rhino smoke was not executed during this run because no `Rhino` process was available from the shell. The live smoke entrypoint is implemented and build-validated.

## Issues Found And Fixed During Execution

- `FilePdf.AddPage(...)` returns `int`, not `bool`; the implementation now treats a negative page index as failure.
- `FilePdf.Write(...)` returns `void`; PDF success is validated by checking that the output file exists and has non-zero bytes.
- `InstanceDefinitionTable.ModifySourceArchive(int, string, ...)` is obsolete under `TreatWarningsAsErrors`; the smoke uses the `FileReference` overload.

## Test Record

- Baseline before edits:
  - `dotnet build .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug -v:minimal`
  - Exit code: 0
- Debug server build:
  - `dotnet build .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug -v:minimal`
  - Exit code: 0
- Release server build:
  - `dotnet build .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release -v:minimal`
  - Exit code: 0
- Debug solution build:
  - `dotnet build .\MCP_Rhino.sln -c Debug -v:minimal`
  - Exit code: 0
- Release solution build:
  - `dotnet build .\MCP_Rhino.sln -c Release -v:minimal`
  - Exit code: 0
- Debug CLI fallback smoke:
  - `dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -- file-export-reliability-smoke-test <LOCAL_TEST_MODEL_PATH>`
  - Exit code: 0
  - Output: `[OK] file-export-reliability CLI fallback smoke completed.`
- Release CLI fallback smoke:
  - `dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release -- file-export-reliability-smoke-test <LOCAL_TEST_MODEL_PATH>`
  - Exit code: 0
  - Output: `[OK] file-export-reliability CLI fallback smoke completed.`
- Packaged bitmap dependency check:
  - Debug `System.Drawing.Common.dll`: product version `7.0.0+d099f075e45d2aa6007a22b71b45a08758559f80`
  - Release `System.Drawing.Common.dll`: product version `7.0.0+d099f075e45d2aa6007a22b71b45a08758559f80`
- Live Rhino availability check:
  - `Get-Process Rhino -ErrorAction SilentlyContinue`
  - Result: `NO_RHINO_PROCESS`

## Acceptance Alignment

- `capture_viewport_image`: implementation changed to shared runtime-compatible bitmap adapter; live validation pending.
- `export_to_image`: implementation changed to shared runtime-compatible bitmap adapter with non-empty output validation; live validation pending.
- `export_to_pdf`: output validation and AddPage failure handling added; live validation pending.
- `export_drawing_package`: now uses fixed image/PDF paths and has export timeout; live validation pending.
- `export_to_dwg`, `export_to_dxf`, `export_to_stl`: moved to `ExportSelected` / `Export` with typed dictionaries and selected-scope fallback; live validation pending.
- `export_to_ifc`: moved to `ExportSelected` / `Export`, with `IFC_EXPORT_UNAVAILABLE` diagnostic when host support is absent; live validation pending.
- `update_linked_block`: smoke covers local-block negative path plus a linked-block fixture path promoted through `ModifySourceArchive(FileReference, Linked, ...)`; live validation pending.
- Debug and Release builds: passed.

## Rollback Verification

- No destructive rollback command was run.
- The implementation keeps previous tool contracts and method names.
- The external export fallback preserves and restores Rhino selection in `finally`.
- Drawing package still restores drawing state in `finally`.

## Current Remaining Items

- Run `_McpFileExportReliabilitySmoke` in Rhino after loading the rebuilt plugin.
- If the scripted export fallback opens a modal dialog in a specific Rhino installation, disable that format's fallback and keep the code-driven diagnostic path.

## Conclusion

The file export reliability implementation and repository-level validation are complete. Live Rhino validation is now available through `_McpFileExportReliabilitySmoke` and remains the only pending runtime confirmation.

## Live Rhino Validation Addendum (2026-05-08)

Validated against the running Debug plugin through `src\MCP_Rhino.Bridge\bin\Debug\net8.0\MCP_Rhino.Bridge.exe`.

Loaded runtime:

- Rhino process: `4892`
- Active document: `<LOCAL_TEST_MODEL_PATH>`
- Loaded plugin: `src\MCP_Rhino.Server\bin\Debug\net8.0\MCP_Rhino.Server.rhp`
- Loaded plugin timestamp: `2026-05-08 20:50:05`
- Loaded RhinoCommon: `C:\Program Files\Rhino 8\System\netcore\RhinoCommon.dll`, product version `Rhino 8.30`
- Loaded `System.Drawing.Common`: `<DOTNET_SHARED_FRAMEWORK>\System.Drawing.Common.dll`

Focused live tool probe output directory:

- `<REPO_ROOT>\_validation\live-file-export-check-20260508_205322`

Results:

| Tool | Status | Output / Size |
| --- | --- | --- |
| `capture_viewport_image` | PASS | base64 PNG, 708 bytes |
| `export_to_image` PNG | PASS | `live-export.png`, 708 bytes |
| `export_to_image` JPG | PASS | `live-export.jpg`, 1667 bytes |
| `export_to_pdf` | PASS | `live-export.pdf`, 2688 bytes |
| `export_to_dwg` | PASS | `live-export.dwg`, 28736 bytes |
| `export_to_dxf` | PASS | `live-export.dxf`, 173393 bytes |
| `export_to_stl` | PASS | `live-export.stl`, 1884 bytes; `BinaryFile` option applied |
| `export_to_ifc` | PASS | `live-export.ifc`, 46827 bytes |
| `export_drawing_package` | PASS | `MCP_Elevation_Front.pdf`, 2757 bytes; `MCP_Elevation_Front.jpg`, 1667 bytes; restore succeeded |

Conclusion update:

- The previously failing capture/image path is fixed in the loaded Rhino process.
- DWG, DXF, STL, IFC, PDF, and drawing package exports all produced non-empty files in live Rhino.
