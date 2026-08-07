# Grasshopper Authoring Tools PLAN

## Background

McNeel's RhinoMCP exposes parallel Grasshopper 1 and Grasshopper 2 tool families for:

- starting the Grasshopper UI
- searching the installed component catalog
- describing component inputs and outputs
- placing components and number sliders
- connecting one or many wires
- applying a complete graph in one call
- reading the active canvas graph and bounded volatile-data samples
- solving a definition and returning component diagnostics
- clearing a canvas with an explicit destructive confirmation

The current MCP_Rhino server has no Grasshopper-facing tool, adapter, contract, or test surface.
It targets Rhino 8 on Windows and references only RhinoCommon from the Rhino installation. Rhino 8
includes Grasshopper 1 and the local installation contains `Grasshopper.dll` and `GH_IO.dll`, so a
GH1 capability is feasible without changing the supported Rhino version. Grasshopper 2 requires a
Rhino 9 target and is not present in the current environment.

This capability will add the McNeel-equivalent GH1 authoring operations while retaining this
repository's stricter contracts:

- Router-only transport and explicit saved-Rhino-document selection
- Live Only execution with no `.gh` / `.ghx` disk fallback
- explicit Grasshopper definition identity instead of `Instances.ActiveCanvas`
- typed requests and responses instead of JSON serialized into text
- preview/apply pairs for ambiguous component resolution and destructive clearing
- stale-preview rejection
- bounded graph and volatile-data payloads
- explicit MCP safety annotations, including open-world signaling when third-party component code can
  execute
- one native Grasshopper undo record per successful graph mutation

The public contracts will reserve an engine discriminator so a later GH2 adapter can use the same
canonical tool names. This PLAN does not pretend that GH2 is implementable in the current Rhino 8
package.

## Goals

- Add a canonical MCP tool family under `Tools/Grasshopper` covering the useful behavior of
  McNeel's `g1_*` authoring tools.
- Let a caller discover every live GH1 definition in the selected Rhino process and target one by an
  opaque definition session id.
- Never infer a mutation target from the foreground window, active Rhino tab, or active Grasshopper
  canvas.
- Search and describe built-in and installed third-party components using stable component GUIDs,
  with name matching available only as a preview-time convenience.
- Read a structured graph snapshot containing components, parameters, wires, runtime messages, and
  bounded optional data samples.
- Preview and atomically apply a batch graph specification containing components, number sliders,
  and wires.
- Solve a definition and return per-object warning/error diagnostics suitable for an agentic
  build-solve-inspect-correct loop.
- Preview and apply a full-canvas clear with revision checking and a destructive annotation.
- Use Grasshopper's own undo system so one successful Apply call produces one coherent Grasshopper
  undo entry.
- Keep the Rhino plugin safe when Grasshopper has not yet been loaded, and load/resolve the GH1
  runtime lazily without opening UI during MCP_Rhino startup.
- Preserve the existing Router, route attestation, document selection, resources, and all non-GH
  tool schemas.
- Produce the matching TEST and EXET artifacts during execution.

## Non-Goals

- Do not add Rhino 9 or Grasshopper 2 binaries to the current Rhino 8 package in this capability.
- Do not copy McNeel's separate `g1_place_component`, `g1_place_slider`, `g1_connect`, and
  `g1_connect_many` tools when one structured preview/apply graph contract covers those operations.
- Do not use `Grasshopper.Instances.ActiveCanvas` as the authority for a read or mutation target.
- Do not read, open, save, export, or rewrite `.gh` / `.ghx` definitions on disk.
- Do not add arbitrary Python, C#, or script-source creation/editing to Grasshopper components.
  Installed script/code components remain valid discovery and placement targets.
- Do not expose component constructors, reflection, or raw Grasshopper/RhinoCommon objects through
  MCP contracts.
- Do not bake Grasshopper preview geometry into the Rhino document. Baking is a separate destructive
  Rhino mutation capability requiring its own preview/apply design.
- Do not add an embedded chat panel, Windows UI automation, alternate transport, Router daemon, or
  process-spawning behavior.
- Do not silently enable obsolete or hidden components. Installed script/code components are
  supported but must be explicitly classified in discovery, preview, and solve results.
- Do not promise that a graph with valid structure will solve without component-level warnings or
  errors. Solve diagnostics are a supported result, not necessarily an Apply failure.
- Do not refactor unrelated existing tool families while adding Grasshopper support.

## McNeel Capability Mapping

