# Panel Cladding Material Catalog

## Background

The latest WPF editor still treats Excel as a catalogue of rendered cladding-type preview sheets.
The BKT `WF Typology.xlsx` currently contains one hidden `_CLADDING_INDEX` sheet and 58 generated
type sheets. The editor also builds its drag-material list from the active panel only, so material
setup is not shared across the project. Real-window review additionally found that parent-boundary
dashes are offset beside the solid grid separator, selected cells need a bright-red color replacement
rather than a blended overlay, material chips are too tall, and related form controls do not share an
explicit height.

## Goal

Make the workbook a project material catalogue rather than a panel-preview/type database. Store the
calculated cladding type, signature, normalized cell assignments, and offsets exclusively as Rhino
panel key/value data. Read and write one managed `Materials` worksheet for project-wide material code,
description, category, and color. Migrate the BKT workbook and update the WPF design and canvas behavior
to match the eight user requests.

## Architecture Ownership

- `Domain/PanelCladdingModels.cs`: framework-neutral material catalogue records and results.
- `Application/Interfaces/IPanelCladdingServices.cs`: material-catalogue repository contract.
- `Application/Services/PanelCladdingSaveService.cs`: panel-only cladding identity/key-value save.
- `Application/Services/PanelCladding/PanelCladdingSurfaceSyncService.cs`: panel/surface synchronization
  without workbook type-sheet generation.
- `Infrastructure/File/OpenXmlPanelCladdingWorkbookRepository.cs`: project `Materials` sheet read/write,
  atomic replacement, legacy managed-type cleanup, and preservation of unrelated sheets.
- `UI/PanelCladdingEditorController.cs`: catalogue read/write orchestration.
- `UI/MaterialSetupDialog.*`: workbook selection/status plus project material editing.
- `UI/PanelCladdingEditorWindow.*`: catalogue-driven material list, reordered sidebar, compact chips,
  simplified Cladding Type card, and equal-height controls.
- `UI/PanelCladdingGridCanvas.cs`: bright-red selection replacement and shared-edge dash rendering.
- `Project_Test/260813_TEST_panel-cladding-material-catalog/`: focused persistence, WPF, rendering,
  migration, and workbook artifact verification.

## Key Design

1. Selected cladding cells use an opaque bright-red fill (`#FF1F1F`) with white labels. The stored
   material color is not blended into the selected preview.
2. A parent/child shared boundary is drawn at the true midpoint between adjacent cell rectangles.
   The solid separator is first cleared, then replaced with one centered dashed stroke so no parallel
   solid fragment remains.
3. `FormTextBox`, workbook browse controls, parent reference, Exit, and Save & sync use an explicit
   40-DIP height.
4. The sidebar order becomes Current Panel → Cell Assignment → Drag Materials → Cladding Type.
   Drag-material swatches shrink to text height and chips shrink around their content.
5. The main window keeps only the calculated key under a `CLADDING TYPE` section. Workbook selection,
   material catalogue status, and browsing move to Material Setup.
6. Loading a panel reads the selected project workbook's `Materials` sheet. The active panel's codes
   are merged as non-destructive fallback entries so unknown legacy assignments remain usable.
7. Accepting Material Setup atomically writes the complete project catalogue and stores the selected
   workbook path in the Rhino document. Codes are case-insensitive and unique; colors are `#RRGGBB`.
8. Normal panel save computes the same deterministic cladding identity but writes only Rhino object
   keys. It does not render a preview or open/update Excel. Workbook presence is not required.
9. Surface sync computes and writes panel identities directly and no longer creates, reuses, or prunes
   workbook type sheets.
10. On first catalogue save, `_CLADDING_INDEX` and sheets referenced by that index are removed. Sheets
    not owned by that legacy index are preserved. The BKT migration keeps a recoverable backup.

## Files Involved

- `src/PanelCladdingEditor/Domain/PanelCladdingModels.cs`
- `src/PanelCladdingEditor/Application/Interfaces/IPanelCladdingServices.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladdingSaveService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSurfaceSyncService.cs`
- `src/PanelCladdingEditor/Infrastructure/File/OpenXmlPanelCladdingWorkbookRepository.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorCommand.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorController.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml.cs`
- `src/PanelCladdingEditor/UI/MaterialSetupDialog.xaml`
- `src/PanelCladdingEditor/UI/MaterialSetupDialog.xaml.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingGridCanvas.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingSyncFromSurfacesCommand.cs`
- `Project_Test/260813_TEST_panel-cladding-material-catalog/`
- `V:/01 Project Folders/P00020 BayHealth Kent Tower BKT/03-Design-Eng/03-BIM/05-Wireframe/WF Typology.xlsx`

## Usage

Open `_PanelCladdingEditor`. The drag-material list loads from the project workbook. Choose **Setup**
to select/change the workbook and edit the catalogue. **Use materials** writes the `Materials` sheet.
Assign materials or parent references and use **Save & sync** to write the panel key/value set to Rhino;
no cladding-type worksheet is generated.

## Acceptance Criteria

- Selection is an opaque bright-red replacement with readable white text.
- Parent shared edges show one centered dashed boundary without an adjacent solid segment.
- Parent/workbook/browse/footer controls are exactly the same height.
- Material chips and swatches are visibly compact and Drag Materials is below Cell Assignment.
- Material Setup owns workbook selection and project-catalog status.
- The main window has `CLADDING TYPE`, not `EXCEL TYPOLOGY RECORD`, and contains no workbook field.
- Panel save succeeds without a workbook and writes type/signature/cell values only to Rhino attributes.
- Surface sync does not produce workbook type sheets.
- The BKT workbook contains a verified `Materials` sheet populated from used BKT codes/colors and no
  legacy managed type/index sheets, with a recoverable pre-migration backup.
- Debug/Release builds, focused smokes, affected regressions, RHP identity, package validation, and
  visual/workbook inspection pass.

## Risks And Rollback

- Removing 58 generated BKT tabs is destructive. Create and verify a timestamped sibling backup before
  replacing the workbook; preserve unrelated sheets and record exact hashes.
- Existing panels can contain material codes absent from `Materials`. Merge fallback records in memory
  and allow Material Setup to persist them.
- Excel may lock the workbook. Fail before mutation with a clear close-and-retry message.
- Rollback consists of the previous installed plug-in version and the timestamped BKT workbook backup.

## Future Extensions

- Add optional material specification URLs and manufacturer fields to the catalogue.
- Add controlled material rename/deprecation with a project-wide panel usage audit.
- Replace the temporary structural-preview boundary with a dedicated Rhino geometry apply contract.
