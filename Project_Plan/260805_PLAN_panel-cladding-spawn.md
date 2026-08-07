# Panel cladding spawn plan

## Background

The standalone `PanelCladdingEditor` Rhino 8 plug-in already reads one panel Brep, parses its
`CW_2.03_OFFSET_H<n>` / `CW_2.04_OFFSET_V<n>` grid, and associates each logical cell (`0A`, `0B`,
...) with a normalized cladding material code. The plug-in does not yet create document geometry
from that authored data.

The requested addition is a command-only workflow. It must not add or change the editor UI and must
remain independent of MCP_Rhino runtime or transport code.

## Goal

- Register `_PanelCladdingSpawn` as a Rhino command in the standalone plug-in.
- Accept one preselected panel Brep or prompt for one panel.
- Use the existing H/V offset and cladding-key contracts to divide the panel face into one cladding
  Brep per populated logical cell.
- Give every spawned object a `CW_1.02_CID` formed as `<panel PID>-<cell label>`, for example
  `W3_06_42-0A`, and use that CID as the Rhino object name.
- Copy the source panel's PID, release-number, and wall-type user-text entries to every spawned
  object while preserving the source key spelling.
- Store each object on `02_Material Surfaces::Surfaces-<family>::<material>`, including the required
  `GL* -> Surfaces-Glass` route and deterministic routes/fallbacks for other material prefixes.
- Complete one invocation as one Rhino Undo record and avoid creating duplicate CIDs.

## Architecture ownership

- `Domain/Models/PanelCladding`: spawn plans and results with no RhinoCommon dependency.
- `Application/Services/PanelCladding`: metadata recognition, CID construction, material-family
  routing, and per-cell spawn planning.
- `Infrastructure/Rhino/Live/PanelCladding`: local-frame extraction, exact Brep splitting, layer
  creation, object-attribute application, duplicate checks, and Undo coordination.
- `UI`: thin Rhino selection/command adapter only; no new window or control.
- `Project_Test/260805_TEST_panel-cladding-spawn/`: standalone planner/contract regression smoke.

## Key design

- Reuse `PanelCladdingKeyService` so spawn ordering remains column-first and bottom-to-top (`0A`,
  `0B`, `1A`, ...), matching the editor and workbook.
- Treat `PID` as required because it is the CID authority. Recognize metadata keys
  case-insensitively after removing punctuation/spaces: `PID`, `Release` / `Release Number`, and
  `Wall Type`. Preserve the exact source keys and values when writing spawned attributes.
- Ignore empty cladding cells instead of creating material-less surfaces; fail if no populated cell
  remains.
- Normalize material codes with the existing cladding normalization rule. Route `GL*` to
  `Surfaces-Glass`; route common metal, stone, terracotta, concrete, and wood prefixes to stable
  parent layers; route unrecognized codes to `Surfaces-Other` without guessing a false material
  family.
- Split a duplicate of the projectable panel Brep by planes located at every H/V offset in the same
  stable local frame used to interpret the offsets. Assign resulting Brep pieces to logical cells by
  area centroid in local XY. Require exactly one non-empty Brep result per populated cell; otherwise
  fail before adding document objects so folded, disconnected, or ambiguous panels do not create
  misleading cladding.
- Create missing layers parent-first. New objects receive the planned name, `CW_1.02_CID`, `Cladding`, and
  inherited metadata user text. Existing source geometry and attributes are not modified.
- Reject a run before mutation when any planned CID already exists in the document.

## Involved files

- `src/PanelCladdingEditor/Domain/PanelCladdingModels.cs`
- `src/PanelCladdingEditor/Application/Interfaces/IPanelCladdingServices.cs`
- new `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSpawnPlanningService.cs`
- new `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSpawnService.cs`
- new `src/PanelCladdingEditor/UI/PanelCladdingSpawnCommand.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorSmokeCommand.cs`
- `Project_Test/260805_TEST_panel-cladding-spawn/**`
- `Project_Exet/260805_EXET_panel-cladding-spawn.md`