| McNeel tool | MCP_Rhino canonical capability |
| --- | --- |
| `g1_start` | `StartGrasshopper` |
| `g1_search_components` | `SearchGrasshopperComponents` |
| `g1_describe_component` | `DescribeGrasshopperComponent` |
| `g1_get_canvas_graph` | `GetGrasshopperGraph` |
| `g1_place_component` | `PreviewApplyGrasshopperGraph` -> `ApplyGrasshopperGraph` |
| `g1_place_slider` | `PreviewApplyGrasshopperGraph` -> `ApplyGrasshopperGraph` |
| `g1_connect` | `PreviewApplyGrasshopperGraph` -> `ApplyGrasshopperGraph` |
| `g1_connect_many` | `PreviewApplyGrasshopperGraph` -> `ApplyGrasshopperGraph` |
| `g1_apply_graph` | `PreviewApplyGrasshopperGraph` -> `ApplyGrasshopperGraph` |
| `g1_solve_graph` | `SolveGrasshopperDefinition` |
| `g1_clear_canvas` | `PreviewClearGrasshopperDefinition` -> `ApplyClearGrasshopperDefinition` |
| McNeel's implicit active canvas | `ListGrasshopperDefinitions` plus required `definitionSessionId` |
| McNeel `g2_*` equivalents | same canonical contracts through a future GH2 adapter after Rhino 9 support |

This mapping intentionally reduces tool count while preserving the supported operations. It follows
the repository rule against equivalent thin wrappers when one structured tool provides safer and
more complete routing metadata.

## Architecture Ownership

- `Tools/Grasshopper/`: thin MCP wrappers for definition discovery, component catalog reads, graph
  reads, preview/apply authoring, solve, and preview/apply clear operations.
- `Contracts/Requests/`: engine, definition target, graph node, slider, wire, preview/apply, solve,
  and bounded data-sampling request DTOs.
- `Contracts/Responses/`: definition inventory, component catalog, parameter description, graph
  snapshot, preview, apply, solve diagnostics, and clear results.
- `Domain/Models/Grasshopper/`: RhinoCommon- and Grasshopper-free graph specifications, resolved
  component identities, definition revision values, limits, and diagnostic enums.
- `Application/Interfaces/`: live Grasshopper operator abstraction. The interface uses Domain and
  Contract types only and exposes no Grasshopper SDK type.
- `Application/Services/`: validation, component-resolution policy, graph preflight, revision
  checking, and orchestration.
- `Infrastructure/Rhino/Live/Grasshopper/`: GH1 SDK access, runtime/definition discovery, component
  proxy access, graph mutation, solve, diagnostics, bounded data projection, and native GH undo.
- `Server/DependencyInjection.cs`: register the application service and live adapter without adding
  a second tool catalog.
- `MCP_Rhino.Server.csproj`: compile against Rhino-provided Grasshopper assemblies without packaging
  duplicate copies.
- `Infrastructure/Plugin/PluginLoadContext.cs`: share Grasshopper-owned assemblies with Rhino's
  default load context and prevent duplicate GH type identity.
- `Project_Guides/MCP_Rhino Architecture.md`: add `Tools/Grasshopper` ownership and define the
  Grasshopper-native undo exception to the Rhino-document undo rule.
- `Project_Test/260805_TEST_grasshopper-authoring-tools/`: API/load-context probe, CLI/live smoke,
  request samples, and README.

No change is expected in `MCP_Rhino.Router` or `MCP_Rhino.Transport`. The Router must forward the new
canonical server tool metadata through its existing dynamic backend surface.

## Key Design

### 1. Phase 0 API And Load-Context Probe

Execution begins with a probe against the installed Rhino 8 assemblies before production types are
introduced. Record the exact assembly versions and verified API names in the TEST README and EXET.

The probe must establish:

- the Rhino-supported way to load the Grasshopper plug-in assembly without opening the Grasshopper
  UI during MCP_Rhino startup
- how `Grasshopper.dll`, `GH_IO.dll`, and any transitive GH assemblies are loaded in Rhino's default
  `AssemblyLoadContext`
- how the isolated MCP_Rhino runtime can share those assemblies without loading a second copy
- how to enumerate all live `GH_Document` instances, including definitions not currently shown by
  the active canvas
- the stable in-memory document id and close/dispose lifecycle events available in GH1
- how a GH definition identifies or binds its Rhino document / solution context
- the exact component-server proxy APIs for search, GUID resolution, hidden/obsolete state, and
  safe parameter description
- the exact native GH undo APIs for grouped object addition, wire changes, and full-canvas clearing
- how to suspend intermediate solutions while a graph batch is assembled and request exactly one
  final solution
