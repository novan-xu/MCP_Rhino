# 260804_PLAN_panel-cladding-type-editor

## Background

The current WT-01 workflow has a shared Rhino 8 Grasshopper Python script beside the project
document. The script accepts Rhino-referenced Breps, guide curves, and a Boolean toggle; calculates
orthogonal mullion offsets; and commits the following object user text directly to the referenced
Rhino panels:

- `CW_2.03_OFFSET_H0`, `CW_2.03_OFFSET_H1`, ... for bottom-to-top horizontal cuts
- `CW_2.04_OFFSET_V0`, `CW_2.04_OFFSET_V1`, ... for left-to-right vertical cuts
- `CW_4.{column:00}_CLADDING_{column}{row}` for cladding cells ordered bottom-to-top and
  left-to-right, for example `CW_4.00_CLADDING_0A`

The current script deliberately has no output. It mutates `RhinoObject.Attributes` only when the
toggle is true and only when its Brep inputs retain Rhino reference ids.

The next workflow must let a user select one panel in Rhino, open a focused visual editor, see the
panel subdivided from its stored offset keys, enter one cladding code per cell, save a deterministic
panel type back to Rhino, and create or reuse a corresponding worksheet in a designated `.xlsx`
workbook. The workbook is a communication artifact for users who do not run Rhino.

The visual editor must not reduce every panel to a flat rectangle. Curtain-wall panels may be planar
or curved. The preview therefore needs to show the selected Brep's actual depth/curvature in a fixed
orthographic axonometric view while preserving a reliable logical relationship between visible cells
and their `0A`, `0B`, ... attribute keys.

The repository currently has no focused panel-cladding UI. Its general XLSX writer creates new basic
workbooks and does not read an existing workbook, preserve unrelated tabs, upsert one visual type
tab, maintain a signature index, or embed an axonometric preview image. The retired chat,
Companion, and general-purpose panel paths must remain retired; this capability is a local,
domain-specific Rhino editor and does not create any MCP transport or chat surface.

The current Grasshopper offset script also validates all panel vertices against one plane. That is
appropriate for the tested planar WT-01 panels but is not sufficient for the full curved-panel
workflow. Curved-panel offset generation must retain the user's orthogonal-distance definition and
must not silently switch to UV distance or surface arc length.

## Goal

- Add a focused Rhino command that uses one preselected panel Brep or prompts for one panel when
  nothing valid is preselected.
- Open a small modeless Eto editor window without starting an external process, MCP session, chat
  surface, bridge, Companion, or alternate connection path.
- Read the selected live panel's H/V offset keys, cladding keys, geometry, model units, tolerance,
  and stable local frame.
- Build a logical grid whose columns run left-to-right and whose rows run bottom-to-top, matching the
  existing `0A`, `0B`, ... key contract exactly.
- Render an orthographic axonometric preview from the selected Brep rather than a perspective image
  or a flat schematic.
- Make planar and curved panels visibly distinct through actual silhouette/depth, mesh shading,
  boundary/grid deformation, and a `Planar` or `Curved` classification badge.
- Let the user select cells, type cladding codes, navigate by keyboard, and review the exact key
  associated with each cell.
- Calculate one deterministic type identity from physical panel layout, geometry class/shape,
  offsets, and normalized cladding values.
- Reuse the same type code for identical signatures and generate a different type code whenever a
  significant offset, geometry-shape, grid, or cladding value changes.
- Commit the type code, full signature, and cladding values to the selected panel in one Rhino Undo
  record.
- Store a designated `.xlsx` workbook path per Rhino document and let the user browse/change it in
  the editor.
- Create one visible, panel-shaped worksheet per unique type, including a proportional cell matrix,
  metadata, and an embedded copy of the axonometric preview.
- Preserve unrelated workbook tabs and reuse an existing type tab when the full signature already
  exists.
- Fail safely when the workbook is locked, read-only, malformed, or cannot be updated; do not leave
  Rhino with partially committed attributes.
- Extend or replace only the planar assumptions in the shared Grasshopper offset script that block
  the same class of projectable curved panels, while preserving its inputs, zero-output contract,
  referenced-object requirement, attribute keys, and toggle behavior.
- Keep the installed Rhino plug-in startup shape and Router-only external connection contract
  unchanged.

## Non-Goals

- Do not restore any old `_Mcpchat`, Companion, chat panel, fixed bridge, embedded agent, or
  panel-bound MCP connection path.