## Usage

1. Select one or more authored panel Breps containing continuous H/V keys, populated cladding keys,
   `CW_1.01_PID`, `CW_1.05_RELEASE`, and `CW_1.07_WALL_TYPE`.
2. Run `_PanelCladdingSpawn`.
3. Review the command-line result count and the spawned material surfaces under
   `02_Material Surfaces`.

## Acceptance criteria

- `_PanelCladdingSpawn` is discoverable as a uniquely GUID-identified Rhino command.
- `W3_06_42` plus cell `0A` produces object name/user text
  `CW_1.02_CID=W3_06_42-0A`.
- PID, release-number, and wall-type entries retain their source keys and values on every surface.
- `GL01` is placed on `02_Material Surfaces::Surfaces-Glass::GL01`.
- Multiple cells with the same material reuse the same layer hierarchy.
- Empty cell values do not create geometry; missing required metadata, duplicate CIDs, unsupported
  panel projections, or ambiguous split results fail without adding cladding objects.
- One successful command invocation creates one Rhino Undo entry.
- Every selected panel is prepared before mutation, and one invocation spawns surfaces for the
  complete selection.
- Spawned objects use their leaf layer color; related material codes use deterministic,
  category-suitable color variants rather than white.
- Standalone Debug and Release builds pass with zero warnings/errors.
- The standalone regression smoke passes in Debug and Release and confirms no MCP dependency was
  introduced.

## Risks and rollback plan

- Plane splitting can expose disconnected or folded source topology. The adapter will reject any
  cell that cannot be represented by exactly one unambiguous Brep rather than merge unrelated
  pieces.
- A panel's metadata may use an unknown naming convention. The planning service will report the
  missing semantic field instead of silently writing an empty CID or losing requested inheritance.
- Rollback is Rhino Undo for successful runs. Code rollback is removal of the command, spawn
  service/interface, spawn models, and its dedicated test artifact; the existing editor remains
  untouched.

## Future extension directions

- Make additional material-family mappings project-configurable without adding editor UI.
- Add an explicit replace/update workflow with preview for already spawned CIDs.
- Support intentionally disconnected per-cell cladding as a separate, reviewed geometry contract.

## Revision record (2026-08-05)

Live use identified two required extensions:

- `_PanelCladdingSpawn` must accept every preselected panel Brep, or prompt for multiple Breps when
  nothing valid is preselected. All selected panels must be validated and prepared before mutation,
  then created in one Rhino Undo record. Duplicate CIDs within the batch or against the document
  must fail before object creation.
- Material leaf layers must not retain Rhino's default white display color. Each material family
  receives a muted base color suited to the category (cool cyan glass, neutral metal, warm stone,
  clay terracotta, gray concrete, brown wood, muted violet fallback). Material codes within one
  family receive deterministic small HSL lightness variations, so `GL01` and `GL02` remain visibly
  related but distinct. Existing material leaf layers are corrected to the deterministic color when
  used, and spawned objects explicitly use layer color.

The canonical metadata correction made during live validation is also fixed as an explicit schema
contract: only `CW_1.01_PID`, `CW_1.05_RELEASE`, and `CW_1.07_WALL_TYPE` are read and inherited;
all aliases are ignored.

The spawned CID follows the same canonical panel schema: write and query only `CW_1.02_CID`;
do not create or use the `CID` alias.

## Revision record (2026-08-07): stronger material variants

Live viewport use showed that related material leaf colors remain too similar. Retain one consistent
hue per material family but increase the alternating light/dark separation and use darker,
more-readable family bases. Calibrate glass explicitly so `GL01` is `#598390` and `GL02` is
`#466873`; later numeric variants use progressively lighter/darker values while remaining visibly
within the glass family. Apply the stronger alternating palette strategy to metal, stone,
terracotta, concrete, wood, and fallback materials as well. Existing material leaf layers continue
to be corrected to the deterministic color whenever spawn uses them.
