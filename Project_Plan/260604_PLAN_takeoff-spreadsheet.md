# Takeoff Spreadsheet Plan

## Background

The desired capability is not a fixed snapshot of the current Rhino document into a spreadsheet. The target workflow is closer to quantity take-off / schedule generation:

- the user assigns meaningful attributes to objects, for example panel type, system, finish, level, phase, mark, width, height, or cost code
- the user asks for a custom take-off, such as "give me a panel schedule grouped by panel type and finish, total the area, count the panels, and sort by level"
- the MCP server resolves the relevant live Rhino objects, reads attributes and geometry, calculates derived values, groups and sorts rows, then writes the spreadsheet shape requested by the user

That means the core capability must be a flexible take-off agent with spreadsheet output, not a fixed `DocumentSummary/Layers/Objects` export. Spreadsheet writing remains external filesystem output and belongs under `Tools/File/Export`, but the judgment over the user's request belongs in `Agents/`: the agent decides what needs discovery, what is ambiguous, what can be previewed, and whether export is allowed.

The server should still stay deterministic and safe. It should not execute arbitrary formulas or user code. The agent can interpret a user's take-off intent into a structured take-off spec, but uncertain mappings must become clarification questions rather than guesses. The validated spec is then executed against the live Rhino document by deterministic services.

Because each Rhino file may use different user text keys and naming conventions, the capability must discover available attributes in the live document and return structured clarification questions when a requested take-off is underspecified. The MCP tool itself should not silently choose an output folder, output file name, attribute mapping, grouping key, or quantity source.

## Goals

- Add a flexible live Rhino take-off capability for user-defined schedules and spreadsheets.
- Make `TakeoffSpreadsheetAgent` the primary capability for interpreting the user's schedule/take-off request.
- Support object selection by explicit ids, current selection, confirmed layer paths, object types, and user attribute filters.
- Read object user text, layer data, object names/types, material names, and supported geometry metrics.
- Calculate derived take-off values such as count, length, area, volume, dimensions, attribute-derived numbers, and safe expression-based columns.
- Support user-defined columns, group-by keys, aggregate columns, sort rules, and multiple sheets.
- Discover available user text keys, sample values, object types, layers, and metric-capable geometry for a candidate scope.
- Return structured clarification questions when output destination, attribute mapping, quantity source, grouping, sorting, or units are ambiguous.
- Provide a preview path that returns the resolved schedule schema, row samples, warnings, and aggregate totals before writing files.
- Provide an export path that writes the previewed take-off to `.csv` or `.xlsx` only after the user has supplied an explicit output location and output file name.
- Keep live Rhino access live-only through `ILiveRhinoDocumentAccessor`; do not read disk `.3dm` files as fallback truth.
- Keep spreadsheet writing outside the Rhino UI-thread callback wherever possible.

## Non-Goals

- Do not build a generic fixed document snapshot exporter as the primary capability.
- Do not add spreadsheet import or spreadsheet-driven Rhino mutation in this plan.
- Do not execute arbitrary user code, arbitrary C# expressions, Rhino scripts, Python, Excel formulas, or SQL.
- Do not force the user to supply a fully fixed input/output schema before the agent can help. The agent may propose a spec from the user request and discovered file data.
- Do not let the agent silently resolve ambiguous attribute mappings, quantity sources, grouping, sorting, units, output folder, or output file name.
- Do not hardcode fixed attribute meanings such as assuming `PanelType`, `Finish`, or `Level` exist in every file.
- Do not invent default output locations or file names.
- Do not require Excel automation or installed Microsoft Excel.
- Do not silently invent missing attributes or units. Missing data must be represented as warnings, blank cells, or explicit failure depending on column settings.

## Architecture Ownership

