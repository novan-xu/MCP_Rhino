# 260429_PLAN_drawing-export-workflow

## Background

The current MCP_Rhino tool surface already covers several pieces needed for drawing output:

- Object filtering by layer, type, and user attributes.
- Temporary object display color changes through `ApplyObjectEdits`.
- PDF and image export from an open Rhino viewport through `ExportToPdf` and `ExportToImage`.

The missing capability is a reliable, repeatable drawing-export workflow that can prepare diagram/elevation
views, fit those views to a user-selected layer scope, temporarily apply export styling, export PDF/JPG drawings,
and restore the document state afterward.

This capability is construction work, not a runtime-only task, because it requires new tool contracts, workflow
orchestration, live Rhino viewport manipulation, and state restoration support.

## Goal

Add a live-only `drawing-export-workflow` capability that can:

- Create or update a standard set of named drawing views.
- Fit those named views to visible, non-deleted objects on user-selected layers.
- Return a structured `NeedsLayerSelection` response with candidate layers when layer input is missing.
- Temporarily apply object display color overrides for drawing export.
- Set the export background to white.
- Export PDF and JPG drawings from the setup views.
- Restore exact object display attributes and background after export, including failure paths.

Default decisions:

- View preset: Standard 8.
- Zoom target: visible, non-deleted objects on selected layers.
- Missing layer behavior: structured MCP response, not blocking Rhino command-line input.
- API shape: both atomic tools and one workflow tool.
- Restore policy: exact snapshot restore.

## Architecture Ownership

- `Tools/`: expose MCP-facing atomic tools plus the workflow tool. These should stay thin and delegate to services.
- `Skills/`: add a fixed workflow skill only if the orchestration needs to compose existing services/tools cleanly.
- `Application/Services/`: own workflow orchestration, validation, snapshot lifecycle, and export package assembly.
- `Application/Interfaces/`: define live Rhino abstractions for drawing view management and export state restore.
- `Infrastructure/Rhino/Live/`: contain all `RhinoDoc`, `RhinoView`, `NamedViewTable`, export viewport materialization, background, and attribute replay calls.
- `Contracts/Requests` and `Contracts/Responses`: define MCP request/response DTOs for view setup, background setup, restore, and package export.
- `Domain/Models` and `Domain/Enums`: hold drawing view presets, snapshot models, export item results, and view definitions.

All mutation or preview-of-mutation behavior must use the live Rhino adapter path through `ILiveRhinoDocumentAccessor`.
No offline fallback is allowed for this capability.

## Key Design

1. Standard named views

   The default preset creates or updates these 8 named views:

   - `MCP_Elevation_Front`
   - `MCP_Elevation_Back`
   - `MCP_Elevation_Left`
   - `MCP_Elevation_Right`
   - `MCP_Iso_NE`
   - `MCP_Iso_NW`
   - `MCP_Iso_SE`
   - `MCP_Iso_SW`

   Elevations use parallel orthographic projections. Isometrics use parallel isometric camera directions. The implementation
   should create/update named views by name rather than creating duplicates on repeated calls.

   Named view table entries are the durable document state, but PDF/JPG capture needs a real `RhinoView`. The implementation
   must either extend the live exporter to resolve `doc.NamedViews` or materialize each named view into a dedicated export
   viewport before capture. Do not pass `NamedViewTable` names directly to the current `ExportToPdf` / `ExportToImage`
   path without this bridge, because the existing exporter resolves open viewport names.

2. Layer-based zoom scope

   `SetupDrawingViews` and `ExportDrawingPackage` must accept layer selection using the existing layer selection pattern:

   - `layerQueries`
   - `confirmedLayerFullPaths`

   If both are omitted or empty, the operation returns a successful response whose status is `NeedsLayerSelection` and whose
   payload includes candidate layer names/full paths/object counts so the MCP client can ask the user to choose a layer and
   call again. Do not model this as `OperationResponse<T>.Fail(...)`, because the current response wrapper clears `Data` on
   failure and would drop the candidate layers.

   Once layers are resolved, the view fit target is the union bounding box of visible, non-deleted objects on those layers.
   If selected layers resolve but contain no visible exportable objects, return `DRAWING_VIEW_TARGET_EMPTY`.

   Use a fixed fit margin of 10 percent around the target bounding box unless a future request adds a user-controlled margin.

3. Temporary export state and styling

   The workflow should snapshot all object attributes it may change before applying display color overrides, and snapshot
   background state before changing background color. Existing metadata snapshot/replay support can be reused or extended,
   but restore must preserve the original `ObjectColor`, `ColorSource`, plot color source, layer index, name, visibility,
   linetype, plot weight, and user strings for touched objects.

   Because Rhino background APIs can affect application or display settings, the implementation must explicitly restore the
   prior state in a `finally` path and report restore warnings if any replay step fails. Snapshot records used by atomic
   tools must be stored in a live server singleton keyed by `snapshotId`, `filePath`, and active document runtime serial or
   equivalent identity, with a short TTL. `RestoreDrawingExportState` must reject stale or document-mismatched snapshots.

