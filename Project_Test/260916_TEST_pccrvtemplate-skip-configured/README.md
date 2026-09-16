# PCCrvTemplate configured-panel skip regression

Validated on 2026-09-16. Regression code extends the existing curve-template and sparse-topology
smokes so their previous rejection expectations cannot conflict with the new behavior.

## Commands and results

Run from the repository root. Every final command below exited 0:

```powershell
dotnet run --project Project_Test/260820_TEST_panel-cladding-curve-template-ui/PanelCladdingCurveTemplateUiSmoke.csproj -c Debug -- --curve-template-only
dotnet run --project Project_Test/260820_TEST_panel-cladding-curve-template-ui/PanelCladdingCurveTemplateUiSmoke.csproj -c Release -- --curve-template-only
dotnet run --project Project_Test/260820_TEST_panel-cladding-sparse-topology/PanelCladdingSparseTopologySmoke.csproj -c Debug
dotnet run --project Project_Test/260820_TEST_panel-cladding-sparse-topology/PanelCladdingSparseTopologySmoke.csproj -c Release
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Debug --no-restore
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Release --no-restore
git diff --check
```

Both standalone RHP builds reported zero warnings and zero errors. Narrow builds are sufficient:
this change does not modify the MCP server, transport, tool surface, or plugin-host lifecycle.

The initial regression run against the old planner exited 1 with
`PCCrvTemplate did not skip a panel with an existing merge code.` The updated planner passes.
The sparse-topology smoke initially failed compilation because an older fixture omitted the
now-required spawn-planner layer path. Passing the fixture's existing `layout.LayerFullPath`
fixed that test setup; both configurations then passed.

## Coverage

- Mixed configured/unconfigured selections in both H and V priorities.
- Configured panel first, middle, or last; duplicate configured and eligible selections.
- Eligible panels receive the same masks as their isolated baseline plans.
- All-configured selections succeed with zero mutation plans and distinct skipped IDs.
- No-run eligible panels stay mask-free, request no signature deletions, and are not configured skips.
- Configured topology remains unchanged during planning.
- Empty/empty-ID selection, invalid priority, and invalid eligible track counts still fail.
- Existing H/V topology round trips and missing/hidden segment preservation.
- Existing sparse mask persistence, case-insensitive merge-code eligibility, and blank mask behavior.
- Existing command reflection/source checks for name, selection order, Undo, and rollback structure.

The `--curve-template-only` option skips unrelated WPF window/layout checks and creates no UI.
Undo/rollback checks are source-contract checks, not live Rhino execution.

## Live acceptance, not executed

After loading a build in Rhino, select configured panel A and unconfigured panels B/C, then run
`PCCrvTemplate` with each priority. Verify A's complete user text is unchanged, B/C receive the
chosen masks, and the report counts two updates and one skip. Undo should restore B/C together.
Repeat with A alone: success, zero updates, one skip, unchanged document attributes. Repeat with
an eligible one-row/one-column no-run panel: it stays mask-free without being counted as configured.

No production installation, registry mutation, or Rhino UI automation was performed.