- how to collect runtime messages and bounded volatile-data summaries without mutating the graph
- whether component description requires constructing third-party component instances

If the probe cannot prove a definition's association with the Router-selected Rhino document, stop
execution and report `GRASSHOPPER_TARGET_UNVERIFIED`. Do not weaken the plan to active-canvas or
foreground-window targeting.

### 2. Version-Neutral Engine Contract

Use a `GrasshopperEngine` discriminator with `Gh1` and reserved `Gh2` values in canonical requests
and responses. The current adapter supports only `Gh1`.

- `Gh1` operates through the Rhino 8 Grasshopper SDK.
- `Gh2` returns `GRASSHOPPER_ENGINE_UNAVAILABLE` without loading assemblies or mutating state.
- Tool names remain engine-neutral so adding GH2 later does not duplicate the public surface.
- Engine-specific differences such as GH1 NickName versus GH2 UserName stay inside adapters and are
  normalized into canonical contract fields.

Adding real GH2 support requires a separate confirmed capability after Rhino 9 multi-target build,
packaging, plug-in identity, and route compatibility are established.

### 3. Deterministic Definition Targeting

`ListGrasshopperDefinitions(filePath, engine)` returns live definitions in the exact Rhino process
selected by the Router. Each entry includes:

- opaque `definitionSessionId`
- engine
- display name
- native document id for diagnostics only
- saved/unsaved definition state, without reading its disk file
- associated Rhino runtime serial and target-binding status
- object and wire counts
- solution state
- current `definitionRevision`

Every graph read, preview, mutation, solve, or clear call requires `definitionSessionId`. The live
adapter validates on every call that:

- the definition still exists
- its engine matches
- its lifecycle generation has not changed
- it remains associated with the routed Rhino document

Closing and reopening a definition invalidates its prior opaque id. No tool follows a replacement
definition, active canvas, or newly focused window.

`StartGrasshopper` starts the GH1 UI through a Rhino/Grasshopper API or Rhino command verified by the
probe; it is not Windows UI automation. It returns engine status and the resulting definition
inventory. It does not create a definition implicitly unless GH1 itself creates its standard blank
definition.

### 4. Canonical MCP Tool Surface

Add these method-level MCP tools with complete `[Description]` metadata and explicit annotations:

| Tool | ReadOnly | Destructive | OpenWorld | Purpose |
| --- | ---: | ---: | ---: | --- |
| `ListGrasshopperDefinitions` | true | false | false | enumerate deterministic live targets |
| `StartGrasshopper` | false | false | false | load/show the requested available engine |
| `SearchGrasshopperComponents` | true | false | false | search proxy metadata without placing objects |
| `DescribeGrasshopperComponent` | true | false | true | inspect ports; may instantiate third-party component code |
| `GetGrasshopperGraph` | true | false | false | read nodes, wires, messages, and bounded data summaries |
| `PreviewApplyGrasshopperGraph` | true | false | true | resolve and validate a graph spec without document mutation |
| `ApplyGrasshopperGraph` | false | false | true | add nodes/sliders/wires and optionally solve once |
| `SolveGrasshopperDefinition` | false | false | true | execute installed component code and return diagnostics |
| `PreviewClearGrasshopperDefinition` | true | false | false | report objects/wires that would be removed |
| `ApplyClearGrasshopperDefinition` | false | true | false | clear exactly the previewed definition revision |

`OpenWorld = true` is required where constructing or solving installed third-party components may
touch filesystem, network, processes, or other external state even though MCP_Rhino itself does not
perform those operations directly.

### 5. Component Discovery And Resolution

Search matches component proxy name, nickname, description, category, and subcategory with bounded
results. Responses include:

- stable component GUID / proxy id
- name and nickname
- category and subcategory
- description
- object kind
- assembly / plug-in identity when available
- hidden, obsolete, and script/code classification

Defaults:

- exclude hidden components
- exclude obsolete components
- include installed script/code components and classify their language/host and assembly identity
  when the Grasshopper SDK exposes them
- cap results at a domain-owned maximum

Placement prefers exact component GUIDs. A name selector is permitted in
`PreviewApplyGrasshopperGraph`, but ambiguous names return candidates and no preview token.
`ApplyGrasshopperGraph` consumes the preview's resolved GUIDs; it never repeats fuzzy name
resolution.

`DescribeGrasshopperComponent` reports normalized inputs and outputs including index, name,
nickname, description, access mode, optional/required state, and type hint where the SDK exposes it.
It must dispose any temporary instance and must never add it to a live definition.

