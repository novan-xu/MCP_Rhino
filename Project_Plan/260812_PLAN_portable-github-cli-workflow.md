# Portable GitHub CLI Workflow

## Background

The repository is used on laptop `NYCZBF16G9NXU`, where the user does not have administrator
access. GitHub CLI is intentionally available as a portable binary under the user's local app-data
directory rather than as a system installation. Treating a missing `gh` entry on `PATH` as an
installation blocker prevents otherwise valid commit, push, pull-request, and merge workflows.

## Goal

Record a durable, laptop-specific repository rule that directs agents to use the portable GitHub
CLI and the authenticated GitHub connector without requesting an administrator-installed CLI.

## Architecture Ownership

- `AGENTS.md`: repository-entry workflow and host-specific execution rule.
- `Project_Test/260812_TEST_portable-github-cli-workflow/`: read-only verification record.

## Key Design

1. Scope the exception to host `NYCZBF16G9NXU`.
2. Resolve `gh.exe` from `%LOCALAPPDATA%\CodexTools\gh\<version>\bin\gh.exe`, preferring the
   highest available version and using the currently validated `2.96.0` binary when present.
3. Do not require an administrator installation or require portable `gh.exe` to be on `PATH`.
4. Continue to use local `git` for staging, committing, and pushing.
5. Prefer the authenticated GitHub connector for pull-request and merge mutations. If a CLI-only
   operation requires authentication, run `auth login` through the resolved portable binary rather
   than asking for installation.

## Files Involved

- `AGENTS.md`
- `Project_Plan/260812_PLAN_portable-github-cli-workflow.md`
- `Project_Test/260812_TEST_portable-github-cli-workflow/README.md`
- `Project_Exet/260812_EXET_portable-github-cli-workflow.md`

## Usage

On the named laptop, resolve the portable executable and invoke it by absolute path. Use local Git
and the installed GitHub connector to complete the normal publishing workflow.

## Acceptance Criteria

- `AGENTS.md` identifies the laptop and portable CLI discovery location.
- The rule explicitly forbids treating missing system installation or missing `PATH` registration
  as a blocker on that laptop.
- The resolved portable binary reports a valid version.
- Repository fetch succeeds and the GitHub connector can access `novan-xu/MCP_Rhino`.
- The current worktree can be committed, pushed through a topic branch, and merged into `master`.

## Risks And Rollback

- A hard-coded version can become stale, so discovery uses the versioned parent directory and
  prefers the highest installed version.
- The host-specific rule must not be generalized to machines that do not have the portable binary.
- Rollback is a revert of the new `AGENTS.md` rule and its matching PLAN/TEST/EXET artifacts.

## Future Extensions

- Add a repository-owned read-only helper that resolves portable developer tools consistently if
  more host-specific portable dependencies are introduced.
