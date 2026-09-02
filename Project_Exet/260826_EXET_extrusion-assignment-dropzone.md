# EXET — Extrusion assignment drop zone

## Corresponding plan

- Plan: `Project_Plan/260826_PLAN_extrusion-assignment-dropzone.md`
- Execution date: 2026-08-26

## Related artifacts

- Focused test folder: `Project_Test/260826_TEST_extrusion-assignment-dropzone/`
- Existing assignment/formula/render regression:
  `Project_Test/260820_TEST_panel-extrusion-assignment/`
- Commit / PR: none created in this execution.

## Execution result / actual scope

- Changed ready extrusion tiles from click-capable buttons to drag-only preview tiles.
- Removed extrusion-profile drop acceptance and the associated event contract from the drawing
  canvas while retaining the existing cladding-material canvas drop behavior.
- Made the extrusion assignment section the sole receiver for extrusion-profile drag payloads.
- Replaced assigned-code chips with two-column preview cards containing the schedule crop, code,
  remove action, and a compact signed curve-length modifier input.
- Kept length modification curve-scoped. Every visible card reflects the selected curves' shared
  modifier; editing any card updates that same saved curve modifier and refreshes every card.
- Added a selection-aware available catalogue. Codes already represented in the assignment panel
  are removed from Drag Extrusions and return immediately when removed from the selection.
- Preserved additive assignments, parent dependency expansion, manual dependency removal, undo,
  frame typology updates, assignment payload schema v2, take-off formulas, bake, and sync behavior.
- Bumped and built PanelCladdingEditor `1.0.67`.

## Deviations from the plan

- The implementation does not add a separate drag-hover highlight. The native WPF copy cursor and
  assignment-panel boundary provide the accepted-target feedback without adding another UI state.
- Production activation was initially deferred because two Rhino processes were open at packaging
  time. After Rhino was closed, the exact validated staged bundle was activated registry-only.

## Problems found and fixed during execution

- Running several WPF smoke projects concurrently caused a transient shared XAML markup-cache file
  lock. The affected suite was rerun alone and passed; no production change was required.
- An initial inline-modifier guard would have made an intentionally blank Enter action unable to
  clear mixed modifiers. Enter now forces evaluation, while ordinary focus loss still ignores an
  untouched blank mixed-value field.
- The first `1.0.67` staging copy predated the final modifier adjustment. The package was rebuilt,
  revalidated, and restaged; the authoritative staged bundle is the later path recorded below.

## Test record

- `dotnet run --project Project_Test/260826_TEST_extrusion-assignment-dropzone/ExtrusionAssignmentDropZoneSmoke.csproj`
  passed all assignment-target, drag-only, preview-card, and filtering source contracts.
- `dotnet run --project Project_Test/260820_TEST_panel-extrusion-assignment/PanelExtrusionAssignmentSmoke.csproj -- <six-page schedule PDF>`
  passed assignment serialization, formulas, typology, bake/sync keys, all-page import, workbook
  round-trip, profile transfer/filtering, inline modifier mutation, and WPF render assertions.
- WPF UI, latest UI, direct interaction, and extrusion-sync regression projects all passed.
- The assigned-state render was written to
  `Project_Test/260820_TEST_panel-extrusion-assignment/bin/Debug/net8.0-windows/qa/extrusion-assignment-selected.png`.
- Direct Debug and Release RHP builds completed with zero warnings and zero errors.
- Package assembly metadata reports PanelCladdingEditor GUID
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, non-empty and distinct from MCP_Rhino.
- The packaged native-runtime probe loaded Skia/HarfBuzz and imported all 38 profiles from all six
  schedule pages.
- Packaged and authoritative staged RHP SHA-256 values both equal
  `8C86BAC88C68AD3218F102933E19E0DC1407E8D59658ED652A468CD8420B88E7`.

## Acceptance criteria alignment

- Assignment section receives profile drops: passed by routed-handler/source contract and clean WPF build.
- Canvas profile drops and catalogue click assignment removed: passed.
- Assigned image cards with remove and inline modifier controls: passed and visually rendered.
- Assigned profiles leave/re-enter the available catalogue: passed by live window-state assertions.
- Dependencies, modifier formulas, persistence, typology, bake, sync, and undo contracts retained: passed.
- Debug/Release build, package, GUID, native runtime, and real schedule import validation: passed.

## Rollback verification

Rollback is isolated to the four UI files in the plan plus the filtered collection binding. The
domain assignment schema and saved Rhino/workbook data were not changed, so reverting this UI flow
does not require data migration.

## Activation follow-up

After Rhino was closed, PanelCladdingEditor `1.0.67` was activated registry-only at
`C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.67`. Installer `Validate` mode passed,
the installed and packaged RHP hashes match exactly, and direct installed-assembly inspection again
confirmed plug-in GUID `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`.

## Conclusion

The requested assignment-only drag workflow, preview-card assignment list, inline curve modifier,
and available-catalogue exclusion behavior are implemented, regression-tested, installed, and
registry-validated as PanelCladdingEditor `1.0.67`.
