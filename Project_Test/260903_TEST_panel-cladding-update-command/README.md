# Panel Cladding Update Command Smoke

Validates deterministic create/update/delete reconciliation, duplicate expected-CID rejection,
the `PCUpdate` batch command contract, managed-root/PID/CID mutation boundaries, one Undo record, and
package registration.

The 2026-10-06 follow-up also covers normalized parent/child source CIDs, shared
PIDs, mixed and all-duplicate selections, cross-PID CID conflicts, casing and
whitespace, missing CIDs, skipped-panel dependency ownership, and ambiguous
legacy unsuffixed ownership. Live source contracts check normalization order,
processable-only preparation, source-panel exclusion, and duplicate selection.
These are pure planning and contract checks, not a native Rhino integration run.
Role fixtures now use `CW_1.06_UNIT_TYPE`; both legacy flags are deliberately set
to verify they are ignored. An explicit flat panel clears its stale corner CID
before duplicate classification.

Run from the repository root:

```powershell
dotnet run --project .\Project_Test\260903_TEST_panel-cladding-update-command\PanelCladdingUpdateCommandSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260903_TEST_panel-cladding-update-command\PanelCladdingUpdateCommandSmoke.csproj -c Release
```
