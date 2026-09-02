# Panel cladding full-track collapse execution

## Corresponding plan

- Plan: `Project_Plan/260819_PLAN_panel-cladding-full-track-collapse.md`
- Execution date: 2026-08-19

## Related artifacts

- Tests: `Project_Test/260819_TEST_panel-cladding-full-track-collapse/`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.45/`
- Commit / PR: none created

## Execution result / actual scope

- Added iterative topology normalization for fully missing H and V tracks.
- Complete tracks now remove their offset, merge the adjacent dimension entries, renumber later cells, remap parent labels, and rebuild masks/merge runs.
- PCEditor applies normalization immediately after deletion and when loading panels saved by earlier versions with a complete-track mask.
- Save repeats normalization as a fail-safe, writes the reduced grid, deletes obsolete offset/cell keys, refreshes type/signature/masks, and writes unit width/height/dimension attributes.
- PCSpawnSrf now expands logical assignments over every hidden physical member before region planning, preventing missing pieces in spawned surfaces.
- Bumped PanelCladdingEditor from `1.0.44` to `1.0.45`.

## Deviations from plan

- No functional deviation.
- The first Router query transiently exposed the BKT Rhino session as an unroutable unsaved Untitled document. A later query attested the same session id and runtime serial as the saved BKT path, after which live inspection and repair proceeded normally.

## Problems found and fixed during construction

- The previous logical-cell cleanup collapsed only persisted keys. It intentionally retained the physical H/V lattice, which was insufficient when all segments on a track were absent.
- Spawn planning resolved regions directly from parsed physical cell values. Hidden cells have no persisted keys after logical cleanup, so they remained blank and produced missing surface pieces. Logical expansion now supplies representative/parent values for those physical pieces.
- The historical cell-topology test used a one-column deletion as a partial-boundary example. Under the corrected rule that is a complete track, so the fixture was changed to a true partial deletion in a two-column grid.

## Test record

Focused Debug and Release smoke:

```powershell
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-full-track-collapse\PanelCladdingFullTrackCollapseSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-full-track-collapse\PanelCladdingFullTrackCollapseSmoke.csproj -c Release
```

Result: exit 0. Assertions cover complete H/V collapse, opposite-axis coordinate remap, cell/parent renumbering, Save deletes/writes, unit dimensions, logical spawn expansion, immediate editor state, Undo, and legacy-mask normalization on load.

Regression smokes:

```powershell
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-logical-cell-cleanup\PanelCladdingLogicalCellCleanupSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-topology-persistence\PanelCladdingTopologyPersistenceSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-cell-topology\PanelCladdingCellTopologySmoke.csproj -c Debug
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-spawn\PanelCladdingSpawnSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_pcmatch-parent-fidelity\PanelCladdingMatchParentFidelitySmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-split-spawn-sync\PanelCladdingSplitSpawnSyncSmoke.csproj -c Debug
```

Result: all exit 0.

Solution validation:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug -m:1
dotnet build .\MCP_Rhino.sln -c Release -m:1
```

Result: both exit 0 with 0 warnings and 0 errors.

Package and identity:

```powershell
powershell -ExecutionPolicy Bypass -File .\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1 -Version 1.0.45
powershell -ExecutionPolicy Bypass -File .\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Debug
```

The packaged Release RHP was also probed directly. Debug and packaged Release both declare `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35` and match the manifest.

## Acceptance alignment

- Full H/V track removal: passed.
- Cell and parent renumbering: passed.
- Immediate dimensions/locks update and Undo: passed.
- Save grid cleanup and unit dimensions: passed.
- Partial-mask retention: passed.
- Complete logical surface-region coverage: passed at planning level.
- Live BKT panel-attribute validation: passed; regenerated spawned-surface geometry remains pending because no associated surfaces currently exist.

Live BKT verification subsequently passed for the panel attributes. The PID filter found exactly one panel Brep and no associated spawned surfaces. The panel was repaired in one Undo record and re-read as a normalized 2x3 layout with H0/H1 `97.875/182.75`, cells `0A-1C`, current masks/type/signature, and populated unit dimensions.

## Rollback verification

- Remove `PanelCladdingTopologyNormalizationService` and its UI/Save call sites to restore mask-only behavior.
- Remove logical expansion from spawn planning to restore physical-only region resolution.
- The live BKT repair was performed in one Rhino Undo record and can be reverted with Undo if needed.

## Current remaining items

- `1.0.45` is installed at `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.45\PanelCladdingEditor.rhp`.
- Installer validation passed, the installed RHP exists, and Rhino 8's current-user plug-in registry entry points to that exact path.
- No associated spawned surfaces currently exist for `PID_BKT_N1_PF_07`; rerun `PCSpawnSrf` after installing `1.0.45` to verify regenerated geometry visually.

## Conclusion

The editor, Save pipeline, and surface-spawn planning now share the corrected full-track and logical-cell semantics. Automated validation, live panel repair, packaging, installation, and registry validation are complete. Regenerated-surface visual verification can be performed by reopening BKT and rerunning `PCSpawnSrf` for `N1_PF_07`.
