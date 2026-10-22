# MCP_Rhino Workflow

## Purpose

Define the runtime protocol for handling user tasks with existing `tool / skill / agent` capabilities, including task-specific scripts through supported tools, and define when to stop and ask for capability construction under `Project_Guides`.

## Scope

- `Project_Guides/`: how to build or modify capabilities
- `Runtime_Workflow/`: how to use and route existing capabilities
- `Runtime_Log/`: cross-session tool usage history for pattern detection

When adding a new rule file, decide by intent: *how to build* goes to `Project_Guides/`, *how to use* goes to `Runtime_Workflow/`.

## Routing Rules

1. Route tasks autonomously. Do not require the user to name a skill.
2. Prefer MCP `Resource` only for reference-only content that does not inspect, query, export, or mutate the live Rhino document.
3. Prefer `Tool` for atomic live Rhino tasks.
4. Prefer `Skill` for fixed, repeatable multi-step workflows.
5. Prefer `Agent` for goal-driven tasks with branching, sequencing, or cross-skill orchestration.
6. If an atomic tool chain and a skill tool can both complete the task, prefer the skill tool.
7. When existing task-specific tools or skills do not fit, use supported script-execution tools for ad hoc work. Writing and running the task script is runtime work, not capability construction, and does not itself require a PLAN. Script execution remains a Tool path; it is not another capability layer.
8. Assess supported scripting before declaring a capability gap. A missing dedicated tool alone does not justify stopping or constructing a new one. Follow the selected runner's contract and verify its availability; scripting does not bypass document routing, execution-mode limits, or authorization requirements.

## Runtime Steps

### Current MCP Runtime Shape

- For ad hoc live operations not covered by an equivalent atomic tool or skill, use
  `preview_rhino_script` then `apply_rhino_script` on the same Router-selected saved document.
  This accepts trusted synchronous C# with `doc`, `Parameters`, `Result`, `Log`, `CancellationToken`
  and `Checkpoint`. Preview only compiles; validate modeling assumptions inside the script before
  editing. Apply returns an execution id; poll `get_rhino_script_status` to a terminal result rather
  than rerunning code. `cancel_rhino_script` is cooperative, not forced termination. Scripts are
  full trust; assess their external effects. Keep Rhino work on the synchronous main thread and
  use the supplied `doc`, never ActiveDoc, command scripting, manual Undo, save/close or background
  modeling. Ordinary target-document edits share one Undo record and are rolled back on failure;
  external effects are not. No new per-task capability is necessary when a short script suffices.

- Existing Rhino plug-in capabilities operate on open Rhino 8 documents. Explicit `rhino_offline_*` tools in the Router support saved `.3dm` JavaScript edits without Rhino, or native `.dwg`/`.dxf`/`.3dm` C# jobs through an installed licensed Rhino 8 headless worker. Never fall back to offline after a live failure.
- For offline work, inspect the absolute source path with `rhino_offline_inspect_document`, preview a restricted JavaScript modeling script with `rhino_offline_preview_script`, then publish its returned one-use `previewToken` with `rhino_offline_apply_script` in the same Router session within ten minutes. Apply refuses changed sources and writes the exact preview result to a new sibling `<stem>_YYYYMMDD_HHMMSS.3dm` using local date and 24-hour time. Same-second collisions wait for another available second, with cancellation and a bounded timeout. It never overwrites the source or an existing output; every subsequent edit creates another copy without accumulating suffixes. Return the output path to the user.
- Bounded JavaScript scripts have an in-memory `model` bridge, no filesystem/process/network/CLR access and no raw file-write API. Read the tool's returned scripting help for supported operations and limits. Known open source paths are rejected by offline tools; unknown sessions' unsaved state is never represented by disk reads.
- Hard rule: never close a Rhino document or Rhino, kill Rhino, or ask the user to close a file just
  to enable offline tools. On `OFFLINE_SOURCE_OPEN`, switch to the live session. Return offline
  output at its suffixed path; never rename or move it over the user's canonical `.3dm` unless the
  user explicitly asks for that in the current conversation.
- For explicit DWG/DXF conversion or native Rhino modeling offline, call `rhino_offline_preview_rhino_script(filePath, script, ...)` with trusted synchronous C# against `doc`. An empty script converts/inspects. Set `modelUnits` when source units are ambiguous. Reuse a trusted C# class via `libraryPath` + `libraryType` when appropriate. This is full-trust code; assess the script's actual external effects. Read its result/log and apply the returned token with `rhino_offline_apply_rhino_script` in the same Router session. Always return the suffixed `.3dm` output path. Source is read-locked; apply never reruns code or overwrites a file. Native failure/timeout is an error, not permission to switch to live documents or UI automation.
- `MCP_Rhino.Router.exe` is the only supported MCP client connection. Installed clients launch
  `%LOCALAPPDATA%\MCP_Rhino\bin\MCP_Rhino.Router.exe` over stdio; each client session owns its Router
  process, and users do not start a shared Router daemon.