### 6. Typed Graph Specification

The request model contains no Grasshopper SDK types:

- `GrasshopperGraphSpec`
  - `Nodes`
  - `Wires`
  - `SolveAfterApply`
- `GrasshopperNodeSpec`
  - caller-unique `ClientKey`
  - node kind: `Component` or `NumberSlider`
  - exact component GUID for components
  - optional existing object id when wiring to an already present object
  - canvas X/Y position
  - optional display nickname
  - slider range/value/accuracy for number sliders
- `GrasshopperWireSpec`
  - source node client key or existing object id
  - source parameter selector
  - destination node client key or existing object id
  - destination parameter selector

Parameter selectors resolve in this order:

1. exact zero-based index
2. exact normalized name
3. exact normalized nickname

Ambiguous or missing selectors fail preview. Empty / `0` pure-parameter shorthand is normalized in
the application layer rather than leaking GH1-specific conventions through every adapter.

The preview response includes resolved component GUIDs, resolved port indices, predicted additions,
wire count, warnings, the current definition revision, and an opaque `previewToken` bound to the
target, graph spec, resolved identities, and revision.

### 7. Preview, Revision, Apply, And Failure Semantics

`PreviewApplyGrasshopperGraph` must not mutate a live definition, start a solution, create an undo
record, change selection, or change the visible canvas.

`ApplyGrasshopperGraph` requires the preview token and rejects:

- another definition
- another engine
- a changed graph specification
- a changed component resolution
- a changed definition revision
- an expired or unknown target lifecycle

Use `GRASSHOPPER_GRAPH_CHANGED` for stale revisions. The caller must preview again.

Apply prevalidates the complete node and wire set before mutation, suspends intermediate solutions,
and commits nodes and wires as one batch. Structural or operational failure rolls back additions and
wires. The result must state whether rollback completed; a rollback failure returns
`GRASSHOPPER_ROLLBACK_FAILED` and does not claim success.

Solve diagnostics do not automatically roll back a structurally applied graph. A successful Apply
may return `Solved = false` plus actionable diagnostics so the agent can inspect and correct the
definition.

`PreviewClearGrasshopperDefinition` returns the exact object ids, wire count, and definition
revision. `ApplyClearGrasshopperDefinition` requires its preview token and performs no work if the
revision changed.

### 8. Threading, Undo, And Solve Contract

All Grasshopper API access runs inside the live Rhino execution boundary on the Rhino UI thread.
The adapter must not access GH documents from MCP worker threads.

Grasshopper graph state has its own undo stack, distinct from `RhinoDoc` geometry state:

- one successful `ApplyGrasshopperGraph` call creates one native GH undo entry
- one successful `ApplyClearGrasshopperDefinition` call creates one native GH undo entry
- preview and read calls create no undo record
- solve-only calls create no graph undo entry unless GH1 itself changes persistent definition state
- no empty Rhino document undo record is opened for a GH-only mutation
- future bake tools must use Rhino document undo separately

The Architecture Guide must record this narrow exception so the repository does not incorrectly
claim that a Rhino `BeginUndoRecord` can roll back Grasshopper definition edits.

Apply suppresses per-node solutions and requests at most one final solution when
`SolveAfterApply = true`. `SolveGrasshopperDefinition` returns:

- completion phase
- solved boolean
- warning/error/fault counts
- per-object diagnostics with id, name, nickname, severity, and message
- graph revision
- bounded output summaries when requested

### 9. Assembly And Plug-In Loading

The production plugin loads at Rhino startup before the user necessarily starts Grasshopper. Adding
compile-time GH references must not make MCP_Rhino startup depend on Grasshopper already being
active.

Implementation requirements:

- reference the Rhino-provided GH1 assemblies with `Private = false`
- never package duplicate `Grasshopper.dll`, `GH_IO.dll`, RhinoCommon, Eto, or Rhino UI assemblies
- extend `PluginLoadContext` sharing rules for the exact GH assembly names proven by the probe
- load the Grasshopper plug-in lazily on the first GH request through a Rhino-supported mechanism
- avoid resolving GH implementation types while the MCP tool assembly is being scanned if the GH
  runtime is not yet loaded
- if direct implementation types would make assembly scanning eager, isolate them behind a lazily
  loaded adapter boundary rather than using broad reflection throughout business logic
- return `GRASSHOPPER_RUNTIME_UNAVAILABLE` without breaking non-GH MCP tools when loading fails

