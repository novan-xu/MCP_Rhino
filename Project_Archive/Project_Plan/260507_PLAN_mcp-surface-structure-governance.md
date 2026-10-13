# MCP Surface Structure Governance Plan

## Background

The current MCP_Rhino tool surface has grown well beyond a small proof-of-concept set. Source inspection shows more than one hundred method-level MCP tools across Analysis, Geometry, Blocks, Layers, File, Drawing, Editing, and Workflow. The existing assembly-reflection registration model is good for this repository because all live Rhino execution already runs inside the C# plugin host, and each tool can carry explicit MCP safety annotations.

The external `rhinomcp` project uses a Python FastMCP facade, a C# socket command dispatcher, and JSON schema contracts. That split is not a good structural fit for MCP_Rhino because this repository already owns an in-process C# MCP server, live-only document access, named-pipe bridge compatibility, panel-bound execution, and explicit MCP safety metadata. The useful lesson is not to copy the dual Python/C# registry. The useful lesson is to improve tool surface governance so registration, routing descriptions, safety metadata, and optional non-tool MCP resources do not drift.

## Goal

Improve the project structure and rules for managing a large MCP surface without changing runtime behavior in this plan.

The target structure is:

- keep `Server/ToolRegistration.cs` as assembly-reflection based tool registration
- add a first-class rule set for MCP resources if/when static reference resources are introduced
- keep tool routing metadata on `[Description]` attributes instead of maintaining a second prompt-only catalog
- add generated or smoke-tested inventory reporting as validation output, not as a hand-maintained source of truth
- clarify tool family ownership, naming, safety annotations, preview/apply conventions, and documentation expectations in project rules

## Architecture Ownership

- `Server/ToolRegistration.cs`: remains the single tool registration entry point.
- `Server/ResourceRegistration.cs`: proposed future entry point for MCP resources, only if resource support is implemented. This plan owns the registration/rule-layer decision; downstream reference plans must not duplicate that ownership.
- `Tools/`: remains the home for atomic MCP tools that execute or inspect live Rhino state.
- `Resources/`: proposed new directory for MCP resources that expose static or derived reference content and never mutate Rhino.
- `Project_Guides/MCP_Rhino Architecture.md`: must be updated if `Resources/` or any new tool governance rules are accepted.
- `Runtime_Workflow/MCP_Rhino Workflow.md`: must be updated to include resource-vs-tool routing behavior.
- `Project_Test/...`: future validation should include tool/resource inventory smoke tests and safety annotation checks.

## Key Design

### 1. Keep Assembly Discovery

Do not replace the current C# assembly discovery model with a central command dictionary. The current model scales better for this repository because each tool class remains independently injectable, testable, and safety annotated.

`ToolRegistration.AddRhinoTools()` should continue to use `WithToolsFromAssembly(...)`.

### 2. Add Resource Layer Only For Non-Executing Content

If MCP resources are introduced, they should live under `src/MCP_Rhino.Server/Resources/` with `*Resource` naming. Resources are for static or generated reference information, not for Rhino document mutation or live document querying.

Allowed examples:

- RhinoScript or RhinoCommon reference pages
- browsable tool help generated from local metadata
- static modeling policy references

Disallowed examples:

- tools disguised as resources
- live document reads that should be `Tools/Analysis`
- mutation previews that should be tools

### 3. Tool Family Conventions

Tool directories should remain grouped by domain:

- `Tools/Analysis`
- `Tools/Geometry`
- `Tools/Geometry/CurveOps`
- `Tools/Geometry/Edit`
- `Tools/Geometry/Rebuild`
- `Tools/Geometry/Architecture`
- `Tools/Selection`
- `Tools/Viewport`
- `Tools/Reference`
- `Tools/Layers`
- `Tools/Blocks`
- `Tools/File`
- `Tools/Drawing`
- `Tools/Editing`
- `Tools/Workflow`

Proposed ownership for new families:

- `Tools/Geometry/CurveOps`: curve-derived construction and curve segmentation.
- `Tools/Selection`: live Rhino UI selection reads and selection-state mutations.
- `Tools/Viewport`: in-band viewport capture and future viewport-only inspection. File export remains under `Tools/File/Export`.
- `Tools/Reference`: read-only searchable reference tools when MCP resources are not reliable across clients.