- `Agents/Takeoff`: add the primary `TakeoffSpreadsheetAgent` for goal-level judgment over user requests, discovery needs, clarification questions, preview planning, and export readiness.
- `Tools/File/Export`: add a thin MCP wrapper for the agent, expected method name `RunTakeoffSpreadsheetAgent`, because the agent may write a caller-requested spreadsheet file.
- `Tools/Analysis`: optionally expose supporting read-only atomic tools, expected method names `InspectTakeoffSources` and `PreviewTakeoffSchedule`, when direct deterministic calls are useful outside the agent.
- `Tools/File/Export`: optionally expose the deterministic file-writing support tool, expected method name `ExportTakeoffSchedule`, if direct spec-driven export is useful outside the agent.
- `Skills/Inspection` or `Skills/Takeoff`: add `TakeoffScheduleSkill` only if there is a reusable deterministic workflow for executing a validated take-off spec.
- `Application/Services`: add services for spec validation, object resolution, metric collection orchestration, take-off calculation, grouping, sorting, preview shaping, and export orchestration.
- `Application/Interfaces`: add abstractions such as `ILiveTakeoffMetricReader` and `ISpreadsheetWorkbookWriter`.
- `Domain/Models`: add the take-off spec model, calculation model, schedule rows, aggregate definitions, warnings, and workbook model.
- `Contracts/Requests`: add request DTOs for preview/export and the structured take-off spec.
- `Contracts/Responses`: add response DTOs for preview/export, row samples, totals, warnings, and written file metadata.
- `Infrastructure/Rhino/Live`: add RhinoCommon-backed metric extraction for supported geometry quantities.
- `Infrastructure/File` or an equivalent writer folder: add CSV and XLSX writer implementations.
- `Server/DependencyInjection.cs`: register new services, agent/skill, metric reader, and writer abstractions.
- `Project_Test/260604_TEST_takeoff-spreadsheet/`: add capability smoke artifacts during Execute.
- `Project_Exet/260604_EXET_takeoff-spreadsheet.md`: record implementation and validation after Execute.

## Key Design

### 1. Primary Agent Surface

The primary runtime entry point should be `RunTakeoffSpreadsheetAgent`.

Inputs:

- `filePath`
- `userRequest`: the user's natural-language take-off request
- optional `scopeHints`: selected object ids, confirmed layer paths, object types, or user attribute filters
- optional `knownMappings`: user-confirmed mappings from schedule concepts to user text keys or metric sources
- optional `outputDirectory`
- optional `outputFileName`
- optional `overwriteExisting`
- optional `mode`: `Clarify`, `Preview`, or `Export`

Agent responsibilities:

- inspect the live document when the request cannot be answered from supplied mappings
- infer a proposed take-off spec from the user's request and discovered source data
- decide whether the request is clear enough to preview or export
- ask concise structured clarification questions when it is not clear enough
- build and run preview through deterministic Application services
- export only when output folder, output file name, mappings, quantity source, grouping/sorting, and units are sufficiently resolved
- return a response status such as `NeedsClarification`, `PreviewReady`, `Exported`, `CapabilityGap`, or `Failed`

Safety:

- `RunTakeoffSpreadsheetAgent` must be annotated `ReadOnly = false, Destructive = true, OpenWorld = true` because it can write or overwrite external spreadsheet files when called in export mode.
- When the agent only discovers or previews, it must not write files, mutate Rhino document state, or open an undo record despite the conservative MCP safety annotation.

### 2. Declarative Take-Off Spec

The capability should expose a structured spec rather than a fixed report shape. The spec defines what the user wants calculated and how the result should be laid out.

Core concepts:

- `scope`: which objects participate
- `source columns`: values read directly from object metadata, user text, layer, material, or geometry metrics
- `computed columns`: values derived from safe supported expressions
- `groupBy`: keys such as `PanelType`, `Finish`, `Level`, `LayerFullPath`, or material
- `aggregates`: count, sum, min, max, average, first, or joined distinct values
- `sortBy`: stable sort keys and directions
- `sheets`: one or more named schedules, each with its own scope, columns, grouping, and sorting
- `units`: requested display units and conversion behavior

The spec is flexible in shape, but not arbitrary code. Supported expression syntax should be small and explicit, for example references to named columns plus arithmetic operators and safe functions such as `round`, `coalesce`, `min`, and `max`. Unsupported expressions fail validation.

### 3. Source Discovery And Clarification

Add a read-only discovery tool:

- `InspectTakeoffSources`
  - resolves a candidate live object scope
  - returns available user text keys, sample values, layer paths, object type counts, material names, selected-object counts, and metric availability by object type
  - returns possible quantity-source candidates such as geometry area, bounding-box dimensions, or numeric user text keys
  - returns bounded samples only, with counts and truncation warnings
  - safety: `ReadOnly = true, Destructive = false, OpenWorld = false`

This discovery path gives the agent enough evidence to ask the user targeted questions before finalizing the take-off spec. Example questions:

- Which output folder should receive the spreadsheet?
- What should the output file be named, including `.csv` or `.xlsx`?
- Which discovered user text key represents panel type?
- Which discovered user text key represents finish, level, phase, or mark?
- Should panel area come from Rhino geometry area, projected/bounding-box area, or width x height attributes?
- Should rows be detailed per object or grouped by selected attributes?
- Which columns should be sorted, and in what order?