- Debug and Release `MCP_Rhino.Server.rhp` builds have the same startup shape. The plugin loads at
  Rhino startup and publishes a current-user route endpoint for every saved open document in that
  Rhino process. Opening or closing documents updates discovery without loading another plugin.
- For live work, call `rhino_router_list_documents`, then `rhino_router_select_document` with its opaque session id
  before using tools that do not carry an unambiguous top-level `filePath`.
- In Router mode, the endpoint is bound to one runtime serial number. A selected session plus a
  conflicting `filePath` returns `DOCUMENT_TARGET_CONFLICT`; duplicate open paths return
  `DOCUMENT_TARGET_AMBIGUOUS` until the caller selects a session id. A lost mutation connection is
  not replayed and reports `MUTATION_OUTCOME_UNKNOWN`.
- `DOCUMENT_ENDPOINT_BUSY` (diagnostic `ENDPOINT_BUSY`) means the document is open but other agent
  sessions hold every connection slot of its route. Retry after a short wait; if it persists, tell
  the user that ending idle agent sessions frees slots. It is not a reason to use offline tools,
  close the document, or report the document as closed. `DOCUMENT_ENDPOINT_UNAVAILABLE` carries the
  document's own diagnostic, such as `ENDPOINT_NOT_LISTENING` or `ROUTE_ATTESTATION_MISMATCH`.
- Fixed debug pipes, stdio bridges, embedded chat/companion surfaces, and per-document panel pipes are
  retired. Do not add a fallback transport when Router discovery or selection fails; report the live
  route capability gap instead.
- Repository-workspace and test-route sessions follow `AGENTS.md`, `Runtime_Workflow/`, and
  `Project_Guides/`.
- MCP resources, when registered, are reference-only surfaces. They may be used for static RhinoCommon/RhinoScript references, local tool help, or modeling policy text. They must not be used as a substitute for live document reads, previews, mutations, selection state, viewport state, or filesystem export.

### 1. Assess

Before execution, assess:

- task type
- current `tool / skill / agent` coverage, including supported script runners and their availability
- current reference `resource` coverage, if the task asks for static documentation or guidance
- whether an ad hoc script can reliably cover an operation without a suitable task-specific tool or skill, within the selected runner's constraints
- execution reliability

Assessment may lead to only one of two branches:

- **Execute branch**: state the chosen path and execute
- **Gap branch**: only when neither existing task-specific capabilities nor supported scripting can reliably complete the task, stop, explain the remaining gap, and wait for user confirmation before construction

Do not wait for execution failure before declaring a capability gap.

### 2. Execute

Choose one path:

- **Resource**: static reference or documentation lookup only. *Examples: read local RhinoCommon reference notes, browse generated tool help, inspect modeling policy text.* If the requested information depends on the current Rhino document, do not use a resource.
- **Tool**: clear target and inputs, with bounded execution through an atomic tool or a supported script runner. *Examples: read layers, export files, create basic geometry, read attributes or measurements, or perform an ad hoc operation with a task-specific script.* Use the applicable live or offline script sequence above; a live compile-only preview is not a geometry dry run.
- **Skill**: fixed workflow, repeated pattern, clear boundary, existing skill coverage. *Examples: filter-then-confirm, preview-then-apply.*
- **Agent**: dynamic next-step selection, branching, cross-skill coordination. *Examples: audit-then-choose-analysis-path, state-driven branching.*

## Layer Boundaries

- **Resource**: reference-only content; no live Rhino truth, no filesystem writes, no mutation, no preview-of-mutation.
- **Tool**: single executable or inspectable capability, directly exposed, minimal orchestration
- **Skill**: reusable workflow built from tools or services
- **Agent**: goal-driven routing and orchestration. Prefer Skill over Tool; never touch low-level Rhino details directly.

Do not introduce a top-level Orchestrator Agent unless cross-Skill dynamic orchestration becomes a recurring need.

## Gap Handling

Missing a dedicated tool is not itself a capability gap. First assess whether existing tools,
skills, agents, or supported scripting can reliably complete the task. Script writing alone
does not trigger construction. Unavailable live routing or script runners and operations outside
the supported execution contracts can still leave genuine gaps; do not bypass them with another
transport, implicit offline fallback, or unauthorized UI automation.