- Do not add an MCP Tool, Skill, Agent, Resource, alternate transport, or background daemon for this
  local editor in the first version.
- Do not open the editor automatically on every ordinary Rhino selection event. Selection remains a
  normal Rhino action; a toolbar button, alias, or command explicitly opens the editor.
- Do not use Microsoft Excel COM automation or require Excel to be installed.
- Do not support `.xls`, `.xlsm`, Google Sheets, SharePoint coauthoring, or cloud synchronization in
  the first version.
- Do not treat the spreadsheet as the editable source of truth. In this workflow, the Rhino editor is
  the authoring surface and the workbook is the durable human-readable type catalog.
- Do not overwrite, delete, rename, or reformat unrelated workbook worksheets.
- Do not create a new worksheet for every panel instance. One full type signature owns one worksheet;
  any number of panels may reuse that type.
- Do not promise an exact developed/flattened surface pattern. The preview is a controlled
  orthographic axonometric representation, not fabrication unfolding.
- Do not infer an apparently accurate grid on folded, undercut, self-overlapping, or otherwise
  non-projectable geometry. Such geometry must receive a bounded warning or an explicit unsupported
  result.
- Do not silently redefine H/V values as UV parameter distance or surface arc length. They remain
  orthogonal distances in the panel reference frame.
- Do not let a short type-code hash stand alone as identity. The full canonical signature must be
  stored and checked in Rhino and the workbook index.

## Architecture Ownership

- `Domain/Models/PanelCladding/`: immutable logical models for panel layout, cells, offsets,
  curvature/depth samples, canonical signatures, type identities, and workbook type records. These
  models contain no RhinoCommon, Eto, Open XML, MCP, or filesystem dependencies.
- `Domain/Enums/`: geometry classification and preview/result enums such as `Planar`, `Curved`, and
  `UnsupportedProjection` when a separate enum improves clarity.
- `Application/Services/PanelCladding/`: deterministic orchestration for attribute-key parsing,
  layout validation, cladding normalization, signature calculation, type-code reuse, save preflight,
  and coordinated Rhino/workbook commit.
- `Application/Interfaces/`: abstractions for reading/writing one live panel, producing preview
  geometry, rendering preview images, and reading/upserting the type workbook.
- `Infrastructure/Rhino/Live/PanelCladding/`: RhinoCommon-backed panel selection resolution,
  object-attribute snapshots, local-frame derivation, Brep/mesh cloning, projectability checks,
  curved grid projection, and one-record live attribute mutation.
- `Infrastructure/Plugin/PanelCladding/`: the Rhino command, modeless Eto window, canvas, cell editor,
  controller, and UI-only state. It does not own type-signature rules or workbook package logic.
- `Infrastructure/File/PanelCladding/`: an existing-workbook `.xlsx` repository that preserves
  unrelated sheets, manages the hidden type index, writes visible type sheets, embeds the generated
  preview, and replaces the workbook safely through a same-directory temporary file.
- `Server/DependencyInjection.cs`: registrations required by the command/controller. No Tool or
  Resource registration is added.