Future broad additions should add a subfolder only when there are multiple related tools and a stable capability boundary. One-off tools should join the nearest existing family.

### 4. Routing Metadata Source

Tool and skill routing metadata should remain on C# `[Description]` attributes. Do not add a second hand-maintained tool catalog for runtime routing. If a human-readable inventory is useful, generate it from reflection during smoke tests.

### 5. Safety Metadata Source

Every method-level `[McpServerTool]` remains required to explicitly declare:

- `ReadOnly`
- `Destructive`
- `OpenWorld`

If resources are added, resources must not bypass this model by performing hidden mutations. If resource support has no equivalent safety metadata, the architecture guide must state that resources are reference-only.

### 6. Preview/Apply Rule

Use preview/apply pairs for mutations where:

- the operation can delete or replace existing Rhino objects
- the selection can expand beyond explicitly confirmed object ids
- the result count or geometry validity is uncertain
- the tool depends on ambiguous matching

Creation-only tools may be apply-only when they create new objects without deleting or replacing existing ones.

### 7. Inventory Smoke

Add a future CLI smoke that reports:

- total MCP tools by namespace/family
- tool method name
- class name
- safety annotations
- description presence
- duplicate method names
- optional resource list if resources exist

This report should validate structure and metadata. It should not become the runtime registry.

## Involved Files

Planned rule updates:

- `Project_Guides/MCP_Rhino Architecture.md`
- `Runtime_Workflow/MCP_Rhino Workflow.md`

Likely future implementation files:

- `src/MCP_Rhino.Server/Server/ResourceRegistration.cs`
- `src/MCP_Rhino.Server/Resources/README.md`
- `src/MCP_Rhino.Server/Resources/**`
- `Project_Test/260507_TEST_mcp-surface-structure-governance/DeveloperCommandHandler.McpSurfaceStructureGovernanceSmokeTest.cs`
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs`

No production implementation files are changed by this plan draft.

## Usage

After execution, maintainers should use the project rules this way:

- add atomic Rhino actions under `Tools/`
- add reference-only browsable content under `Resources/`
- keep `ToolRegistration` assembly-based
- register resource support through `ResourceRegistration` if the MCP SDK supports it cleanly
- validate metadata with the structure governance smoke before closing any tool-surface change

## Acceptance Criteria

- Architecture guide documents the MCP resource layer or explicitly rejects it if implementation research shows poor SDK support.
- Runtime workflow documents when a model should use a resource instead of a tool.
- Tool family ownership and preview/apply conventions are documented.
- New families `Tools/Selection`, `Tools/Viewport`, `Tools/Reference`, and `Tools/Geometry/CurveOps` are either documented or explicitly rejected with reasons.
- No hand-maintained runtime tool catalog is introduced.
- A smoke test can enumerate all tools and fail on missing descriptions, duplicate method names, or missing safety metadata.
- `dotnet build .\MCP_Rhino.sln -c Debug` passes.
- `dotnet build .\MCP_Rhino.sln -c Release` passes.
- `dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-tool-safety-annotations-smoke-test` passes.
- `dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-tool-safety-annotations-smoke-test` passes.

## Risks And Rollback

- Risk: adding resources may duplicate tool descriptions or create routing ambiguity.
  - Mitigation: resources must be reference-only and must not be used as routing source.
- Risk: generated inventory becomes treated as a manual registry.
  - Mitigation: inventory is generated during smoke only and is not committed as source unless explicitly documented as a test snapshot.
- Risk: rule updates conflict with existing active plans.
  - Mitigation: execution should read current active plans and avoid changing unrelated construction rules.

Rollback is straightforward: remove the new rule sections, resource registration files, and structure governance smoke. Existing tool registration remains unchanged.

## Future Extension

- Add a generated Markdown tool inventory for release notes only.
- Add CI validation for safety annotations and description coverage.
- Add MCP resource support for Rhino reference content after the resource layer rules are accepted.
