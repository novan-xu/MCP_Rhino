# Portable GitHub CLI Workflow Execution

## Corresponding Plan

- Plan: `Project_Plan/260812_PLAN_portable-github-cli-workflow.md`
- Execution date: 2026-08-12

## Related Artifacts

- Test folder: `Project_Test/260812_TEST_portable-github-cli-workflow/`
- Feature commit: `6df368b` (`Add panel cladding region and offset synchronization`)
- Publication branch: `codex/panel-cladding-regions-offset-sync`
- Pull request: none; the installed GitHub connector returned HTTP 403 for PR creation, so the
  authenticated Git remote is used for the explicitly requested direct merge and push.

## Execution Result / Actual Scope

- Added a host-specific `AGENTS.md` rule for laptop `NYCZBF16G9NXU`.
- Recorded portable GitHub CLI discovery under
  `%LOCALAPPDATA%\CodexTools\gh\<version>\bin\gh.exe` and the currently validated 2.96.0 path.
- Explicitly prohibited treating a missing system installation or absent `PATH` entry as a blocker
  on this laptop.
- Retained local Git as the branch, commit, merge, and push implementation, with the authenticated
  GitHub connector preferred for PR mutations when its installation permissions allow them.
- Documented portable `auth login` as the fallback for CLI-only authenticated operations; no admin
  installation is required.

## Deviation From Plan

- Read-only repository access through the GitHub connector succeeded and reported admin/push
  repository permissions, but PR creation returned HTTP 403 `Resource not accessible by
  integration`. The requested publication therefore uses a direct local Git merge followed by an
  authenticated push to `origin/master`.
- Portable `gh auth status` reported no saved GitHub host login. No existing credential was
  extracted or repurposed to configure it.

## Problems Found And Fixed During Construction

- `gh` was absent from `PATH`, which previously caused the workflow to stop and request an install
  that the user cannot perform. The new entry rule resolves the existing portable binary directly.
- A user-visible temporary portable extraction also exists, but the stable `CodexTools` copy is now
  the preferred source.

## Test Record

The following read-only checks completed successfully from the repository root:

```powershell
$portableGh = Get-ChildItem -LiteralPath "$env:LOCALAPPDATA\CodexTools\gh" -Filter gh.exe -File -Recurse |
  Sort-Object { [version]$_.Directory.Parent.Name } -Descending |
  Select-Object -First 1 -ExpandProperty FullName

& $portableGh --version
git fetch origin --prune
git rev-list --left-right --count origin/master...master
```

Observed results before publication:

```text
portableGh=C:\Users\nxu\AppData\Local\CodexTools\gh\2.96.0\bin\gh.exe
gh version 2.96.0 (2026-07-02)
origin/master...master = 0 0
```

`git push -u origin codex/panel-cladding-regions-offset-sync` completed with exit code 0.

The encompassing panel-cladding changes were also revalidated in this publication run:

- focused offset-sync smoke in Debug and Release: passed, with the documented native Rhino skip
- focused region smoke in Debug and Release: passed, with the documented native Rhino skip
- serial Debug solution build: 0 warnings, 0 errors
- serial Release solution build: 0 warnings, 0 errors

## Acceptance Alignment

- Laptop and portable discovery path recorded in `AGENTS.md`: passed.
- Missing system installation and missing `PATH` entry are no longer blockers: passed.
- Portable CLI version check: passed with 2.96.0.
- Git fetch and topic-branch push: passed.
- Connector repository access: passed for read; PR creation permission is unavailable and recorded.
- Direct merge and `origin/master` push: selected as the supported authenticated publication path.

## Rollback Verification

- Reverting the workflow commit removes the host-specific rule and matching PLAN/TEST/EXET
  artifacts.
- The rule performs no credential migration and installs no software, so rollback does not require
  machine-level changes.

## Current Remaining Items

- No workflow implementation work remains. Publication is completed by the enclosing direct Git
  merge and push requested by the user.

## Conclusion

The repository now remembers how to use the existing portable GitHub CLI on this laptop and no
longer asks for an administrator-installed copy. The GitHub connector permission limitation is
documented, with authenticated direct Git merge/push used to complete publication.