The final structure must be the smallest design that passes startup, type-identity, and unload tests.
A separate project is not authorized by this PLAN unless the Phase 0 probe proves that an in-project
adapter cannot satisfy the isolated load-context boundary; that material deviation requires review
and must be recorded in EXET or a revised PLAN.

### 10. Bounds And Safety Policy

Initial domain limits:

- at most 250 new nodes per Apply
- at most 1,000 new wires per Apply
- at most 100 component-search results
- data sample size from 0 through 20 items per parameter
- a bounded total number of sampled branches/items per graph response
- explicit truncation flags and original counts in every truncated response

The Phase 0 probe may lower a limit for stability. Increasing these limits is not permitted silently;
record any change in EXET.

Script/code component policy:

- search and describe include installed script/code components using the same bounded discovery
  rules as other installed components
- preview and apply allow their placement by exact component GUID or an unambiguous selector
- discovery, preview, graph, and solve results identify them as executable-code components and
  report language/host and assembly identity when available
- preview emits an explicit executable-code warning; placement and solve remain `OpenWorld = true`
- no request field accepts raw script source in this capability; source creation/editing is outside
  scope, but placement of an installed script/code component is not blocked

Third-party compiled components remain supported. Preview and solve responses identify their
assembly/plug-in when available, and the relevant tools remain `OpenWorld = true`.

### 11. Error Contract

Return stable machine-readable messages through the existing `OperationResponse<T>` pattern. At
minimum cover:

- `GRASSHOPPER_RUNTIME_UNAVAILABLE`
- `GRASSHOPPER_ENGINE_UNAVAILABLE`
- `GRASSHOPPER_TARGET_UNVERIFIED`
- `GRASSHOPPER_DEFINITION_NOT_FOUND`
- `GRASSHOPPER_DEFINITION_TARGET_CONFLICT`
- `GRASSHOPPER_COMPONENT_NOT_FOUND`
- `GRASSHOPPER_COMPONENT_AMBIGUOUS`
- `GRASSHOPPER_PARAMETER_NOT_FOUND`
- `GRASSHOPPER_PARAMETER_AMBIGUOUS`
- `GRASSHOPPER_GRAPH_CHANGED`
- `GRASSHOPPER_GRAPH_LIMIT_EXCEEDED`
- `GRASSHOPPER_APPLY_FAILED`
- `GRASSHOPPER_ROLLBACK_FAILED`
- `GRASSHOPPER_SOLVE_FAILED`

Do not return raw exception stacks or locale-dependent Grasshopper UI strings as the only error
signal.

## Likely Involved Files

### Additions

- `src/MCP_Rhino.Server/Domain/Models/Grasshopper/GrasshopperEngine.cs`
- `src/MCP_Rhino.Server/Domain/Models/Grasshopper/GrasshopperGraphModels.cs`
- `src/MCP_Rhino.Server/Domain/Models/Grasshopper/GrasshopperLimits.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/GrasshopperRequests.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/GrasshopperResponses.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveGrasshopperOperator.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoGrasshopperAuthoringService.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/Grasshopper/LiveGrasshopperOperator.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/Grasshopper/GrasshopperDefinitionRegistry.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/Grasshopper/GrasshopperGraphProjector.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/Grasshopper/GrasshopperComponentCatalog.cs`
- `src/MCP_Rhino.Server/Tools/Grasshopper/ListGrasshopperDefinitionsTool.cs`
- `src/MCP_Rhino.Server/Tools/Grasshopper/StartGrasshopperTool.cs`
- `src/MCP_Rhino.Server/Tools/Grasshopper/SearchGrasshopperComponentsTool.cs`
- `src/MCP_Rhino.Server/Tools/Grasshopper/DescribeGrasshopperComponentTool.cs`
- `src/MCP_Rhino.Server/Tools/Grasshopper/GetGrasshopperGraphTool.cs`
- `src/MCP_Rhino.Server/Tools/Grasshopper/PreviewApplyGrasshopperGraphTool.cs`
- `src/MCP_Rhino.Server/Tools/Grasshopper/ApplyGrasshopperGraphTool.cs`
- `src/MCP_Rhino.Server/Tools/Grasshopper/SolveGrasshopperDefinitionTool.cs`
- `src/MCP_Rhino.Server/Tools/Grasshopper/PreviewClearGrasshopperDefinitionTool.cs`
- `src/MCP_Rhino.Server/Tools/Grasshopper/ApplyClearGrasshopperDefinitionTool.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpGrasshopperAuthoringToolsSmokeCommand.cs`
- `Project_Test/260805_TEST_grasshopper-authoring-tools/DeveloperCommandHandler.GrasshopperAuthoringToolsSmokeTest.cs`
- `Project_Test/260805_TEST_grasshopper-authoring-tools/README.md`
- `Project_Test/260805_TEST_grasshopper-authoring-tools/grasshopper-graph-request.json`

