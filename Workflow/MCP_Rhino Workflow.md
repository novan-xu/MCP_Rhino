# MCP_Rhino Workflow

## Purpose

Define the runtime protocol for handling user tasks with existing `tool / skill / agent` capabilities, and define when to stop and ask for capability construction under `Project_Guides`.

## Scope

- `Project_Guides/`: how to build or modify capabilities
- `Workflow/`: how to use and route existing capabilities
- `log/`: cross-session tool usage history for pattern detection

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

- **Tool**: clear target, clear inputs, few calls, no complex branching
- **Skill**: fixed workflow, repeated pattern, clear boundary, existing skill coverage
- **Agent**: dynamic next-step selection, branching, cross-skill coordination

## Layer Boundaries

- **Tool**: single capability, directly exposed, minimal orchestration
- **Skill**: reusable workflow built from tools or services
- **Agent**: decision-making, routing, and orchestration; prefer skills before tools

Do not introduce a top-level Orchestrator Agent by default.

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

All construction must follow `Project_Guides`.

## Skill Metadata

Use `*SkillTool` metadata as the single routing source.

Each skill description should state:

- what it does
- when to use it
- when it should beat atomic tools
- input assumptions
- boundaries
- non-applicable cases

## Skill Candidate Suggestion

Use `log/` as the basis for identifying repeated task patterns.

If a repeated pattern is detected, suggest a skill candidate at the end of the response, including:

- repeated combination
- frequency or recent occurrence
- one-line scope

Do not start planning unless the user explicitly asks to begin.

## Activity Log Minimum

- root path: `log/`
- file name: `YYMM.json`
- format: JSONL
- fields: `ts`, `task`, `tools`
- write path: unified `AppendActivityLogTool`

Example:

```json
{"ts":"2026-04-23T13:36","task":"add spheres on layer X","tools":["CreatePointsTool","GetLayersInLiveTool"]}
```