If current capabilities, including supported scripting, are insufficient, stop and report:

1. why the task cannot be completed reliably
2. what capability is missing and why supported scripting does not cover it
3. which layer it belongs to:
   - reference-only content -> `Resource`
   - atomic capability -> `Tool`
   - fixed workflow -> `Skill`
   - dynamic orchestration -> `Agent`
4. what likely needs to be added
5. whether to start construction

## Construction Trigger

Only enter construction after explicit user confirmation.

Do not create a dedicated tool for each new Rhino operation. A new tool should be explicitly
requested by the user or justified by demonstrated reuse, reliability requirements, or execution
needs beyond a task-specific script. Recurring multi-step work may be better served by a reusable
script or skill; repetition alone does not require another MCP tool. These considerations can
justify a proposal, but do not replace user confirmation to build.

When confirmed, choose the recordkeeping level in `Project_Guides/MCP_Rhino Plan Log.md`:

- Substantial capability or architecture work: PLAN before implementation, appropriate validation, and EXET after execution. Add a matching TEST folder only when new standalone validation assets are needed; otherwise link existing tests and results.
- Small fixes, documentation changes, and routine housekeeping: one concise EXET record with the reason, changes, validation, and remaining work. No separate PLAN or empty TEST folder is required.
- Routine runtime tasks and ad hoc scripts remain runtime work: use the activity log below and preserve useful evidence in the task response or actual output artifacts, without construction records.

All construction must follow all Markdown guide files under `Project_Guides/`.

## Skill Metadata

- **Carrier**: the C# registration layer. Use the `*SkillTool` class's `[Description]` attribute as the single routing source. Do not maintain a separate prompt layer or standalone index - avoids multi-source drift.
- **`*SkillTool` meaning**: server-side skills under `Skills/` must be wrapped as `*SkillTool` (or an equivalent MCP Tool) to be visible to the model; the model sees MCP tools, not raw Skills.
- **Description style**:
  - Skill Tool `[Description]` - **task-oriented**, close to user intent.
  - Atomic Tool `[Description]` - **operation-oriented**.
- **Naming**: keep the `Skill` infix in Skill Tool names to aid model classification.
- **Description coverage**: each skill description states
  - what it does
  - when to use it
  - when it should beat atomic tools
  - input assumptions
  - boundaries
  - non-applicable cases

## Tool And Resource Metadata

- Tool routing metadata is carried by method-level C# `[Description]` attributes. Do not maintain a second hand-written tool catalog for runtime routing.
- Resource routing metadata is carried by MCP resource names, URIs, and descriptions after resources are registered.
- Generated inventories are validation output only. Use them to audit coverage, duplicate method names, descriptions, safety annotations, and resource lists; do not treat them as the runtime source of truth.
- If a client cannot reliably read MCP resources, use a read-only `Tools/Reference` fallback only for the same reference-only content. Do not move live Rhino reads into resources or reference tools.
- When multiple tools appear to cover the same live-only capability, prefer the canonical structured tool with the shortest stable name. Current examples: use `FilterObjects` for layer/type/user-attribute object filtering, `FindLayerCandidates` for layer disambiguation, `GetLayers` for layer inventory, and `Tools/Blocks` preview/apply tools for block lifecycle.

## Skill Candidate Suggestion

Use `Runtime_Log/` as the basis for identifying repeated task patterns.

- Detection requires cross-session `Runtime_Log/`; single-session data is insufficient for stable identification.
- A skill creation trigger is met when either of the following is true:
  - there are at least 3 records, the overlap of used `tools` is 70% or higher, and the number of required `tools` is greater than 10
  - a single task requires more than 15 `tools`
- Unaccepted candidates are not persisted to any file.

If a skill creation trigger is met, suggest a skill candidate at the end of the response, including:

- repeated combination
- frequency or recent occurrence
- one-line scope

Do not start planning unless the user explicitly asks to begin.

## Activity Log Minimum

- root path: `Runtime_Log/`
- file name: `YYMM.json`
- format: JSONL
- fields: `ts`, `task`, `tools`
- write path: unified `AppendActivityLogTool`
- **Read**: at task start, load logs for the current month plus the previous 2 months. Example: if the current month is April, read February, March, and April.
- **Write discipline**: only `AppendActivityLogTool` writes; business tools do not write logs themselves.
- **No params / no results**: record only `ts`, `task`, `tools`.

Example:

```json
{"ts":"2026-04-23T13:36","task":"add spheres on layer X","tools":["CreatePointsTool","GetLayers"]}
```
