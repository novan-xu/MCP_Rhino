# TEST: file-export-reliability

Validates the export failures found by the 2026-05-08 live runtime audit.

## Scope

- `capture_viewport_image` returns non-empty base64 PNG data.
- `export_to_image` writes PNG and JPG through the runtime-compatible bitmap capture adapter.
- `export_to_pdf` writes a non-empty PDF and validates output.
- `export_to_dwg`, `export_to_dxf`, and `export_to_stl` write non-empty selected-object exports through Rhino 8 `ExportSelected` or the scoped scripted fallback.
- `export_to_ifc` writes a non-empty IFC when host support is installed, or returns `IFC_EXPORT_UNAVAILABLE`.
- `export_drawing_package` writes PDF and JPG and restores drawing state.
- `update_linked_block` covers the local-block negative path and a promoted linked-block fixture path.

## Commands

CLI fallback:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -- file-export-reliability-smoke-test C:\Users\Novan\Desktop\Untitled.3dm
```

Live Rhino command:

```text
_McpFileExportReliabilitySmoke
```

## Prerequisites

- Rhino 8 is running with the rebuilt `MCP_Rhino.Server.rhp` loaded.
- The active Rhino document is saved.
- The smoke writes validation files under `_validation/file-export-reliability-smoke-test/`.