4. Export orchestration

   `ExportDrawingPackage` runs this sequence:

   1. Resolve layer scope or return `NeedsLayerSelection` with candidate layer payload.
   2. Snapshot object attributes and background.
   3. Apply optional temporary display color overrides to the filtered objects.
   4. Create/update and fit the standard named views to the selected layer object bounding box.
   5. Set export background to white.
   6. Materialize each requested named view into a capturable `RhinoView` when needed.
   7. Export requested PDF and JPG files for each requested view.
   8. Restore exact object attributes and background in `finally`.

   Existing `RhinoFileExportService` behavior should be reused for PDF/JPG generation, but implementation may call
   `ILiveFileExporter` directly inside the drawing workflow's single live document scope if that is needed to keep snapshot,
   viewport materialization, export, and restore in one `try/finally` flow.

   Output file names are deterministic: combine `outputDirectory`, sanitized view name, and extension. If both PDF and JPG
   are requested, each view produces `<sanitized-view-name>.pdf` and `<sanitized-view-name>.jpg`. Honor `overwriteExisting`
   consistently with existing export tools.

5. Atomic tools and workflow tool

   Add reusable atomic tools for advanced callers:

   - `CaptureDrawingExportStateTool`
   - `SetupDrawingViewsTool`
   - `ApplyDrawingExportStyleTool`
   - `SetDrawingExportBackgroundTool`
   - `RestoreDrawingExportStateTool`

   Add one higher-level workflow tool:

   - `ExportDrawingPackageTool`

   The workflow tool is the default recommended MCP path. Atomic tools exist for manual or staged workflows.

## Public Interfaces

Requests:

- `SetupDrawingViewsRequest`
  - `filePath`
  - `layerQueries`
  - `confirmedLayerFullPaths`
  - `preset`, default `Standard8`
  - optional `fitMarginPercent`, default `10`
- `CaptureDrawingExportStateRequest`
  - `filePath`
  - layer/filter scope for objects whose attributes may be temporarily changed
  - captures both object attribute snapshots and current background state
- `SetDrawingExportBackgroundRequest`
  - `filePath`
  - `snapshotId`
  - optional `color`, default white
- `ApplyDrawingExportStyleRequest`
  - `filePath`
  - `snapshotId`
  - optional object color override
- `RestoreDrawingExportStateRequest`
  - `filePath`
  - `snapshotId`
- `ExportDrawingPackageRequest`
  - `filePath`
  - `layerQueries`
  - `confirmedLayerFullPaths`
  - optional object filter fields matching existing filter contracts
  - optional temporary object color override
  - `outputDirectory`
  - export format selection for `pdf` and/or `jpg`
  - optional image size, PDF page size, DPI, and overwrite flag
  - optional list of view names; default all Standard 8 views

Responses:

- `DrawingViewSetupResponse`
  - status, file path, candidate layers when status is `NeedsLayerSelection`, resolved layers, created/updated view names, target object count, warnings
- `DrawingExportStateResponse`
  - snapshot id, background state captured flag, touched object count, warnings
- `DrawingExportPackageResponse`
  - status, candidate layers when status is `NeedsLayerSelection`, exported files, view names, format, output size, warnings, restore success flag

Structured statuses and error codes:

- `NeedsLayerSelection` response status with candidate layer payload
- `DRAWING_VIEW_TARGET_EMPTY`
- `DRAWING_EXPORT_RESTORE_FAILED`
- `DRAWING_EXPORT_SNAPSHOT_NOT_FOUND`
- `DRAWING_EXPORT_SNAPSHOT_STALE`
- Existing live-only errors such as `NO_ACTIVE_DOCUMENT`, `ACTIVE_DOC_UNSAVED`, `FILE_NOT_ACTIVE`, and `RHINO_MAIN_THREAD_BUSY`.

## Involved Files

Likely implementation areas:

- `src/MCP_Rhino.Server/Tools/`
- `src/MCP_Rhino.Server/Skills/`
- `src/MCP_Rhino.Server/Application/Services/`
- `src/MCP_Rhino.Server/Application/Interfaces/`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/`
- `src/MCP_Rhino.Server/Contracts/Requests/`
- `src/MCP_Rhino.Server/Contracts/Responses/`
- `src/MCP_Rhino.Server/Domain/Models/`
- `src/MCP_Rhino.Server/Domain/Enums/`
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/`
- `Project_Test/260429_TEST_drawing-export-workflow/`
- `Project_Exet/260429_EXET_drawing-export-workflow.md`

This first plan-doc step intentionally creates only this plan file and does not modify those implementation paths.

## Usage

Recommended workflow call:

1. Call `ExportDrawingPackage` with `filePath`, layer selection, output directory, and requested formats.
2. If response status is `NeedsLayerSelection`, show returned candidate layers to the user.
3. Call `ExportDrawingPackage` again with `confirmedLayerFullPaths`.
4. Receive exported PDF/JPG file paths and restore status.

Advanced staged usage:

1. `CaptureDrawingExportState`
2. `ApplyDrawingExportStyle`
3. `SetupDrawingViews`
4. `SetDrawingExportBackground`
5. Existing or new export calls
6. `RestoreDrawingExportState`

The staged path is useful for manual verification but should not be the default client workflow because callers can forget
to restore state.

## Test Plan

Add capability-owned test artifacts under `Project_Test/260429_TEST_drawing-export-workflow/`.

Required test entry points:

- CLI smoke slug: `drawing-export-smoke-test`
- Rhino command: `_McpDrawingExportSmoke`

CLI fallback smoke:

- Verify atomic tools and workflow tool reject with `LIVE_RHINO_REQUIRED` when run without Rhino plugin context.

Live smoke:

- Open a saved fixture document in Rhino.
- Call setup without layer selection and verify `Success=true`, response status `NeedsLayerSelection`, and candidate layers.
- Call setup with a known layer and verify 8 named views exist.
- Verify generated views fit the selected layer object bounding box.
- Verify PDF/JPG export captures the generated named views, not whichever Rhino viewport happened to be active.
- Verify generated output file names are deterministic and based on sanitized view names.
- Run workflow with a temporary color override and white background.
- Verify PDF and JPG outputs are created and non-empty.
- Verify changed object attributes are restored exactly after successful export.
- Force an export failure and verify object attributes and background still restore.
- Verify `RestoreDrawingExportState` rejects missing/stale/document-mismatched snapshot ids.

Build verification:

```powershell
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj --nologo
```

## Acceptance Criteria

- `Project_Plan/260429_PLAN_drawing-export-workflow.md` exists before code execution begins.
- Implementation adds both atomic tools and one workflow tool.
- Missing layer selection returns `Success=true` with status `NeedsLayerSelection` and candidate layers.
- View setup creates or updates the Standard 8 named views without duplicates.
- Views are fitted to visible, non-deleted objects on selected layers with the default 10 percent margin.
- PDF/JPG exports use the setup named views.
- Named views are explicitly bridged to capturable Rhino views or supported directly by the live exporter.
- Object display attributes and background restore exactly after success and failure.
- Atomic snapshot restore rejects missing, stale, or document-mismatched snapshots.
- CLI fallback and live smoke tests are documented in the matching EXET file.

## Risks and Rollback

- Rhino background APIs may affect application-level settings rather than document-local state. Mitigation: snapshot and
  restore explicitly, keep the mutation in one live accessor path, and report restore warnings.
- Named view APIs may preserve stale view metadata if only partially updated. Mitigation: replace or delete/re-add by name
  when necessary, and verify no duplicate Standard 8 names are created.
- Current PDF/JPG export resolves open viewport names, not `doc.NamedViews`. Mitigation: add an explicit named-view-to-capture
  bridge before calling the exporter or extend the exporter to support named views.
- `OperationResponse<T>.Fail(...)` cannot carry candidate layer data. Mitigation: model missing layer selection as a
  successful response state with candidate payload, not as a failed operation.
- Snapshot ids in atomic tools can become stale if Rhino document state changes or the server restarts. Mitigation: bind
  snapshots to file path plus active document identity, add TTL, and reject stale restore attempts.
- View fitting can fail for empty or hidden layer scopes. Mitigation: return `DRAWING_VIEW_TARGET_EMPTY` before mutation.
- Export failure must not leave temporary colors or white background behind. Mitigation: perform restore in `finally` and
  expose restore status in the response.

Rollback for implementation:

- Remove the new tools, contracts, services, live adapters, DI registrations, plugin smoke command, and test folder.
- Delete the matching EXET document if execution is abandoned before completion.
- This plan file can remain as a record of the rejected or deferred capability.

## Future Extensions

- Add custom view presets beyond Standard 8.
- Add caller-controlled view fit margin and per-view crop framing.
- Add layout/detail creation for sheet-based PDF output.
- Add SVG/AI/vector export if a stable RhinoCommon or command-macro path is validated.
- Add per-layer styling presets for lineweight, plot color, and display mode.
- Add batch export profiles stored in document user strings.

## Revision Record (2026-04-29)

- Clarified that current PDF/JPG export resolves open Rhino viewports, so implementation must bridge `NamedViewTable`
  entries to capturable views.
- Added explicit atomic state/style tools so staged workflows can restore temporary color overrides, not just background.
- Clarified that missing layer selection must be a successful structured response state because failed `OperationResponse`
  instances cannot carry candidate payloads.
- Added snapshot lifetime/document identity constraints and deterministic output naming.