The implementation may combine closely related infrastructure files when that reduces unearned
abstraction. It must not combine tool wrappers into a large multi-tool file.

### Modifications

- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/PluginLoadContext.cs`
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs`
- `Project_Guides/MCP_Rhino Architecture.md`
- `README.md`
- tool inventory and safety smoke expectations affected by the expanded MCP surface

### Required Construction Artifacts

- `Project_Plan/260805_PLAN_grasshopper-authoring-tools.md`
- `Project_Test/260805_TEST_grasshopper-authoring-tools/`
- `Project_Exet/260805_EXET_grasshopper-authoring-tools.md`

The TEST folder owns the unique CLI slug `grasshopper-authoring-tools-smoke-test`. Its registration
partial implements `RegisterGrasshopperAuthoringToolsHandlers()` and is invoked by the single
`RegisterExtensionHandlers()` implementation in `DeveloperCommandHandler.cs`.

The corresponding Rhino command is `_McpGrasshopperAuthoringToolsSmoke`. No second live-only slug or
shared smoke command is introduced.

## Execution Sequence

1. Create the matching TEST folder and run the Phase 0 GH1 API/load-context probe.
2. Stop and report a target-binding gap if a live GH definition cannot be tied reliably to the
   Router-selected Rhino document.
3. Add engine-neutral Domain and Contract types with serialization/validation smoke coverage.
4. Add the application service and fake-adapter tests for bounds, component ambiguity, parameter
   resolution, revision checking, and preview-token binding.
5. Add the lazy GH1 live adapter, definition registry, component catalog, graph projection, native
   undo, and solve diagnostics.
6. Add the ten thin MCP wrappers with exact safety annotations and routing descriptions.
7. Register DI, load-context sharing, CLI smoke hook, and Rhino smoke command.
8. Run static/CLI smokes, then Debug and Release solution builds.
9. Run the live Rhino/Router smoke against a saved anonymous `.3dm` fixture and a live unsaved GH1
   definition; do not commit `.3dm` or `.gh` files.
10. Update architecture/runtime documentation and write the matching EXET with actual commands,
    outputs, deviations, and rollback verification.

## Intended Usage

Example agent flow:

1. Call `rhino_router_list_documents` and `rhino_router_select_document` for the exact saved Rhino
   document.
2. Call `StartGrasshopper(filePath, engine = Gh1)` if GH1 is not loaded.
3. Call `ListGrasshopperDefinitions` and select the returned `definitionSessionId`.
4. Call `SearchGrasshopperComponents` and `DescribeGrasshopperComponent`; use exact component GUIDs.
5. Call `GetGrasshopperGraph` to inspect existing nodes and ids.
6. Call `PreviewApplyGrasshopperGraph` with sliders, components, and wires.
7. Review resolved components, port indices, limits, warnings, target revision, and preview token.
8. Call `ApplyGrasshopperGraph` with the unchanged specification and preview token.
9. Read the returned solve diagnostics. If needed, inspect the graph, preview a correction, and
   apply again.
10. To clear the definition, call `PreviewClearGrasshopperDefinition`, then
    `ApplyClearGrasshopperDefinition` with its token.

No step depends on which Grasshopper canvas or Rhino window is visually active.

## Acceptance Criteria

### Build And Surface Validation

- `dotnet build .\MCP_Rhino.sln -c Debug` succeeds.
- `dotnet build .\MCP_Rhino.sln -c Release` succeeds.
- Normal Rhino startup succeeds when Grasshopper has never been opened.
- The plugin package does not contain duplicate RhinoCommon, Grasshopper, GH_IO, Rhino UI, or Eto
  assemblies.
- Loading GH1 after MCP_Rhino startup produces one shared Grasshopper type identity and does not
  break existing tools.
- All ten canonical tools appear exactly once with non-empty descriptions and the annotations in
  this PLAN.
- Tool inventory and MCP safety annotation smokes pass in Debug and Release.
- The Router forwards the new tool schemas and results without Router/Transport business changes.
- Existing tool and resource inventories remain otherwise unchanged.
- `engine = Gh2` returns `GRASSHOPPER_ENGINE_UNAVAILABLE` without mutation in the Rhino 8 build.
- The unique smoke slug and `_McpGrasshopperAuthoringToolsSmoke` command satisfy the Plan Log
  ownership contract.

