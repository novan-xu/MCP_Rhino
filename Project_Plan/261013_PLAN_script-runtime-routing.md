# Script Runtime Routing Plan

## Background

The runtime workflow supports ad hoc scripts, but the entry point and gap-handling rules do not explicitly include scripting in capability assessment. The user approved clarifying that a missing dedicated tool need not trigger construction.

## Goal

Treat task-specific scripts through existing supported runners as runtime work; reserve gap reporting for tasks that neither existing task-specific capabilities nor supported scripting can reliably complete.

## Architecture ownership

Repository routing policy: `AGENTS.md` is the entry point; `Runtime_Workflow/MCP_Rhino Workflow.md` owns execution details. No runtime implementation changes.

## Key design

- Prefer suitable existing tools/skills, then assess supported script execution.
- Writing and running a task-specific script does not itself require PLAN work or capability construction.
- Preserve runner constraints, document targeting, live/offline separation, and explicit construction authorization.
- Recommend new tools for demonstrated reuse, reliability, or execution needs; do not require them for each new operation.
- Leave the PLAN/EXET/TEST policy unchanged pending the user's decision on recordkeeping.

## Files

- `AGENTS.md`
- `Runtime_Workflow/MCP_Rhino Workflow.md`
- Matching EXET report and TEST folder for `261013_script-runtime-routing`.

## Usage

Agents assess both task-specific capabilities and supported scripting before choosing execution or reporting a gap.

## Acceptance criteria

- Both routing documents classify ad hoc supported scripting as runtime work.
- Missing dedicated tools alone do not trigger construction.
- Genuine execution limits and user authorization remain explicit.
- Review routing scenarios and run `git diff --check`; no Rhino or build validation is required for prose-only changes.
- Preserve the pre-existing `.gitignore` modification.

## Risks and rollback

Avoid implying that scripts bypass execution constraints or that installed script tools are always available. Roll back only this change's policy edits and matching records; preserve unrelated edits.

## Future extensions

The user is considering lighter traceability records. That policy change is outside this approved routing edit.