- `Infrastructure/Plugin/PluginLoadContext.cs`: continue sharing RhinoCommon, Rhino.UI, Eto, and
  Eto.Wpf with Rhino's default load context. Any new workbook dependency must be explicitly tested
  in both the command/default and isolated server load contexts.
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`: Rhino UI/Eto references and a pinned workbook
  package dependency if the Execute investigation confirms that direct Open XML package editing is
  safer than extending the BCL ZIP writer.
- The shared project script
  `V:\01 Project Folders\P00020 BayHealth Kent Tower BKT\03-Design-Eng\03-BIM\05-Wireframe\02_grasshopper\WF setup\WT01_panel_mullion_attributes.py`:
  curved/projectable-panel compatibility only; its public Grasshopper contract remains unchanged.
- `Project_Test/260804_TEST_panel-cladding-type-editor/`: unit, workbook, render, CLI smoke, and live
  Rhino smoke artifacts created during Execute.
- `Project_Exet/260804_EXET_panel-cladding-type-editor.md`: actual implementation, deviations,
  build/test evidence, screenshots, and rollback results recorded after Execute.

This feature is not an MCP capability surface. It is a local Rhino plug-in command backed by Domain,
Application, live Rhino infrastructure, and file infrastructure. Adding it must not change the
Router's public tools/resources, endpoint lifecycle, discovery registry, or installed client
configuration.

## Key Design

### 1. Rhino Entry Point And Selection Workflow

- Add one production command with the working English name `McpPanelCladdingEditor`.
- The command first consumes exactly one valid preselected Brep. If there is no valid preselection,
  it prompts the user to select one Brep. Multiple valid preselected Breps produce a clear choice or
  a single-object prompt; the first object must never be chosen silently.
- The command opens or focuses one modeless Eto tool window scoped to the selected Rhino document.
  Opening another document must not retarget the existing window silently.
- The editor pins the object id it loaded. Ordinary selection changes do not overwrite unsaved cell
  edits. A visible `Load Selected Panel` action intentionally replaces the current editor target
  after an unsaved-change check.
- The editor exposes `Save Type`, `Reload Panel`, `Choose Workbook`, and `Close`. It shows the active
  document name, selected object id/name, geometry classification, grid size, and workbook path.
- The command and window are entirely local. They must not call `MCP_Rhino.Router.exe`, create a pipe,
  load an LLM, or register any general-purpose panel/Companion command.

### 2. Existing Attribute Contract

- Parse dynamic H keys case-insensitively with the canonical write form
  `CW_2.03_OFFSET_H{index}`.
- Parse dynamic V keys case-insensitively with the canonical write form
  `CW_2.04_OFFSET_V{index}`.
- Sort numeric suffixes, reject duplicates/gaps that make the grid ambiguous, and verify every offset
  is strictly inside the panel extent within model tolerance.
- The number of logical rows is `H count + 1`; the number of logical columns is `V count + 1`.
- Rows are labeled `A`, `B`, ... bottom-to-top, including `AA`, `AB`, ... after `Z`.
- Columns are numbered `0`, `1`, ... left-to-right.
- A cell at column `c`, row label `r` maps to
  `CW_4.{c:00}_CLADDING_{c}{r}`. The UI displays both its short cell label and full key.
- Existing nonblank cladding values are loaded into the editor. Newly created blank keys are shown as
  empty editable cells.
- First-version type creation requires every logical cell to contain a non-whitespace cladding code.
  Intentional void cells require a future explicit sentinel/policy rather than an indistinguishable
  empty string.
- Proposed type metadata keys are:
  - `CW_4.00_CLADDING_TYPE`
  - `CW_4.00_CLADDING_SIGNATURE`
- The key strings must be centralized as one contract class so a later approved naming change cannot
  drift across Rhino, UI, signature, tests, and workbook output.

### 3. Panel Descriptor And Stable Local Frame

- The live reader captures one immutable descriptor on Rhino's UI thread: duplicated attributes,
  duplicated Brep, bounded display mesh, document units/tolerance, object id/name/layer, local frame,
  extents, offsets, cell values, and geometry classification inputs.
- Prefer the project's existing `Plane` geometry user string when it parses successfully, so the
  editor and Grasshopper script share the same point-0/right/up convention.
- For a planar Brep without a valid stored plane, derive the frame from the first reliable planar
  face, align local up with model gravity, and stabilize right orientation consistently with the
  current script.
- For a curved Brep without a stored plane, fit a reference plane to bounded Brep/mesh samples,
  gravity-align its up direction, and stabilize handedness from the dominant horizontal extent.
- Point 0 remains the lower-left corner of the orthogonal panel envelope in this reference frame.
- Panel width/height and H/V offsets remain orthogonal projected distances. The preview may bend in
  depth, but key meaning must not change with curvature.
- The descriptor records why a frame was accepted, plus bounded warnings for fallback derivation.
  No frame ambiguity is hidden from the user.

### 4. Curved Geometry Classification And Projectability

- Classify a panel as planar when exact/fit-plane checks and sampled depth residuals remain within a
  tolerance derived from document tolerance and panel size.
- Classify it as curved when a stable frame exists, its geometry is not planar, and the panel remains
  single-valued under projection along the frame normal over the logical grid domain.
- Detect unsupported projection when sampled normal rays have no consistent hit, multiple separated
  valid hits, folded topology, severe undercut, or disconnected regions that make one cell-to-surface
  mapping misleading.
- A curved/projectable panel is supported even when it has multiple Brep faces, provided the bounded
  normal-ray mapping is single-valued and continuous enough for the logical grid.
- Unsupported geometry may show the raw shaded Brep with a warning, but `Save Type` must remain
  disabled until the cell mapping is reliable. The editor must not fabricate a flat successful grid.
- Store bounded planarity/depth diagnostics in the descriptor and surface them in UI details and
  smoke-test output.

### 5. Fake 3D Orthographic Axonometric Preview

- Implement the preview as an Eto `Drawable` using a bounded cloned mesh and deterministic projection
  math. Do not create a second Rhino viewport or use perspective projection.
- Transform model geometry into the panel-local frame, then apply one fixed orthographic axonometric
  camera rotation. The exact yaw/pitch becomes a tested constant and the preview auto-fits with a
  stable margin.
- Depth-sort mesh triangles and apply simple normal-based light/dark shading, silhouette edges, and
  grid-line contrast. This is sufficient to make planar and curved geometry visually distinct
  without pretending to be a rendered fabrication model.
- Build logical grid boundaries at outer extents plus stored H/V offsets. For planar panels, map them
  directly to the panel plane. For curved/projectable panels, sample each boundary and intersect
  rays along the frame normal with the actual Brep/preview mesh.
- Project the resulting 3D grid polylines and cell centroids into the same axonometric canvas.
- Make cell regions hit-testable. Clicking a visible cell selects it, highlights its boundary, and
  focuses an editor containing the current cladding code and full Rhino key.
- Support Tab/Shift+Tab and arrow navigation in deterministic bottom-to-top/left-to-right order.
- Bound preview cost through mesh and sampling budgets. Large render meshes are reduced or recreated
  from controlled meshing parameters before leaving the Rhino UI thread.
- Show a small orientation marker and a `Planar`/`Curved` badge. Do not display perspective size
  distortion.
- The same renderer must support an offscreen PNG/bitmap export so the workbook tab can embed the
  exact preview shown in Rhino.

### 6. Curvature-Sensitive Canonical Signature And Type Code

- Canonical signature schema version 1 includes, in a fixed culture-invariant order:
  - schema version
  - panel geometry class (`P` or `C`)
  - physical width and height converted to millimeters
  - ordered H and V offsets converted to millimeters
  - row/column counts
  - normalized cladding values ordered by column left-to-right and row bottom-to-top
  - for curved panels, a fixed-size depth sample grid relative to the stable reference plane
- Quantize lengths/depths using a documented physical tolerance derived from Rhino model tolerance
  with a small lower bound. The same physical panel modeled in different Rhino units should produce
  the same signature; insignificant floating-point or mesh noise should not create new types.
- Normalize cladding codes by trimming outer whitespace and applying one documented case policy.
  The first version should use uppercase invariant normalization while preserving the displayed
  canonical value written to Rhino/workbook.
- Hash the canonical signature with SHA-256. Store the complete versioned signature or full digest in
  the proposed signature key and in the workbook's hidden index.
- Produce a short visible code with a shape/grid prefix and digest prefix, for example
  `WT01-C-2X4-8F2C1A9B`. Keep it below Excel's 31-character worksheet limit.
- The leading system token (`WT01` in the example) is configuration, not hardcoded business logic.
  The editor derives it from an approved document setting, layer/system attribute, or explicit
  first-time setting.
- Before accepting a short code, compare the full signature in the workbook index. If a short-code
  collision exists, extend the digest portion deterministically.
- Identical full signatures reuse the existing code/tab. Editing an existing panel into a different
  signature creates or reuses the new type and leaves the prior type catalog entry intact for other
  panels.
- Curved and planar panels with otherwise identical dimensions, offsets, and cladding must receive
  different signatures. Materially different curved depth profiles must also receive different
  signatures.

### 7. Rhino Attribute Commit And Undo

- Save builds a proposed attribute copy and validates the full operation before changing the live
  object.
- Write every canonical cladding value, the deterministic type code, and the full signature in one
  Rhino Undo record named `Assign Panel Cladding Type`.
- Resolve the object again by id immediately before commit. Reject deleted, replaced, wrong-document,
  or changed-geometry targets instead of writing against a stale editor snapshot.
- Use `RhinoDoc.Objects.ModifyAttributes(...)` or the repository's equivalent live adapter and redraw
  the document after success.
- No-op saves do not create a meaningful Undo entry or duplicate workbook sheet.
- The editor refreshes from committed live attributes after success.

### 8. Per-Document Workbook Configuration

- Store the designated absolute `.xlsx` path in document user text using the proposed key
  `MCP_Rhino.CladdingWorkbookPath`.
- The first editor launch without a configured path requires the user to choose an existing `.xlsx`
  file or explicitly create a new one. Do not invent a Desktop or project folder location.
- Changing the path is a deliberate UI action and updates the document setting in Rhino Undo.
- Restrict the first version to `.xlsx`. Reject relative paths, `.xls`, `.xlsm`, directories, and a
  path equal to the active `.3dm` file.
- Show path existence, writability, and current lock status in the editor before Save.

### 9. Existing Workbook Upsert Contract

- Add a dedicated `IPanelCladdingWorkbookRepository`; do not overload the takeoff export interface or
  change `XlsxSpreadsheetWorkbookWriter` semantics.
- During Execute, validate a pinned `DocumentFormat.OpenXml` dependency in Rhino's default and
  isolated load contexts. If it cannot preserve the plugin's packaging/load contract, implement the
  same repository behind a bounded BCL/Open Packaging Convention adapter and record the deviation in
  EXET.
- If the workbook does not exist and the user explicitly selected `Create`, create a valid workbook
  with a hidden index and the first type sheet.
- If it exists, preserve unrelated worksheets, workbook relationships, styles, defined names, and
  other supported package parts. Do not regenerate the whole workbook through the current simple
  takeoff writer.
- Maintain a hidden worksheet named `_CLADDING_INDEX` with at least type code, full signature/digest,
  geometry class, grid size, physical dimensions, normalized offsets, visible sheet name, schema
  version, and created/updated timestamps.
- Each visible type worksheet contains:
  - type code and geometry class
  - physical dimensions and ordered H/V offsets
  - a top-to-bottom/left-to-right proportional cell matrix with row/column labels
  - one cladding value per cell
  - the embedded orthographic axonometric preview image
  - a bounded metadata section explaining the Rhino key orientation
- Scale Excel row heights and column widths proportionally to physical cell sizes within readable
  minimum/maximum bounds. The sheet remains recognizable as the panel even when actual aspect ratios
  are extreme.
- Sanitize all user-entered text against spreadsheet formula injection. Treat leading `=`, `+`, `-`,
  and `@` as text and preserve the visible cladding code.
- Existing identical signatures reuse their tabs. A missing indexed tab is repaired only after the
  user sees a warning; an existing same-name tab with a different signature is never overwritten.

### 10. Coordinated Rhino And Workbook Safety

- Preflight the workbook path, directory, format, structure, writability, lock state, type-code
  availability, and preview image before starting a Rhino mutation.
- Read the source workbook and produce a complete updated temporary workbook in the same directory.
  Open and validate that temporary package before touching Rhino.
- Keep the original object attributes in memory, begin one Undo record, and apply the proposed Rhino
  attributes only after the workbook temporary file is ready.
- Atomically replace/create the final workbook from the validated temporary file. Use a recoverable
  same-directory backup when replacing an existing workbook.
- If final workbook replacement fails, restore the original Rhino attributes before ending the Undo
  scope and report failure. Do not report success for a partial operation.
- If the workbook update succeeds and the user later undoes the Rhino assignment, the type tab may
  remain as an unused catalog definition; this is acceptable and should be documented because Rhino
  Undo cannot atomically reverse an external file later.
- If Excel or another process has the workbook locked, fail before Rhino mutation with a concise
  `close workbook and retry` message. Do not write a second conflicting filename silently.
- Clean abandoned temporary files created by the current operation. Never delete an unknown user
  file or unrelated backup.

### 11. Grasshopper Curved-Panel Compatibility

- Preserve the shared script inputs `brep`, `lines`, `toggle`, the zero-output contract, reference-id
  resolution, one Undo record, idempotence, and current attribute names.
- Retain planar behavior as a regression baseline.
- Replace the blanket `all vertices must lie on one plane` rejection with a projectability test that
  uses the same stable local reference frame as the editor.
- For supported curved panels, project panel extents and guide samples orthogonally into the local
  frame. H/V values remain orthogonal distances from point 0, exactly matching the original user
  definition.
- Do not interpret curved-surface UV distances or arc lengths as H/V.
- Accept only guide/panel relationships whose orthographic projection passes through the panel domain
  without ambiguous folds/overlaps. Unsupported curved panels must raise a clear Grasshopper runtime
  error and modify no objects.
- Share or mirror test vectors for frame orientation, point 0, H/V order, and projectability between
  the Python script and C# editor so their key interpretation cannot drift.
- Curved support requires at least one user-provided representative curved panel when available, plus
  synthetic cylindrical/projectable and folded/unsupported fixtures in tests.

### 12. Dependency And Assembly-Load Boundaries

- Keep RhinoCommon, Rhino.UI, Eto, and Eto.Wpf resolved from Rhino's default load context, consistent
  with `PluginLoadContext`.
- Do not pass Eto controls, RhinoCommon geometry, workbook package types, or service interfaces across
  the isolated MCP host boundary.
- Keep the local command/controller in the default plugin context and keep Router/MCP hosts operating
  exactly as before.
- Include every required private workbook assembly in Release packaging and owned-hash validation.
- Verify that loading the UI command does not force ModelContextProtocol or conflicting
  `System.Text.Json` assemblies into Rhino's default context.

## Involved Files

Expected production additions or modifications during Execute include:

- `src/MCP_Rhino.Server/Domain/Models/PanelCladding/PanelCladdingLayout.cs`
- `src/MCP_Rhino.Server/Domain/Models/PanelCladding/PanelCladdingTypeIdentity.cs`
- `src/MCP_Rhino.Server/Domain/Models/PanelCladding/PanelPreviewDescriptor.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILivePanelCladdingRepository.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IPanelCladdingWorkbookRepository.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IPanelPreviewRenderer.cs`
- `src/MCP_Rhino.Server/Application/Services/PanelCladding/PanelCladdingKeyService.cs`
- `src/MCP_Rhino.Server/Application/Services/PanelCladding/PanelCladdingTypeSignatureService.cs`
- `src/MCP_Rhino.Server/Application/Services/PanelCladding/PanelCladdingSaveService.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingRepository.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/PanelCladding/PanelAxonometricProjectionBuilder.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/PanelCladding/McpPanelCladdingEditorCommand.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/PanelCladding/PanelCladdingEditorWindow.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/PanelCladding/PanelCladdingPreviewCanvas.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/PanelCladding/PanelCladdingEditorController.cs`
- `src/MCP_Rhino.Server/Infrastructure/File/PanelCladding/OpenXmlPanelCladdingWorkbookRepository.cs`
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/PluginLoadContext.cs` only if dependency validation
  proves an explicit load rule is required
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`
- `Packaging/MCP_Rhino/Build-McpRhinoPackage.ps1` and package manifest inputs when a new private
  dependency must be staged
- `V:\01 Project Folders\P00020 BayHealth Kent Tower BKT\03-Design-Eng\03-BIM\05-Wireframe\02_grasshopper\WF setup\WT01_panel_mullion_attributes.py`
- `Project_Test/260804_TEST_panel-cladding-type-editor/`
- `Project_Exet/260804_EXET_panel-cladding-type-editor.md`

These are expected ownership locations, not a requirement to create empty files. Execute may combine
closely related small types when doing so preserves the architecture boundaries and will record any
material deviation in EXET.

## Usage

1. In Grasshopper, reference the Rhino panel Breps and mullion guide curves through native Brep and
   Curve parameters.
2. Run the shared no-output offset script by changing its toggle from false to true. The script
   commits H/V offsets and blank cladding keys to the referenced Rhino objects.
3. In Rhino, preselect one panel.
4. Run `McpPanelCladdingEditor` from a toolbar button, alias, or command line.
5. If the document has no configured workbook, choose or explicitly create the project's `.xlsx`
   cladding type catalog.
6. Inspect the orthographic axonometric preview and its `Planar`/`Curved` classification.
7. Click each projected cell and enter its cladding code. Use keyboard navigation to move through the
   logical cells.
8. Press `Save Type`.
9. The editor validates the panel and workbook, computes/reuses the deterministic type, commits the
   Rhino attributes, and creates/reuses the visible workbook tab.
10. Other users can open the workbook and read the proportional matrix, metadata, and embedded
    axonometric preview without Rhino.

## Test Strategy

### Domain And Application Tests

- Parse unsorted H/V keys and produce stable numeric ordering.
- Reject duplicate/gapped indices, outside extents, non-monotonic offsets, malformed keys, and
  missing required cells.
- Verify `A..Z, AA..` row labels and multi-digit columns.
- Verify exact cell-to-user-text mapping in both directions.
- Verify cladding normalization, whitespace handling, uppercase policy, and formula-like text safety.
- Verify signature stability across equivalent model units and insignificant numeric noise.
- Verify a meaningful offset, dimension, cladding, planar/curved class, or curved depth-profile change
  changes the signature.
- Verify identical signatures reuse a type code and a short-code collision extends deterministically.
- Verify no-op save behavior and stale-object/change detection.

### Projection And Render Tests

- Use deterministic synthetic fixtures for:
  - planar rectangular Brep
  - planar non-square Brep
  - mildly cylindrical/projectable Brep
  - doubly curved but single-valued Brep when reliable
  - folded/overhanging or multi-hit unsupported Brep
- Assert stable local orientation, point 0, extents, planarity classification, depth samples, grid
  boundary order, projected cell centroids, and bounded preview dimensions.
- Assert planar and curved fixtures produce visibly/deterministically different depth diagnostics and
  preview output.
- Produce bounded PNG artifacts for manual visual review. Golden-image comparison may use tolerant
  perceptual or geometry assertions rather than a brittle exact pixel hash.

### Workbook Tests

- Create a new `.xlsx` with index, one visible type tab, proportional cell matrix, and embedded image.
- Upsert a second unique type without changing the first or unrelated fixture worksheets.
- Reuse an identical signature without adding a duplicate worksheet.
- Preserve unrelated workbook parts supported by the chosen Open XML path.
- Detect same-name/different-signature conflicts and extend the type code safely.
- Reject malformed, read-only, locked, `.xls`, and `.xlsm` targets.
- Verify formula-like codes are stored as text.
- Verify same-directory temp replacement, backup recovery, and cleanup behavior.
- Reopen every generated workbook through an independent Open XML reader and assert workbook/package
  validity, sheet visibility, cell values, relationships, and embedded image parts.

### Grasshopper Script Tests

- Compile the shared Python source without emitting `__pycache__` beside the Rhino document.
- Preserve the planar WT-01 expected H/V and cell-key results.
- Verify referenced Breps commit to `RhinoObject.Attributes` and generated/unbaked Breps fail clearly.
- Verify one supported curved/projectable fixture computes orthogonal projected offsets.
- Verify an unsupported folded/multi-hit fixture fails before any Rhino object changes.
- Verify toggle false is a strict no-op and repeated toggle true is idempotent.

### Live Rhino Smoke

- Add the capability-owned CLI slug `panel-cladding-type-editor-smoke-test` through the existing
  partial handler registration pattern.
- Add the capability-owned Rhino smoke command `McpPanelCladdingTypeEditorSmoke`.
- In a saved live test document, create or use planar and curved projectable panels with seeded H/V
  attributes, build descriptors, render previews, calculate signatures, and commit one type through
  the non-UI save service.
- Verify object user text, Undo behavior, workbook output, type reuse, and locked-workbook no-mutation
  behavior.
- The smoke command must not open a chat/Companion panel, start Router, or mutate another open
  document.

### UI Manual QA

- Capture the planar and curved editor windows at a consistent size.
- Verify the curved silhouette and shading are distinguishable from the planar case.
- Verify clicking projected cells selects the correct key and keyboard navigation is predictable.
- Verify unsaved edits are protected when selection/document state changes.
- Verify workbook path selection, lock messages, success feedback, and error details are concise.
- Verify the window scales at normal and high Windows DPI.

### Build, Packaging, And Router Regression

- Run `dotnet build .\MCP_Rhino.sln -c Debug`.
- Run `dotnet build .\MCP_Rhino.sln -c Release`.
- Run the MCP tool inventory/safety smoke in both configurations if any tool surface changes
  unexpectedly; the expected outcome is no new MCP tool.
- Build the Release package and verify all new private dependencies are staged and hash-owned.
- Install/repair the package in a controlled current-user test, restart Rhino 8, and confirm the
  plugin still auto-loads and the new command is available.
- Verify Router document discovery and one read-only routed call after the UI feature is installed.
- Verify no removed Bridge/Companion/chat/panel command or fallback transport is registered or
  packaged.

## Acceptance Criteria

- A user can preselect one panel and open the editor through one focused Rhino command.
- The editor reads only the selected live document/object and never silently switches documents or
  panels.
- A tested planar panel displays a planar orthographic axonometric grid with correct cell labels.
- A tested curved/projectable panel displays actual visible bend/depth and deformed grid boundaries,
  is labeled `Curved`, and remains editable by logical cell.
- Folded/non-projectable geometry is identified explicitly and cannot be saved with a fabricated
  successful mapping.
- Every editable cell maps to the expected existing cladding user-text key.
- Saving a complete layout writes cladding values, type code, and full signature in one Rhino Undo
  record.
- Identical physical/curvature/offset/cladding signatures reuse the same type code and worksheet.
- A significant geometry class/profile, dimension, offset, grid, or cladding change produces a
  different type signature.
- The workbook path is explicitly chosen and remembered per Rhino document.
- A new unique type creates one visible worksheet shaped proportionally like the panel and containing
  the same axonometric preview shown in Rhino.
- Existing unrelated worksheets survive the update unchanged to the extent promised by the selected
  Open XML adapter.
- A locked or invalid workbook produces no lasting Rhino attribute change.
- The planar behavior and no-output contract of the existing Grasshopper script remain intact.
- Supported curved panels can receive orthogonal H/V keys without redefining those distances as arc
  length or UV distance.
- Debug and Release solution builds, capability tests, Release package validation, and live smoke all
  pass with recorded evidence.
- The installed plugin remains startup-loaded and Router-only; no retired access path or companion
  command returns.

## Risks And Rollback

### Risks

- Orthographic mapping is inherently ambiguous for folded, undercut, or self-overlapping panels.
  Mitigation: classify projectability, bound ray sampling, warn clearly, and disable save rather than
  display a misleading grid.
- Type stability can be damaged by floating-point, meshing, or model-unit noise. Mitigation: sample
  from stable geometry/frame rules, convert to physical units, quantize by documented tolerance, and
  store the full versioned signature.
- Curved preview performance can degrade with dense Breps. Mitigation: clone only bounded data,
  control meshing/sampling budgets, cache one selected descriptor, and keep heavy redraw work off the
  Rhino document-access callback.
- Existing workbooks may contain advanced Excel features that a simplistic writer would damage.
  Mitigation: use a dedicated read/upsert repository, preserve package parts, validate against fixture
  workbooks, and never route this through the current create-only takeoff writer.
- Cross-system Rhino/file updates cannot participate in one native transaction. Mitigation: fully
  prepare/validate the workbook temp file before Rhino mutation and restore original attributes if
  final replacement fails.
- Excel may lock the workbook. Mitigation: explicit preflight and no silent alternate filename.
- New UI/workbook dependencies can conflict with Rhino's assembly load contexts. Mitigation: keep UI
  in the default context, share Rhino/Eto assemblies, pin private dependencies, and validate Debug,
  Release, packaging, startup load, and Router regression.
- The current shared Grasshopper script is planar-only. Mitigation: treat curved/projectable support
  as an explicit compatibility deliverable, retain planar regression fixtures, and reject unsupported
  geometry rather than guessing.

### Rollback

- Remove the new command/UI, panel-cladding Application/Domain services, live adapters, workbook
  repository, dependency registrations, and private package dependency.
- Remove only the capability-owned smoke handler/command and test folder if the feature is formally
  abandoned; do not alter unrelated historical smoke registrations.
- Restore the shared Grasshopper script from its pre-Execute copy if curved compatibility causes a
  planar regression.
- Rhino attribute assignments remain recoverable through the single Undo record during use.
- Workbook replacement retains a recoverable backup during the operation; rollback never deletes
  unrelated user workbooks or worksheets.
- Rebuild/reinstall the prior Router-only package and verify the plugin startup route endpoints before
  declaring rollback complete.

## Future Extensions

- A dockable panel variant that follows selection only after an explicit opt-in.
- Multi-panel batch assignment of an already-defined type.
- Search/autocomplete and controlled dictionaries for approved cladding codes.
- Rectangular clipboard paste, fill-row/fill-column, and recent-value shortcuts.
- Type catalog browsing and selecting an existing type directly from Rhino.
- Intentional void-cell semantics and non-rectangular logical grids.
- Exact surface-development/fabrication output as a separate capability with different geometry
  guarantees.
- Bidirectional workbook import only after conflict, locking, authorship, and approval semantics are
  planned separately.
- Optional MCP atomic tools for type-catalog inspection or assignment only if a later runtime use
  case requires them; no MCP surface is justified by the first local-editor workflow.