### Contract And Validation Smokes

- All Grasshopper MCP inputs and outputs serialize as typed JSON without Grasshopper SDK objects.
- Duplicate node client keys are rejected.
- Invalid slider ranges/values/accuracy are rejected before mutation.
- Graphs above node, wire, search, or data-sample limits are rejected or truncated according to the
  documented contract.
- Component GUID resolution is deterministic.
- Ambiguous name resolution returns candidates and no preview token.
- Hidden and obsolete components are excluded by default; installed script/code components are
  included and explicitly classified.
- Script/code component placement succeeds for an exact GUID or unambiguous selector, preview
  reports the executable-code warning, and the relevant apply/solve tools remain open-world.
- No graph request field accepts raw script source text.
- Parameter index/name/nickname resolution follows the documented precedence and reports ambiguity.
- Preview tokens are bound to engine, definition session, lifecycle, graph specification, resolved
  component/port identities, and definition revision.
- A changed graph or definition invalidates a preview with `GRASSHOPPER_GRAPH_CHANGED`.
- Preview methods do not mutate object count, wire count, solution state, canvas selection, or undo
  state.

### Live GH1 Validation

- Through the Router-selected saved Rhino 8 fixture, `StartGrasshopper` opens GH1 without Windows UI
  automation and returns a live definition inventory.
- With two live definitions, `ListGrasshopperDefinitions` returns distinct opaque session ids and
  every read/mutation affects only the explicitly requested definition.
- Bringing another Rhino or Grasshopper window to the foreground does not change a tool target.
- Closing and reopening a definition invalidates the old session id.
- Search finds stable built-in components and returns exact proxy GUIDs.
- Describe returns normalized input/output metadata without adding an object to the definition.
- Previewing a graph containing two number sliders, one arithmetic component, and two wires reports
  the expected additions without mutation.
- Applying that preview creates exactly the predicted objects and wires and returns the
  client-key-to-object-id mapping.
- The Apply creates one native GH undo entry. One GH undo removes the complete applied batch.
- Apply with a structural/runtime failure leaves no partial nodes or wires; rollback status is
  explicit.
- `SolveGrasshopperDefinition` completes and reports zero errors for the valid smoke graph.
- A deliberately invalid component setup remains in the graph but returns object-level diagnostics
  sufficient to locate and correct the failure.
- `GetGrasshopperGraph` reports objects, wires, messages, and bounded data samples with truncation
  metadata.
- Preview Clear reports exact impact. Apply Clear at the same revision removes it in one GH undo
  entry; stale Clear is rejected.
- No GH-only operation creates an empty Rhino document Undo entry or changes unrelated Rhino
  geometry.
- Non-GH MCP tools continue to operate before and after loading/using/closing Grasshopper.

### Multi-Document And Failure Validation

- A definition that cannot be proven to belong to the routed Rhino document returns
  `GRASSHOPPER_TARGET_UNVERIFIED` and is not mutated.
- A selected Router session plus conflicting `filePath` is rejected by the existing Router before
  the GH adapter is invoked.
- Definitions in another Rhino process are never returned by the selected endpoint.
- A blocked Rhino UI thread returns `RHINO_MAIN_THREAD_BUSY`; the tool does not retry a mutation.
- A post-dispatch Router disconnect preserves the existing `MUTATION_OUTCOME_UNKNOWN` behavior and
  never replays an Apply/Clear/Solve call.
- Grasshopper unavailable/load failure returns a typed error while ordinary Rhino tools remain
  usable.
- Router stdout remains MCP-only and plugin diagnostics do not expose full user paths or component
  data.

### Documentation And Artifact Validation

- `Project_Guides/MCP_Rhino Architecture.md` owns `Tools/Grasshopper` and documents GH-native undo.
- README usage requires Router document selection and Grasshopper definition selection.
- TEST README records exact Rhino, Grasshopper, RhinoCommon, Grasshopper assembly, and GH_IO
  versions used.
- TEST README and EXET contain actual Debug/Release build commands and live smoke results.
- `Project_Test/260805_TEST_grasshopper-authoring-tools/` contains no committed `.3dm`, `.gh`, or
  `.ghx` user fixture.
- `Project_Exet/260805_EXET_grasshopper-authoring-tools.md` records implementation scope, deviations,
  test evidence, rollback validation, and the deferred GH2 status.

## Risks And Mitigations

- **Grasshopper assembly load-order failure**: MCP_Rhino loads at Rhino startup before GH1. Mitigate
  with the Phase 0 load-context probe, lazy runtime loading, default-context sharing, and startup
  tests where GH1 has never been opened.
