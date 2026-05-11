# 260506_PLAN_project-folder-cleanup

## Background

The project has recently shifted to a live Rhino plugin, named-pipe bridge, and standalone companion workflow. The root planning, execution, and test folders now contain both active and completed historical capability artifacts, while `Project_Archive/` still stores some archived plans directly at its root.

## Goals

- Add explicit archive buckets for completed Plan, EXET, and TEST artifacts.
- Add a cleanup/archive rule to `AGENTS.md`.
- Update `Project_Guides/` and `Runtime_Workflow/` so they match the current live-only plugin, bridge, companion, and panel-bound MCP setup.
- Move completed artifacts older than the retention window into `Project_Archive/` without moving active or unexecuted work.

## Architecture Ownership

- Repository routing rule: `AGENTS.md`
- Construction workflow and artifact policy: `Project_Guides/MCP_Rhino Plan Log.md`
- Runtime routing and MCP usage workflow: `Runtime_Workflow/MCP_Rhino Workflow.md`
- Archive storage: `Project_Archive/`

## Key Design

- Archive folders mirror the active artifact roots:
  - `Project_Archive/Project_Plan/`
  - `Project_Archive/Project_Exet/`
  - `Project_Archive/Project_Test/`
- A capability is archive-eligible only when matching PLAN, EXET, and TEST artifacts exist and the EXET execution date is more than three days old.
- Current or incomplete work stays in `Project_Plan/`, `Project_Exet/`, and `Project_Test/`.
- Documentation updates should describe the current setup without introducing a conflicting second rule set.

## Involved Files

- `AGENTS.md`
- `Project_Guides/MCP_Rhino Architecture.md`
- `Project_Guides/MCP_Rhino Plan Log.md`
- `Runtime_Workflow/MCP_Rhino Workflow.md`
- `Project_Archive/`
- `Project_Plan/`, `Project_Exet/`, `Project_Test/`

## Usage

Agents should keep active work in the root artifact folders. After a successful execution is older than three days, agents may move the matching PLAN, EXET, and TEST artifacts into the corresponding archive buckets.

## Acceptance Criteria

- `Project_Archive/Project_Plan/`, `Project_Archive/Project_Exet/`, and `Project_Archive/Project_Test/` exist.
- `AGENTS.md` contains a cleanup rule that allows archiving successfully executed work after more than three days.
- The architecture and runtime workflow docs describe the current live-only, plugin-hosted, bridge/companion MCP setup.
- Completed old triples are moved into the archive buckets.
- Recent and incomplete work remains active.

## Risks And Rollback

- Risk: moving an incomplete or still-active artifact would hide active work.
- Mitigation: move only matched PLAN/EXET/TEST triples with execution dates older than the retention rule.
- Rollback: move the affected artifacts back to their original root folders.

## Future Extensions

- Add a script to list archive-eligible triples before moving them.
- Add an archive index if the archive grows large enough to need search metadata.