The agent response should carry these as structured `ClarificationQuestion` entries. The MCP client/model presents those questions to the user; the server does not block waiting for interactive input inside a tool call.

### 4. Preview Then Export

Add deterministic schedule execution paths for the agent and optional direct tools:

- `PreviewTakeoffSchedule`
  - validates the spec
  - resolves live objects
  - computes schedule rows and aggregate totals
  - returns schema, warnings, row counts, aggregate values, and bounded row samples
  - returns `NeedsClarification` with specific questions if required mappings or quantity rules are ambiguous
  - safety: `ReadOnly = true, Destructive = false, OpenWorld = false`

- `ExportTakeoffSchedule`
  - runs the same validation/calculation path
  - requires explicit `outputDirectory` and `outputFileName`, or an implementation-documented full-path alternative that is split and validated into those two concepts
  - writes the generated schedule to `.csv` or `.xlsx`
  - returns output path, format, sheet counts, row counts, byte count, duration, and warnings
  - returns `TAKEOFF_OUTPUT_REQUIRED` if the output location or file name is missing
  - returns `TAKEOFF_CLARIFICATION_REQUIRED` if attribute mapping, quantity source, grouping, or units are unresolved
  - safety: `ReadOnly = false, Destructive = true, OpenWorld = true`

The export tool should not write a file if calculation validation fails. For broad or ambiguous take-offs, runtime policy should prefer preview first, then export after the user accepts the schedule shape.

### 5. Output Destination Contract

Export must ask for or receive both:

- output location: an absolute existing folder path
- output file name: a filename with `.csv` or `.xlsx`

The server must not invent either value. Validation rules:

- reject missing output folder or missing output file name with `TAKEOFF_OUTPUT_REQUIRED`
- require absolute output folder path
- require existing output folder
- reject path traversal in `outputFileName`
- allow only `.csv` and `.xlsx`
- compose and normalize final `outputPath`
- block final output path equal to the active `.3dm` path
- if output exists and `overwriteExisting` is false, return `TAKEOFF_OUTPUT_OVERWRITE_BLOCKED`
- if overwriting is allowed, write via a temporary file in the target directory, then replace the final file
- validate final output exists and has non-zero bytes

### 6. Object Resolution And Attributes

Supported first-version scope options:

- explicit object ids
- current Rhino selection
- confirmed layer full paths
- object types
- user attribute conditions using existing filter conventions
- match mode: all or any

Supported attribute sources:

- object id
- object name
- normalized object type
- geometry type
- layer name and full path
- material name when available
- object user text by key
- document/user text only if explicitly requested and safe

Attribute values should preserve strings by default. Numeric columns derived from attributes must parse with invariant culture and report non-numeric rows with warnings or blanks depending on the column's null policy.

Attribute ambiguity policy:

- never assume a specific key name exists across files
- when the spec references a missing key, return a clear missing-key warning or failure according to null policy
- when multiple discovered keys plausibly match a requested concept, return `NeedsClarification`
- include sample values in discovery responses so the user can choose the intended key
- preserve the user-chosen key names exactly in the finalized spec

### 7. Geometry And Quantity Metrics

Supported first-version metrics should reuse existing metric services where possible:

- count
- curve length
- surface/brep/mesh area
- volume where the geometry supports volume computation
- bounding-box dimensions
- center point

Panel-specific schedules should be able to combine attributes and geometry, for example:

- `PanelType` from user text
- `Finish` from user text
- `Level` from layer or user text
- `PanelArea` from brep/mesh/surface area, or from user text width x height when geometry is not reliable
- `PanelCount` as count
- `TotalArea` as sum of `PanelArea`

If a metric is unsupported for an object type, the row should receive a warning and the configured null policy decides whether the schedule fails, blanks the value, or skips the row.

Panel quantity source policy:

- require the spec to state whether panel area comes from geometry area, projected/bounding-box dimensions, or numeric width/height attributes
- return `TAKEOFF_QUANTITY_SOURCE_REQUIRED` if the requested quantity cannot be determined safely
- keep geometry-derived and attribute-derived quantities visibly labeled in preview/export metadata

### 8. Grouping, Aggregation, And Sorting

The take-off engine should produce two useful row modes:

- detail rows: one row per object
- aggregate rows: one row per group

Aggregations:

- `count`
- `sum`
- `min`
- `max`
- `average`
- `first`
- `distinctJoin`

