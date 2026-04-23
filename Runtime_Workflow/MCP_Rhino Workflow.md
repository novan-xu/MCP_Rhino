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
2. Prefer `Tool` for atomic tasks.
3. Prefer `Skill` for fixed, repeatable multi-step workflows.
4. Prefer `Agent` for goal-driven tasks with branching, sequencing, or cross-skill orchestration.
5. If an atomic tool chain and a skill tool can both complete the task, prefer the skill tool.

## Runtime Steps

### 1. Assess

Before execution, assess:

- task type
- current `tool / skill / agent` coverage
- execution reliability

Assessment may lead to only one of two branches:

- **Execute branch**: state the chosen path and execute
- **Gap branch**: stop, explain the gap, and wait for user confirmation

Do not wait for execution failure before declaring a capability gap.

### 2. Execute

Choose one path:

- **Tool**: clear target, clear inputs, few calls, no complex branching. *Examples: read layers, export files, create basic geometry, read attributes or measurements.*
- **Skill**: fixed workflow, repeated pattern, clear boundary, existing skill coverage. *Examples: filter-then-confirm, preview-then-apply.*
- **Agent**: dynamic next-step selection, branching, cross-skill coordination. *Examples: audit-then-choose-analysis-path, state-driven branching.*

## Layer Boundaries

- **Tool**: single capability, directly exposed, minimal orchestration
- **Skill**: reusable workflow built from tools or services
- **Agent**: goal-driven routing and orchestration. Prefer Skill over Tool; never touch low-level Rhino details directly.

Do not introduce a top-level Orchestrator Agent unless cross-Skill dynamic orchestration becomes a recurring need.

## Gap Handling

If current capabilities are insufficient, stop and report:

1. why the task cannot be completed reliably
2. what capability is missing
3. which layer it belongs to:
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
{"ts":"2026-04-23T13:36","task":"add spheres on layer X","tools":["CreatePointsTool","GetLayersInLiveTool"]}
```
