# MCP_Rhino Workflow

## Purpose

Define the runtime protocol for handling user tasks with existing `tool / skill / agent` capabilities, and define when to stop and ask for capability construction under `Project_Guides`.

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

## Runtime Steps

### Current MCP Runtime Shape

- Rhino-facing runtime capabilities are Live Only. They operate on the currently running Rhino 8 document through the loaded `MCP_Rhino.Server.rhp` plugin; do not fall back to direct `.3dm` disk reads.
- `MCP_Rhino.Router.exe` is the only supported MCP client connection. Installed clients launch
  `%LOCALAPPDATA%\MCP_Rhino\bin\MCP_Rhino.Router.exe` over stdio; each client session owns its Router
  process, and users do not start a shared Router daemon.
- Debug and Release `MCP_Rhino.Server.rhp` builds have the same startup shape. The plugin loads at
  Rhino startup and publishes a current-user route endpoint for every saved open document in that
  Rhino process. Opening or closing documents updates discovery without loading another plugin.
- Call `rhino_router_list_documents`, then `rhino_router_select_document` with its opaque session id
  before using tools that do not carry an unambiguous top-level `filePath`.
- In Router mode, the endpoint is bound to one runtime serial number. A selected session plus a
  conflicting `filePath` returns `DOCUMENT_TARGET_CONFLICT`; duplicate open paths return
  `DOCUMENT_TARGET_AMBIGUOUS` until the caller selects a session id. A lost mutation connection is
  not replayed and reports `MUTATION_OUTCOME_UNKNOWN`.
- Fixed debug pipes, stdio bridges, embedded chat/companion surfaces, and per-document panel pipes are
  retired. Do not add a fallback transport when Router discovery or selection fails; report the live
  route capability gap instead.
- Repository-workspace and test-route sessions follow `AGENTS.md`, `Runtime_Workflow/`, and
  `Project_Guides/`.
- MCP resources, when registered, are reference-only surfaces. They may be used for static RhinoCommon/RhinoScript references, local tool help, or modeling policy text. They must not be used as a substitute for live document reads, previews, mutations, selection state, viewport state, or filesystem export.

### 1. Assess

Before execution, assess:

- task type
- current `tool / skill / agent` coverage
- current reference `resource` coverage, if the task asks for static documentation or guidance
- execution reliability

Assessment may lead to only one of two branches:

- **Execute branch**: state the chosen path and execute
- **Gap branch**: stop, explain the gap, and wait for user confirmation

Do not wait for execution failure before declaring a capability gap.

### 2. Execute

Choose one path:

- **Resource**: static reference or documentation lookup only. *Examples: read local RhinoCommon reference notes, browse generated tool help, inspect modeling policy text.* If the requested information depends on the current Rhino document, do not use a resource.
- **Tool**: clear target, clear inputs, few calls, no complex branching. *Examples: read layers, export files, create basic geometry, read attributes or measurements.*
- **Skill**: fixed workflow, repeated pattern, clear boundary, existing skill coverage. *Examples: filter-then-confirm, preview-then-apply.*
- **Agent**: dynamic next-step selection, branching, cross-skill coordination. *Examples: audit-then-choose-analysis-path, state-driven branching.*

## Layer Boundaries

- **Resource**: reference-only content; no live Rhino truth, no filesystem writes, no mutation, no preview-of-mutation.
- **Tool**: single executable or inspectable capability, directly exposed, minimal orchestration
- **Skill**: reusable workflow built from tools or services
- **Agent**: goal-driven routing and orchestration. Prefer Skill over Tool; never touch low-level Rhino details directly.

Do not introduce a top-level Orchestrator Agent unless cross-Skill dynamic orchestration becomes a recurring need.

## Gap Handling

If current capabilities are insufficient, stop and report:

1. why the task cannot be completed reliably
2. what capability is missing
3. which layer it belongs to:
   - reference-only content -> `Resource`
   - atomic capability -> `Tool`
   - fixed workflow -> `Skill`
   - dynamic orchestration -> `Agent`
4. what likely needs to be added
5. whether to start construction

## Construction Trigger

Only enter construction after explicit user confirmation.

When confirmed, follow:

1. `Project_Plan/YYMMDD_PLAN_<capability-name>.md`
2. implementation
3. `Project_Test/YYMMDD_TEST_<capability-name>/`
4. `Project_Exet/YYMMDD_EXET_<capability-name>.md`

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
