# Panel Extrusion Assignment

## Background

The extrusion view currently supports frame/intermediate curve selection, merge, explode, hide,
delete, and mullion editing, but its assignment section is a placeholder. Production take-off uses
the baked `FRM_*` and `INT_*` curves, and one physical curve may carry more than one length-based
main-frame or associated extrusion code. Project schedules are normally supplied as PDFs containing
the source die number, part description, and a dimensioned profile drawing.

The project already has a workbook-backed cladding material catalogue and drag/drop assignment UX.
The extrusion workflow should follow that interaction model while preserving the important
differences: profile thumbnails instead of color swatches, additive multi-code assignment instead of
exclusive material assignment, and a deterministic frame typology rather than a cladding type.

## Goal

- Add a project extrusion catalogue stored in the existing project workbook.
- Import main-frame entries from a selected PDF schedule, normalize `ALU-H0651` to
  `1D-ALU-H0651`, and retain a readable profile thumbnail and description.
- Prefer pages identified as framing schedules; if a vendor PDF has no framing-labelled page, use
  all pages containing extrusion schedule entries and leave the resulting catalogue editable.
- Add image-over-text extrusion tiles to the main editor and an extrusion setup window based on the
  material setup workflow.
- Support click or drag/drop additive assignment of one or more catalogue codes to selected `FRM`
  and `INT` curves, with explicit removal/clear controls.
- Persist assignments canonically and write them onto baked curves for length take-off.
- Calculate a frame typology from segment, merge, and hide masks plus all curve-code assignments,
  and store the visible value at the exact panel user-text key `CW_1.5D_FRAME TYPOLOGY`.

## Architecture Ownership

- `Domain`: catalogue records, frame-assignment state, frame-typology identity, save request/result
  additions, and curve-plan assignment data.
- `Application`: assignment validation/serialization, deterministic typology hashing, save
  orchestration, and extrusion spawn metadata planning.
- `Infrastructure/File`: PDF text/layout extraction and profile rendering; transactional Open XML
  read/write of the `Extrusions` worksheet.
- `Infrastructure/Rhino`: load persisted assignment state through the live panel layout and commit
  the new panel attributes through the existing stale-check/Undo boundary.
- `UI`: setup dialog, thumbnail tiles, drag/drop, multi-code editing, assignment-aware curve labels,
  and extrusion-view typology preview.
- `Packaging`: include the managed/native PDF rendering dependencies and increment the standalone
  plug-in package version without changing its plug-in GUID.

No MCP server tool, Router path, command registration, or Rhino UI automation is added.

## Key Design

1. Add `PdfPig.Rendering.Skia` as the PDF parser/renderer. It exposes positioned PDF text and a
   Skia page renderer without requiring an external Poppler/Python installation in Rhino.
2. Detect schedule records from the `ISLAND DIE NUMBER` / `ALU-*` text and nearby `PART NAME`
   content. Group header locations into schedule rows/columns, derive each schedule cell, render the
   page, and crop the drawing region into a bounded PNG thumbnail.
3. Store catalogue rows on an `Extrusions` worksheet with code, source code, description, source
   PDF, page number, and base64 PNG. Writes use the repository's existing prepare/commit temp-file
   transaction so a failed update does not corrupt the workbook.
4. Use canonical catalogue codes matching `1D-[A-Z0-9][A-Z0-9-]*`. The setup dialog may remove an
   imported entry before confirmation; duplicate die numbers collapse deterministically.
5. Represent panel assignments by stable targets: `FRM_0` through `FRM_3`, plus indexed horizontal
   and vertical segment coordinates. Persist a versioned, sorted JSON payload at
   `CW_2.09_FRAME_ASSIGNMENTS`; omit/delete that attribute when no codes are assigned.
6. A click applies a code to every selected curve. A drop targets the curve under the pointer. Both
   are additive and idempotent. Removing a chip removes that code from all selected targets.
7. A merged intermediate curve applies assignments to all member atomic segments. Merging unions
   existing member codes and writes the union back to every member, so one baked continuous curve
   has one unambiguous take-off code set. Exploding retains those per-segment codes.
8. Deleting a segment deletes its assignments. Inserting/collapsing tracks remaps coordinate keys;
   when two perpendicular bays collapse into one, their code sets are unioned. Dimension-only edits
   retain assignments because coordinate indices remain stable.
9. Baked curve planning writes a sorted semicolon-separated `Extrusions` user-text value when the
   curve has assigned codes. Hidden/missing curves are not baked, matching existing topology
   behavior. The panel remains the source of truth for editable assignment state.
10. Calculate the frame typology from a versioned canonical payload containing dimensions/offsets,
    the full encoded segment/merge/hide masks, and canonical frame/segment assignments. The visible
    format mirrors the marker-free cladding type shape:
    `<system>-<columns>X<rows>-<digest8>`.
11. Write `CW_1.5D_FRAME TYPOLOGY` only when at least one extrusion assignment exists; delete it
    together with the assignment payload when the panel returns to an unassigned state. Default
    topology masks remain sparse under the existing 2.05-2.07 rules.
