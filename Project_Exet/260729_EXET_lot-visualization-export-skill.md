# Lot Visualization Export Skill Execution

## Corresponding Plan

- Plan: `Project_Plan/260729_PLAN_lot-visualization-export-skill.md`
- Execution date: 2026-07-29

## Associated Artifacts

- Test folder: `Project_Test/260729_TEST_lot-visualization-export-skill/`
- Smoke source: `Project_Test/260729_TEST_lot-visualization-export-skill/DeveloperCommandHandler.LotVisualizationExportSkillSmokeTest.cs`
- Live fixture: `Project_Test/260729_TEST_lot-visualization-export-skill/lot-visualization-smoke.3dm`
- Rhino smoke command: `_McpLotVisualizationExportSkillSmoke`
- CLI slug: `lot-visualization-export-skill-smoke-test`
- Commit / PR: none created.

## Execution Result / Actual Scope

- Added `LotVisualizationExportSkill` under `Skills/Drawing` with read-only preview and exporting execution methods.
- Added thin MCP tools `PreviewLotVisualizationExport` and `ExportLotVisualization` under `Tools/Drawing`.
- Added live lot resolution, deterministic lot color assignment, skill-owned group creation/reuse, ineffective-lot white assignment, and idempotent removal of prior `MCP_Lot_` membership from targeted geometry.
- Added conservative lot-key auto-discovery plus explicit key ordering and placeholder-value handling.
- Added `DrawingViewPreset.Isometric4` for `MCP_Iso_NE`, `MCP_Iso_NW`, `MCP_Iso_SE`, and `MCP_Iso_SW`; drawing-view setup now accepts explicit target object ids for exact fitted view construction.
- Extended print-scale PNG export with optional `SolidBackgroundColor` and a `BackgroundRestored` response flag.
- Implemented background staging only through document `RenderSettings.BackgroundStyle`, `BackgroundColorTop`, and `BackgroundColorBottom`. The prior values are captured and restored in a guaranteed cleanup path.
- Removed all production use of `AppearanceSettings.ViewportBackgroundColor` from both print-scale and staged drawing-export background operations.
- Defaulted the skill to four 2400 x 2400 PNGs, 300 DPI, 6 mm margins, a common `FitAll` denominator with multiplier `1.08`, solid white export background, and output beside the `.3dm`.
- Registered the live adapter, application service, skill, smoke hook, Rhino smoke command, and MCP safety expectations.

## Deviations From Plan

- The repository defines runtime skills as registered C# classes in `src/MCP_Rhino.Server/Skills`; therefore no standalone personal `SKILL.md` package was initialized. The skill-creation guidance informed the concise trigger surface, deterministic workflow, preview/apply split, and reusable fixture, while repository architecture controlled the runtime packaging format.
- An automated isolated Rhino launch was attempted with the test fixture, but Rhino stopped at plug-in load protection. Per the user's no-computer-control instruction, no UI interaction was used. The isolated test process was terminated and the user's pre-existing Rhino process remained untouched.

## Problems Found And Fixed During Construction

1. The existing drawing background operator changed `AppearanceSettings.ViewportBackgroundColor`, which is application-global and cannot be safely scoped to an export. It now changes only document render background state and restores the captured render style/top/bottom values.
2. A named-view fit based only on layers could include unrelated geometry when explicit object ids were supplied. The drawing view manager now accepts target object ids and constrains its bounding box to those visible objects.
3. The print exporter previously had no explicit solid-background transaction or restoration status. It now stages a document-only solid background, restores in `finally`, fails with `PRINT_SCALE_BACKGROUND_RESTORE_FAILED` if restoration cannot complete, and reports `BackgroundRestored` on success.
4. Group names derived only from sanitized lot text could collide. Skill-owned group names now combine a readable sanitized lot value with a deterministic FNV-1a suffix.

## Test Record

### Final builds

- `dotnet build .\MCP_Rhino.sln -c Debug --no-restore`
  - Exit `0`; 0 warnings; 0 errors.
- `dotnet build .\MCP_Rhino.sln -c Release --no-restore -p:BaseOutputPath=C:\Users\nxu\AppData\Local\Temp\MCP_Rhino_LotSkill_Final2\`
  - Exit `0`; 0 warnings; 0 errors.

### Final Debug smokes

- `lot-visualization-export-skill-smoke-test`
  - Source background contract passed.
  - 32 deterministic distinct lot colors, deterministic group naming, and conservative lot-key detection passed.
  - CLI fallback returned `LIVE_RHINO_REQUIRED` and wrote no PNGs.
- `mcp-tool-safety-annotations-smoke-test`
  - Exit `0`; 159 tools verified; no bare method-level attributes.

### Final isolated Release smokes

- `lot-visualization-export-skill-smoke-test`
  - Same source, deterministic-policy, and CLI fallback assertions passed.
- `mcp-tool-safety-annotations-smoke-test`
  - Exit `0`; 159 tools verified; no bare method-level attributes.

### Live validation status

- The smoke source includes a live branch that reads application and document background values, performs a temporary white print-scale capture, asserts application appearance is unchanged, asserts exact document render restoration, and deletes the temporary PNG.
- The dedicated fixture contains three visible Breps with two effective lots and one `TBD` lot.
- The live branch was not completed in this execution because Rhino's plug-in load-protection prompt blocked the isolated unattended launch. No user model or user preference was changed by the attempt.

## Acceptance Alignment

- Read-only deterministic preview: met and CLI-validated.
- Effective lots grouped and distinctly colored; ineffective lots white: implemented with deterministic policy tests; live operator smoke remains available.
- Four isometric named views: implemented through `Isometric4` and compiled in Debug/Release.
- One exact print scale for four PNGs: composed through the previously live-verified `ExportPrintScaleImages` `FitAll` path.
- Output beside the `.3dm`: implemented as the default output plan.
- Solid white export background without application-setting mutation: met by production source contract and compilation; live assertion is present but was not executed due load protection.
- Guaranteed restoration path on success/failure: implemented with explicit restore failure reporting and source-contract smoke.
- Debug/Release and MCP safety validation: met.
- No Computer Use: met.

## Rollback Verification

- Removing the new lot contracts, service, adapter, skill, tool, registrations, smoke artifacts, and `Isometric4` preset removes the new workflow surface.
- Removing `SolidBackgroundColor` and its render-state transaction returns print-scale export to its previous background behavior.
- The existing standard eight-view and print-scale request defaults remain compatible when the new options are omitted.
- Persistent lot colors, groups, and named views are covered by Rhino Undo. Export background changes are temporary and do not depend on Undo.

## Current Remaining Items

- Run `_McpLotVisualizationExportSkillSmoke` once after loading the newly built plug-in in a Rhino session where load protection has already been accepted. This will complete the in-process background restoration assertion without Computer Use.
- The built capability is in the repository but has not been installed into the user's current Rhino package. A normal package update/reload is required before the new MCP tools appear in a running session.

## Conclusion

The reusable repository skill and MCP surface are implemented and pass final Debug/Release builds, deterministic workflow smoke, CLI fallback smoke, and the 159-tool MCP safety inventory. The export path no longer changes Rhino application background settings: it uses a temporary solid document render background and always attempts exact restoration. One explicit live smoke remains after plug-in load protection is accepted; no user model was used or modified for that attempted validation.
