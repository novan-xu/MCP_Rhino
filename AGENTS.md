# MCP_Rhino Agents Entry

Before taking any action in this repository, read this file first.
Do not skip it.

This file is the required entry point for agents working in this repository.
Do not treat it as the full rule body. Use it to decide which document set to follow next.

## Project Intent

This repository organizes work around three layers:

- existing runtime capabilities: `tool / skill / agent`, plus reference-only MCP `resource` content when present
- capability construction rules: `Project_Guides/`
- runtime task routing rules: `Runtime_Workflow/`

Your first job is to decide whether the user is asking to:

1. use existing Rhino capabilities to complete a task, or
2. construct or modify repository capabilities

## Step 1: Classify the Request

### A. Runtime Task

Treat the request as a runtime task when the user wants the model to use existing Rhino-facing capabilities to:

- inspect, query, filter, analyze, export, or modify Rhino data
- read reference-only MCP resources that do not inspect or mutate Rhino data
- complete a task with current tools
- choose among existing `tool / skill / agent / resource` paths

If the request is a runtime task, immediately follow:

- `Runtime_Workflow/MCP_Rhino Workflow.md`

### B. Capability Construction Task

Treat the request as capability construction when the user wants to:

- add a new tool, skill, or agent
- extend or refactor an existing capability
- change architecture, registration, prompts, contracts, tests, or workflow infrastructure
- build a missing capability after a confirmed gap

If the request is capability construction, immediately follow:

1. `Project_Guides/MCP_Rhino Architecture.md`
2. `Project_Guides/MCP_Rhino Plan Log.md`

## Step 2: Runtime vs Construction Decision Rule

Use this default rule:

- if the task can be completed reliably with existing capabilities, it is a runtime task
- if the task cannot be completed reliably with existing capabilities, report the gap first
- only after explicit user confirmation to build, switch to capability construction

Do not enter construction mode automatically just because the task is hard.

## Step 3: Required Routing Behavior

### When in Runtime Mode

Follow `Runtime_Workflow/MCP_Rhino Workflow.md` and:

- assess before execution
- choose `Tool`, `Skill`, `Agent`, or reference `Resource` where the runtime workflow allows it
- prefer `Skill` over an equivalent atomic tool chain
- stop and report capability gaps instead of forcing execution

### When in Construction Mode

Follow `Project_Guides/` and:

- respect architecture ownership and layer boundaries
- follow naming and execution-mode constraints
- use the required `PLAN -> EXET -> TEST` chain

## Project Cleanup And Archive Rule

- Keep active construction artifacts in `Project_Plan/`, `Project_Exet/`, and `Project_Test/`.
- When a project plan has been successfully executed and more than three days have elapsed since the EXET execution date, the matching PLAN, EXET, and TEST artifacts may be archived under `Project_Archive/Project_Plan/`, `Project_Archive/Project_Exet/`, and `Project_Archive/Project_Test/`.
- Archive all three matching artifacts together and preserve their original names.
- Do not archive incomplete, failed, current, unmatched, or still-active work.
- Follow `Project_Guides/MCP_Rhino Plan Log.md` for the detailed archive contract.

## Hard Constraints

- Do not maintain a second conflicting rule set inside this file.
- Do not start `PLAN` work without explicit user confirmation.
- Do not bypass `Project_Guides` when constructing new capabilities.
- Do not disguise a capability gap as an executable runtime task.
- Do not use Computer Use or any other Windows UI automation unless the user explicitly requests Computer Use or Windows UI automation in the current request. Mentioning an application or file, or asking to "open" or "go to" one, does not by itself authorize UI automation.

## Document Priority

Use documents in this order:

1. `AGENTS.md`
2. `Runtime_Workflow/MCP_Rhino Workflow.md` for runtime use of existing capabilities
3. `Project_Guides/MCP_Rhino Architecture.md` for construction architecture rules
4. `Project_Guides/MCP_Rhino Plan Log.md` for construction artifact rules

If a request changes from runtime execution to capability construction during the same conversation, switch document sets immediately and state that switch clearly.