12. The extrusion view shows its own `FRAME TYPOLOGY` calculated-key section. The cladding type
    section remains visible only in cladding view.

## Files Involved

- `src/PanelCladdingEditor/Domain/PanelCladdingModels.cs`
- `src/PanelCladdingEditor/Application/Interfaces/IPanelCladdingServices.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelFrameAssignmentService.cs` (new)
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelFrameTypologyService.cs` (new)
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingExtrusionPlanningService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladdingKeyService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladdingSaveService.cs`
- `src/PanelCladdingEditor/Infrastructure/File/PdfFrameExtrusionScheduleImporter.cs` (new)
- `src/PanelCladdingEditor/Infrastructure/File/OpenXmlPanelCladdingWorkbookRepository.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/LivePanelCladdingRepository.cs`
- `src/PanelCladdingEditor/UI/PanelFrameExtrusion.cs` (new)
- `src/PanelCladdingEditor/UI/ExtrusionSetupDialog.xaml` (new)
- `src/PanelCladdingEditor/UI/ExtrusionSetupDialog.xaml.cs` (new)
- `src/PanelCladdingEditor/UI/PanelCladdingEditorController.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorCommand.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingGridCanvas.cs`
- `src/PanelCladdingEditor/PanelCladdingEditor.csproj`
- `Packaging/PanelCladdingEditor/Build-PanelCladdingEditorPackage.ps1`
- `Packaging/PanelCladdingEditor/package-manifest.json`
- `Packaging/PanelCladdingEditor/README.md`
- `Project_Test/260820_TEST_panel-extrusion-assignment/` (new)
- `Project_Exet/260820_EXET_panel-extrusion-assignment.md` (after verification)

## Usage

1. Open `PCEditor`, switch to `Extrusion view`, and choose `Setup` under `DRAG EXTRUSIONS`.
2. Choose the project workbook and extrusion-schedule PDF, then choose `Extract profiles`.
3. Review the image/code tiles, remove any entry that is not a main-frame item, and choose
   `Confirm` to save the project catalogue.
4. Select one or more `FRM`/`INT` curves and click a tile, or drag a tile directly onto one curve.
   Repeat to add multiple 1D codes. Use an assignment chip's remove control or `Clear all` to remove
   codes.
5. Choose `Save Extrusions` or `Save Both`. The panel receives the assignment payload and frame
   typology; a later `PCSpawnCrv`/curve spawn writes the applicable `Extrusions` codes onto each
   baked curve.

## Acceptance Criteria

- The sample framing schedule imports `1D-ALU-H0651` with a non-empty, visually recognizable PNG
  profile crop and its part description.
- The setup window accepts a PDF path, displays image-over-text catalogue tiles, and round-trips the
  catalogue through the `Extrusions` workbook sheet.
- The main extrusion view displays the catalogue, supports click and drag/drop assignment, and
  supports more than one unique code per curve without duplicating a code.
- Frame and intermediate assignments survive save/reload. Delete, split, collapse, merge, explode,
  hide, and dimension edits follow the remapping rules above.
- Assignment JSON is deterministic, versioned, canonical, and absent when empty.
- Changing any one of segment mask, merge mask, hide mask, or a main-frame assignment changes the
  calculated frame typology; repeating the same state produces the same value.
- The calculated value is stored at exactly `CW_1.5D_FRAME TYPOLOGY` and uses the marker-free
  `<system>-<columns>X<rows>-<digest8>` form.
- Baked curve plans include sorted assigned 1D codes in `Extrusions`; an unassigned curve does not
  receive that attribute.
- Existing sparse topology behavior, cladding assignments/types, material catalogue, and curve
  spawn behavior remain compatible.
- Focused Debug/Release tests, relevant existing regressions, Debug/Release plug-in builds, direct
  compiled-RHP assembly GUID validation, package validation/installation, and `git diff --check`
  pass.

## Risks And Rollback

- PDF schedules are presentation documents rather than structured data. Vendor title blocks or
  headers may differ; the importer reports zero/ambiguous results without changing the workbook,
  and setup review allows unwanted entries to be removed.
- PDF rendering adds Skia/native runtime files. Packaging must preserve publish subdirectories and
  the dependency manifest; package validation will reject missing files.
- Base64 thumbnails increase workbook size. Imports cap the rendered thumbnail dimensions and PNG
  size; oversized/invalid thumbnails fail before commit rather than creating unreadable cells.
- Assignment remapping is sensitive to grid topology. Focused tests cover inserted/collapsed tracks,
  merged runs, and deletion. Save rejects out-of-range or inconsistent external payloads.
- Rollback removes the new UI/services/catalogue methods and restores the prior package version.
  Existing 2.09/1.5D attributes and the `Extrusions` worksheet are ignored by the prior plug-in and
  can be deleted independently; existing cladding/topology attributes are unchanged.

## Future Extensions

- Add schedule-page/category filters and manual crop adjustment for nonstandard vendor PDFs.
- Add linked gaskets/fasteners/other length-based items as explicit catalogue relationships.
- Add automated profile-geometry comparison and aliases between vendor die numbers.
- Feed the canonical frame typology and assigned main profiles into the planned PNL calculation
  workflow.
