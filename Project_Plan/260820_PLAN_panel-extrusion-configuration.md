# PLAN - Panel extrusion configuration

Date: 2026-08-20

## Background

The first extrusion-assignment release imports only the framing page and assumes every extracted
profile is a ready-to-assign 1D item. Project take-off requires all schedule pages, page-derived
categories, explicit 1D/0D configuration, dependencies, quantities/spacing, and per-curve length
modifiers that bake as individual Rhino key/value attributes.

## Goals

- Import every schedule page containing extrusion die-number headers.
- Shorten source `ALU-H0579` to base `H0579`; create a ready code only after selecting 1D or 0D.
- Divide setup into an unconfigured pool and ready catalogue, grouped by PDF page category.
- Configure 1D quantity and optional parent dependency.
- Configure 0D as fixed quantity or spacing along modified curve length.
- Store a signed curve-length modifier per frame/intermediate assignment target.
- Bake one Rhino user-text key per ready profile with deterministic formula values.
- Preserve manual dependency removal by expanding dependencies only when a root profile is newly
  assigned to a curve.

## Architecture ownership

- `Domain/`: profile definition, calculation mode, assignment definitions, and curve modifiers.
- `Application/`: validation/serialization, formula evaluation, typology identity, and sync planning.
- `Infrastructure/File/`: all-page PDF extraction and workbook catalogue schema.
- `Infrastructure/Rhino/`: baked/synchronized curve attribute reads and writes.
- `UI/`: pool/ready setup, category grouping, configuration tabs, dependency selection, and modifier input.
- `Packaging/PanelCladdingEditor/`: versioned release and registry-only activation.

## Key design

1. Each extracted row retains `SourceCode`, shortened `BaseCode`, page number, category, description,
   and thumbnail. An empty ready code means the item remains in the pool.
2. Ready codes are `<dimension>-<base>`, for example `1D-H0579` or `0D-H0579`.
3. 1D uses calculation mode `Length`; blank quantity means 1. A quantity greater than one produces
   `LL*n`, or `(LL+modifier)*n` when modified.
4. 0D fixed quantity produces a numeric value. 0D spacing produces `LL/distance`, or
   `(LL+modifier)/distance` when modified. The final `/2` in the request conflicts with the stated
   every-24 rule; this plan implements `(LL+4)/24`.
5. Parent dependency is a ready profile code. Dependency expansion is performed only as part of a
   newly added root assignment; removing a dependency chip later is respected.
6. Assignment payload schema v2 stores assigned codes, the definitions actually assigned to the
   panel, and signed per-curve modifiers. Schema v1 remains readable as legacy 1D length definitions.
7. Baked curves retain the legacy `Extrusions` summary for compatibility and additionally write
   individual `0D-*`/`1D-*` keys. Sync deletes stale individual keys before writing desired values.
8. Frame typology hashes the complete v2 assignment payload, so definition values, dependencies,
   and modifiers affect `CW_1.5D_FRAME TYPOLOGY`.

## Involved files

- `src/PanelCladdingEditor/Domain/PanelCladdingModels.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelFrameAssignmentService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingExtrusionPlanningService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSurfaceSyncPlanningService.cs`
- `src/PanelCladdingEditor/Infrastructure/File/PdfFrameExtrusionScheduleImporter.cs`
- `src/PanelCladdingEditor/Infrastructure/File/OpenXmlPanelCladdingWorkbookRepository.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSurfaceSyncRepository.cs`
- `src/PanelCladdingEditor/UI/ExtrusionSetupDialog.xaml(.cs)`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml(.cs)`
- `Project_Test/260820_TEST_panel-extrusion-configuration/`

## Usage

Open extrusion setup, extract the schedule, configure pool profiles as 1D or 0D, and confirm the
catalogue. In extrusion view, click/drag ready profiles to curves, optionally set a signed LL modifier,
then save extrusion data. Spawned or synchronized curves expose ready codes as Rhino attributes.

## Acceptance criteria

- The supplied six-page PDF imports and categorizes profiles from all six pages.
- `ALU-H0579` is configured as `1D-H0579` or `0D-H0579`.
- Pool and ready sections render with page categories.
- All formula examples, including modifier/quantity combinations, match this plan.
- Parent assignment is automatic once and manual removal remains removed.
- Save/reload, merge/explode, delete, insertion/collapse, undo, spawn, and sync preserve valid state.
- Debug/Release builds, focused tests, visual renders, package GUID validation, and registry-only
  installer regression pass.

## Risks and rollback

- Existing v1 assignment/workbook data must migrate without data loss; retain compatible reads.
- Formula text is contractual; invariant numeric formatting and parentheses are tested explicitly.
- All-page cell geometry may vary; render/crop every imported page and inspect representative output.
- Rollback is installation of the prior registry-owned version while Rhino is closed.

## Future extensions

- Richer take-off expressions and unit-aware spacing.
- Multiple independent ready configurations derived from one source profile.
- Catalogue revision/version comparison across project schedule updates.