- **Duplicate Grasshopper type identity**: loading GH assemblies inside the isolated MCP context
  would make SDK objects incompatible with Rhino's GH plug-in. Mitigate by sharing exact assembly
  names with the default context and rejecting duplicate packaged assemblies.
- **Wrong-canvas mutation**: McNeel's active-canvas approach can follow focus. Mitigate with opaque
  definition sessions, lifecycle generations, routed Rhino binding, and per-call validation.
- **GH definition cannot be bound to one Rhino document**: GH1 may expose process-global state that
  is weaker than the Router's per-document contract. Mitigate by proving the association in Phase 0
  and stopping rather than falling back when it cannot be verified.
- **Third-party component side effects**: constructors and solutions may access external state.
  Mitigate with `OpenWorld = true`, explicit component and executable-code classification in
  preview, exact resolved identities, no raw script-source injection, and bounded solve execution.
- **Partial graph application**: a placement or wire can fail after earlier changes. Mitigate with
  full prevalidation, solution suspension, one native undo group, compensation/undo on failure, and
  explicit rollback status.
- **Stale preview**: the user or another agent may edit the definition between preview and apply.
  Mitigate with revision fingerprints and preview-token binding.
- **Large graphs or data trees**: unbounded snapshots can block Rhino's UI thread or overflow MCP
  context. Mitigate with hard limits, bounded samples, truncation metadata, and performance smoke.
- **Component-name localization or duplication**: names are not stable identifiers. Mitigate by
  preferring GUIDs and making ambiguous name matches preview failures.
- **Solve reentrancy and event storms**: placing nodes individually may trigger repeated solutions.
  Mitigate by using verified GH solution suspension APIs and one optional solve at batch completion.
- **Undo mismatch**: Rhino Undo cannot roll back GH canvas edits. Mitigate by using native GH undo
  and updating the Architecture Guide instead of faking Rhino undo coverage.
- **Tool-surface growth**: exposing every McNeel single-operation alias would add routing noise.
  Mitigate by the ten canonical tools and one graph preview/apply workflow.
- **GH2 schedule pressure**: current Rhino 8 cannot load GH2. Mitigate by reserving engine-neutral
  contracts and treating GH2 as a separate gated capability rather than weakening build guarantees.

## Rollback Plan

- Remove the ten `Tools/Grasshopper` wrappers.
- Remove the Grasshopper requests, responses, Domain models, application service/interface, live
  adapter, definition registry, and DI registrations.
- Remove Rhino-provided Grasshopper/GH_IO references and restore the prior `PluginLoadContext`
  sharing list.
- Remove the Grasshopper CLI hook, dedicated Rhino smoke command, and matching TEST folder.
- Restore tool inventory/safety expectations and remove the Grasshopper architecture/README text.
- Re-run Debug and Release builds, the Router protocol smoke, tool inventory/safety smokes, and one
  existing non-GH live mutation smoke.
- Verify Rhino starts normally and all previous MCP tools/resources remain available.
- No Rhino `.3dm` or Grasshopper file migration is required because the capability never owns or
  rewrites user definition files.

## Follow-Up Extensions

- A separate Rhino 8/9 multi-target runtime and packaging capability.
- A GH2 live adapter behind the same canonical tools after Rhino 9 support exists.
- Graph patching for moving, renaming, updating, replacing, or deleting selected nodes.
- Preview/apply baking with Rhino document Undo and exact object/layer/material attribution.
- Group, scribble, relay, panel, gene-pool, and other higher-level graph authoring nodes.
- Definition save/export as an explicit open-world file capability after separate review.
- Component allow/deny policy by plug-in, publisher, or assembly.
- Optional script source creation/editing only after a dedicated arbitrary-code security design;
  installed script/code component discovery and placement are already in scope.
- A fixed `GrasshopperGraphAuthoringSkill` for search -> describe -> preview -> apply -> solve ->
  diagnose workflows after Runtime_Log evidence meets the repository's skill-candidate threshold.
- A goal-driven Grasshopper correction agent only if repeated dynamic solve/diagnose/repair behavior
  cannot be handled reliably by clients using the atomic tools.

## 修订记录（2026-08-05）

- User-directed revision: installed Grasshopper script/code components are no longer blocked from
  discovery, preview, placement, or solve.
- They remain explicitly classified, preview emits an executable-code warning, and the relevant
  tools remain `OpenWorld = true`.
- Arbitrary script source creation/editing remains outside this capability because it is not part of
  McNeel's corresponding component placement/apply contract.
