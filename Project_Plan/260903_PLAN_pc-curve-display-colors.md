# Panel Cladding Curve Display Colors Plan

## 背景

Managed panel-cladding extrusion curves currently inherit one layer color because spawn and update
attributes use `ObjectColorSource.ColorFromLayer`. The extrusion plan already distinguishes the
outer/main frame (`Kind = Frame`) from intermediate curves and records horizontal/vertical axis, so
the requested visual classification can be derived without geometry heuristics. Existing baked
curves also need correction, not only future creations.

## 目标

- Display main-frame curves in named .NET Blue (`RGB 0,0,255`).
- Display horizontal intermediate curves in named .NET Purple (`RGB 128,0,128`).
- Display vertical intermediate curves in named .NET DarkGreen (`RGB 0,100,0`).
- Apply the same authoritative rule to new `PCSpawnCrv` curves, retained/new `PCUpdate` curves, and
  existing curves processed by `PCSyncCrv`.
- Keep surfaces, curve geometry, layer paths, PID/CID metadata, lock/hidden handling, and Undo scope
  unchanged.
- Ship the behavior as the next standalone plug-in patch package without production activation from
  the agent context.

## 架构归属

- `Domain`: carry the resolved RGB value on the extrusion curve plan and sync DTOs.
- `Application/Services/PanelCladding`: own deterministic kind/axis-to-color planning and include
  color mismatch in curve sync change detection.
- `Infrastructure/Rhino/Live/PanelCladding`: write Rhino object color/source during spawn, update,
  and sync read/commit.
- `Project_Test/260903_TEST_pc-curve-display-colors`: focused planning/source/sync/package regression.
- `Packaging/PanelCladdingEditor`: version and behavior documentation.

No MCP tool, resource, Router, command-registration, or public command-name changes are required.

## 关键设计

1. Add authoritative RGB constants and a resolver to
   `PanelCladdingExtrusionPlanningService`: frame kind wins over axis; non-frame horizontal/vertical
   curves use Purple/DarkGreen respectively.
2. Persist the resolved RGB on every `PanelCladdingExtrusionCurvePlan`.
3. `PCSpawnCrv` and the curve branch of `PCUpdate` set
   `ObjectColorSource.ColorFromObject` plus the planned RGB. Surface attributes remain layer-colored.
4. Extend curve-sync snapshot/plan DTOs with current source, current RGB, and desired RGB.
5. `PCSyncCrv` marks a curve changed when it is not object-colored or its RGB differs, then writes
   the authoritative object color while retaining all unrelated duplicated attributes.
6. Existing curves change only within the selected-panel command scope; no global document migration
   or unrelated curve mutation is introduced.

## 涉及文件

- `src/PanelCladdingEditor/Domain/PanelCladdingModels.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingExtrusionPlanningService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSurfaceSyncPlanningService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSpawnService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingUpdateService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSurfaceSyncRepository.cs`
- current package-version assertions in existing PanelCladdingEditor regressions
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj` for one precise standalone-test exclusion
- `Packaging/PanelCladdingEditor/package-manifest.json`
- `Packaging/PanelCladdingEditor/README.md`
- `Project_Test/260903_TEST_pc-curve-display-colors/`
- `Project_Exet/260903_EXET_pc-curve-display-colors.md`

## 使用方式

- Run `_PCSpawnCrv` for newly baked curves.
- Run `_PCUpdate` to rebuild/reconcile and recolor all expected managed curves for selected panels.
- Run `_PCSyncCrv` to repair metadata and recolor mismatched existing managed curves without rebuilding
  their geometry.

## 验收标准

- All four frame plans resolve to `RGB 0,0,255` regardless of frame axis.
- Horizontal segment/merged plans resolve to `RGB 128,0,128`.
- Vertical segment/merged plans resolve to `RGB 0,100,0`.
- Spawn and update curve attributes use `ColorFromObject` and the planned RGB; surface attributes
  remain `ColorFromLayer`.
- Curve sync detects both wrong source and wrong RGB and commits the desired object color.
- Existing PCUpdate Undo/lock handling and PC command regressions remain passing.
- Focused Debug/Release regressions, plug-in and solution Debug/Release builds, package registration,
  assembly identity, and hash checks pass.
- `git diff --check` reports no errors attributable to the change.

## 风险与回退方案

- Risk: frame color could accidentally follow its horizontal/vertical axis. Mitigation: the resolver
  tests frame kind before axis and tests all four frame plans.
- Risk: sync could skip curves whose metadata is already correct. Mitigation: current color source
  and RGB participate explicitly in `MetadataChanged`.
- Risk: surfaces could become object-colored. Mitigation: update and spawn branch by curve plan;
  surface regressions require `ColorFromLayer` to remain.
- Rollback: remove the display-color fields/resolver/writes, focused regression/exclusion, README
  note, and package bump. No installed plug-in or live document is modified during construction.

## 后续扩展方向

- Move named colors into a user-configurable project palette if customization becomes a requirement.
- Add a native Rhino fixture that checks viewport draw colors for newly spawned and synchronized
  existing curves, including locked/hidden managed states.
