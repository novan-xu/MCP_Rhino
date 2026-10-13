# Portable GitHub CLI Workflow Test

This folder records the read-only verification for the laptop-specific GitHub publishing rule.

Run on host `NYCZBF16G9NXU` from the repository root:

```powershell
$portableGh = Get-ChildItem -LiteralPath "$env:LOCALAPPDATA\CodexTools\gh" -Filter gh.exe -File -Recurse |
  Sort-Object { [version]$_.Directory.Parent.Name } -Descending |
  Select-Object -First 1 -ExpandProperty FullName

& $portableGh --version
git fetch origin --prune
git rev-list --left-right --count origin/master...master
```

Expected results at implementation time:

- resolved executable:
  `C:\Users\nxu\AppData\Local\CodexTools\gh\2.96.0\bin\gh.exe`
- CLI version: `2.96.0`
- fetch exit code: `0`
- pre-publication `origin/master...master`: `0 0`

The publishing workflow additionally verifies topic-branch push, pull-request creation through the
authenticated GitHub connector, merge into `master`, and final local/remote synchronization.