Sorting:

- allow sort by source, computed, or aggregate column
- support ascending/descending
- keep stable ordering for deterministic output

### 9. Spreadsheet Writer

CSV:

- BCL-only UTF-8 writer
- one schedule sheet per file
- fail with `TAKEOFF_CSV_REQUIRES_SINGLE_SHEET` if multiple sheets are requested
- quote fields containing comma, quote, CR, or LF

XLSX:

- supports multiple sheets
- use a no-Excel package writer after dependency validation in Debug and Release plugin builds
- keep writer behind `ISpreadsheetWorkbookWriter`
- if package validation fails, ship CSV first and record the XLSX blocker in EXET

Spreadsheet formula injection:

- escape text values beginning with `=`, `+`, `-`, or `@`
- emit `TAKEOFF_FORMULA_TEXT_ESCAPED` warnings

### 10. UI Thread Boundary

Preferred execution flow:

1. Validate the take-off spec outside the live document accessor.
2. Use `ILiveRhinoDocumentAccessor.Execute(...)` only to resolve objects and collect primitive snapshot values/metrics.
3. Perform grouping, aggregation, sorting, and workbook construction outside the Rhino UI-thread callback when it does not require RhinoCommon objects.
4. Write the spreadsheet outside the Rhino UI-thread callback.

This keeps Rhino responsive and avoids doing file IO while holding the live document execution boundary.

## Involved Files

Likely production files:

- `src/MCP_Rhino.Server/Tools/Analysis/PreviewTakeoffScheduleTool.cs`
- `src/MCP_Rhino.Server/Tools/Analysis/InspectTakeoffSourcesTool.cs`
- `src/MCP_Rhino.Server/Tools/File/Export/RunTakeoffSpreadsheetAgentTool.cs`
- `src/MCP_Rhino.Server/Tools/File/Export/ExportTakeoffScheduleTool.cs`
- `src/MCP_Rhino.Server/Agents/Takeoff/TakeoffSpreadsheetAgent.cs`
- `src/MCP_Rhino.Server/Skills/Inspection/TakeoffScheduleSkill.cs` if the execution flow stabilizes as a reusable skill
- `src/MCP_Rhino.Server/Contracts/Requests/TakeoffScheduleRequests.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/TakeoffSpreadsheetAgentRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/TakeoffScheduleResponses.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/TakeoffSpreadsheetAgentResponse.cs`
- `src/MCP_Rhino.Server/Domain/Enums/TakeoffScheduleEnums.cs`
- `src/MCP_Rhino.Server/Domain/Models/TakeoffScheduleSpec.cs`
- `src/MCP_Rhino.Server/Domain/Models/TakeoffScheduleWorkbook.cs`
- `src/MCP_Rhino.Server/Application/Services/TakeoffScheduleValidationService.cs`
- `src/MCP_Rhino.Server/Application/Services/TakeoffScheduleCalculationService.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoTakeoffScheduleService.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveTakeoffMetricReader.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ISpreadsheetWorkbookWriter.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveTakeoffMetricReader.cs`
- `src/MCP_Rhino.Server/Infrastructure/File/CsvSpreadsheetWorkbookWriter.cs`
- `src/MCP_Rhino.Server/Infrastructure/File/XlsxSpreadsheetWorkbookWriter.cs` if XLSX is validated
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`
- `src/MCP_Rhino.Server/Server/AgentRegistration.cs` if an agent/skill is added
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj` if an XLSX package is added
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpTakeoffSpreadsheetSmokeCommand.cs`
- `Project_Archive/Project_Test/260506_TEST_mcp-tool-safety-annotations/DeveloperCommandHandler.McpToolSafetyAnnotationsSmokeTest.cs`
- `Project_Test/260508_TEST_mcp-tool-overlap-cleanup/DeveloperCommandHandler.McpToolOverlapCleanupSmokeTest.cs`
- `Project_Test/260507_TEST_mcp-surface-structure-governance/DeveloperCommandHandler.McpSurfaceStructureGovernanceSmokeTest.cs`

Required construction artifacts during Execute:

- `Project_Test/260604_TEST_takeoff-spreadsheet/`
- `Project_Exet/260604_EXET_takeoff-spreadsheet.md`

## Usage

Primary agent request:

```json
{
  "filePath": "C:\\Models\\facade.3dm",
  "userRequest": "Create a panel take-off for the facade panels. Group by panel type and finish, count panels, total panel area, sort by level, and export it as an Excel file.",
  "scopeHints": {
    "confirmedLayerFullPaths": ["Facade::Panels"],
    "objectTypes": ["Brep", "Mesh"]
  },
  "outputDirectory": "C:\\Exports",
  "outputFileName": "facade-panel-takeoff.xlsx",
  "overwriteExisting": true,
  "mode": "Export"
}
```

If the agent cannot determine the correct attribute keys or quantity basis, it should return `NeedsClarification` instead of exporting. For example, it may ask which discovered key represents panel type, which key represents finish, and whether area should come from geometry area or width/height attributes.

Deterministic preview spec example:

```json
{
  "filePath": "C:\\Models\\facade.3dm",
  "spec": {
    "sheets": [
      {
        "name": "Panel take-off",
        "scope": {
          "confirmedLayerFullPaths": ["Facade::Panels"],
          "objectTypes": ["Brep", "Mesh"],
          "userAttributeConditions": [
            { "key": "PanelSystem", "comparisonMode": "Exists" }
          ],
          "matchMode": "All"
        },
        "columns": [
          { "name": "Level", "source": { "kind": "UserText", "key": "Level" } },
          { "name": "PanelType", "source": { "kind": "UserText", "key": "PanelType" } },
          { "name": "Finish", "source": { "kind": "UserText", "key": "Finish" } },
          { "name": "PanelArea", "source": { "kind": "GeometryMetric", "metric": "Area" }, "unit": "m2" }
        ],
        "groupBy": ["Level", "PanelType", "Finish"],
        "aggregates": [
          { "name": "PanelCount", "function": "Count" },
          { "name": "TotalArea", "function": "Sum", "column": "PanelArea" }
        ],
        "sortBy": [
          { "column": "Level", "direction": "Ascending" },
          { "column": "PanelType", "direction": "Ascending" }
        ],
        "rowMode": "Aggregate"
      }
    ]
  }
}
```

Discovery / clarification example:

```json
{
  "filePath": "C:\\Models\\facade.3dm",
  "scope": {
    "confirmedLayerFullPaths": ["Facade::Panels"],
    "objectTypes": ["Brep", "Mesh"]
  },
  "sampleValueCount": 5
}
```

The response should include discovered user text keys and sample values. If the user asks "make me a panel take-off" and the model cannot confidently map the keys, it should ask the user which discovered keys represent panel type, finish, level, and quantity basis before calling preview/export.

Export example:

```json
{
  "filePath": "C:\\Models\\facade.3dm",
  "outputDirectory": "C:\\Exports",
  "outputFileName": "facade-panel-takeoff.xlsx",
  "overwriteExisting": true,
  "spec": {
    "sheets": [
      {
        "name": "Panel take-off",
        "scope": { "confirmedLayerFullPaths": ["Facade::Panels"] },
        "columns": [
          { "name": "PanelType", "source": { "kind": "UserText", "key": "PanelType" } },
          { "name": "Finish", "source": { "kind": "UserText", "key": "Finish" } },
          { "name": "PanelArea", "source": { "kind": "GeometryMetric", "metric": "Area" }, "unit": "m2" }
        ],
        "groupBy": ["PanelType", "Finish"],
        "aggregates": [
          { "name": "Count", "function": "Count" },
          { "name": "TotalArea", "function": "Sum", "column": "PanelArea" }
        ],
        "rowMode": "Aggregate"
      }
    ]
  }
}
```

## Acceptance Criteria

- `RunTakeoffSpreadsheetAgent` is discoverable on the MCP tool surface with a non-empty `[Description]`.
- `InspectTakeoffSources` is discoverable on the MCP tool surface with a non-empty `[Description]`.
- `PreviewTakeoffSchedule` is discoverable on the MCP tool surface with a non-empty `[Description]`.
- `ExportTakeoffSchedule` is discoverable on the MCP tool surface with a non-empty `[Description]`.
- Safety annotations are explicit:
  - `RunTakeoffSpreadsheetAgent`: `ReadOnly = false, Destructive = true, OpenWorld = true`
  - `InspectTakeoffSources`: `ReadOnly = true, Destructive = false, OpenWorld = false`
  - `PreviewTakeoffSchedule`: `ReadOnly = true, Destructive = false, OpenWorld = false`
  - `ExportTakeoffSchedule`: `ReadOnly = false, Destructive = true, OpenWorld = true`
- The agent is registered through `Server/AgentRegistration.cs`, and the MCP wrapper remains a thin adapter with no duplicated orchestration logic.
- All live take-off paths are live-only and return `LIVE_RHINO_REQUIRED` in CLI fallback mode.
- The agent can take a natural-language `userRequest`, inspect scoped file attributes, propose a structured take-off spec, and return `NeedsClarification` when mappings are uncertain.
- The agent does not export when required clarification questions remain unanswered.
- The agent exports only when mode/output/mappings/quantity source are sufficiently resolved.
- Discovery returns available user text keys, sample values, object type counts, layer candidates, and metric availability for a scoped set of panel-like objects.
- Preview returns `NeedsClarification` with structured questions when attribute mapping, quantity source, grouping, units, output destination, or sorting is ambiguous.
- Export requires an explicit output folder and output file name; missing values return `TAKEOFF_OUTPUT_REQUIRED` and no file is written.
- Export rejects invalid file names, non-existing output folders, unsupported extensions, path traversal, active `.3dm` overwrite, and overwrite-blocked cases.
- Preview resolves a panel take-off from object user text and geometry metrics, returns grouped aggregate rows, and does not write files.
- Export writes a non-empty CSV for a single-sheet panel take-off.
- Export writes a non-empty XLSX for a multi-sheet take-off if the XLSX dependency validates; otherwise EXET records the precise blocker and CSV remains passing.
- CSV multi-sheet requests fail with `TAKEOFF_CSV_REQUIRES_SINGLE_SHEET`.
- Attribute-missing, ambiguous-attribute, unsupported-metric, missing-quantity-source, non-numeric attribute, row-cap, formula-text escaping, missing-output, invalid-output-name, overwrite-blocked, and invalid-expression cases are covered by smoke tests.
- The live smoke creates or uses panel-like objects with assigned user text, calculates count and total area by group, and verifies the expected aggregate values.
- Debug and Release builds pass:
  - `dotnet build .\MCP_Rhino.sln -c Debug`
  - `dotnet build .\MCP_Rhino.sln -c Release`
- MCP tool safety smoke passes in Debug and Release:
  - `dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-tool-safety-annotations-smoke-test`
  - `dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-tool-safety-annotations-smoke-test`
- Capability smoke slug is unique: `takeoff-spreadsheet-smoke-test`.
- Rhino live smoke command is unique: `McpTakeoffSpreadsheetSmoke`.
- `mcp-tool-overlap-cleanup-smoke-test` and `mcp-surface-structure-governance-smoke-test` pass after updated tool counts.

## Risks And Rollback

- Risk: "flexible" becomes unsafe if implemented as arbitrary formulas or code.
  - Mitigation: use a constrained declarative spec and fail unsupported expressions.
- Risk: the agent over-interprets ambiguous natural-language requests.
  - Mitigation: discovery returns candidate keys and sample values; the agent returns structured clarification questions instead of guessing.
- Risk: large take-offs block Rhino UI.
  - Mitigation: collect only live snapshots on UI thread, cap row counts, and compute/write outside the live callback.
- Risk: geometry metrics may not match user take-off rules for some panel systems.
  - Mitigation: require the quantity source to be explicit when more than one plausible source exists; allow explicit attribute-derived quantity columns and record metric-source warnings.
- Risk: XLSX writer dependencies conflict with Rhino plugin loading or `TreatWarningsAsErrors`.
  - Mitigation: keep writer behind an interface, validate Debug and Release plugin builds, and allow CSV-first EXET deviation if needed.
- Risk: external overwrite surprises users.
  - Mitigation: require explicit output folder and file name, strict output validation, overwrite flag, preview-first policy, warnings, and destructive open-world safety annotation.

Rollback is a normal git revert of the new tools, contracts, agent/skill if added, services, writers, DI registrations, package reference if any, smoke command, and `Project_Test/260604_TEST_takeoff-spreadsheet/`. Because the capability should not mutate Rhino documents, rollback does not require Rhino model recovery.

## Future Extensions

- Add reusable named take-off templates for common panel, door, window, finish, or facade schedules.
- Add template storage only after architecture review, because persisted templates may become a separate resource/config capability.
- Add spreadsheet import as a separate preview/apply capability for controlled user text or attribute updates.
- Add cost-rate tables only as a separate open-world/reference capability once external data-source rules are defined.
- Add block instance, nested block, and assembly-aware take-offs.
- Add richer unit conversion and rounding policy presets.
- Add runtime audit coverage for `RunTakeoffSpreadsheetAgent`, `PreviewTakeoffSchedule`, and `ExportTakeoffSchedule`.
