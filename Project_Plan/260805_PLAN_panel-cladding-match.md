# Panel cladding match plan

## Background

The standalone `PanelCladdingEditor` Rhino 8 plug-in can author cladding configuration on one panel
and can spawn material surfaces from configured panels. It does not currently provide a command to
transfer an already-authored configuration to other unconfigured panels.

The requested workflow is command-only and must not add editor UI. The user first selects one or
more unconfigured target panel Breps, then selects one configured source panel Brep. The command
copies the source grid and material assignment to every compatible target.

## Goal

- Register `_PanelCladdingMatch` as a uniquely GUID-identified Rhino command.
- Prompt for multiple unconfigured target panel Breps, followed by exactly one configured source
  panel Brep.
- Match the source panel's H/V grid and logical cladding-cell assignments onto every target.
- Preserve each target's geometry, object identity, layer, name, and unrelated user text, including
  target-specific `CW_1.*` metadata such as PID, release, wall type, and CID.
- Validate the source and all targets before mutation and commit the complete batch in one Rhino
  Undo record.
- Report the source id, updated target count, and rejected condition clearly on the Rhino command
  line.

## Architecture ownership

- `Domain/Models/PanelCladding`: geometry-match descriptors, configuration snapshots, batch plans,
  and results without RhinoCommon dependencies.
- `Application/Services/PanelCladding`: source/target eligibility, exact configuration-transfer
  planning, canonical key generation, and fail-closed validation.
- `Application/Interfaces`: live match-service contract.
- `Infrastructure/Rhino/Live/PanelCladding`: live object/user-text reads, stable local-frame geometry
  comparison, batch attribute mutation, rollback, redraw, and Undo coordination.
- `UI`: two-stage Rhino selection and concise command-line reporting only.
- `Project_Test/260805_TEST_panel-cladding-match/`: pure planning and assembly-contract regression
  coverage, plus documented live Rhino verification.

## Key design

### Command and selection flow

1. `_PanelCladdingMatch` always prompts for one or more target Breps with `GetMultiple`.
2. It then prompts for exactly one source Brep with `GetObject` and excludes every target id.
3. A target is considered unconfigured only when it has no nonblank
   `CW_4.<column>_CLADDING_<cell>` value and neither `CW_1.10_CLADDING_TYPE` nor
   `Signature` is populated. Existing H/V keys alone are allowed and will be
   replaced by the matched configuration.
4. The source must have a valid continuous H/V key set, a nonblank value for every logical cell,
   and populated cladding type/signature keys. A one-cell source with no H/V divider is valid.
5. If any selected object fails eligibility, the whole command fails before modifying any target.

### Geometry compatibility and mapping

- Matching is exact configuration transfer, not automatic stretching. Supported source and target
  panels may have different local extents, planar/curved classifications, and depth profiles.
- Use the existing stable panel-frame rules. Rotation and translation in the Rhino document are
  allowed because comparison and mapping happen in panel-local coordinates.
- The source's `H<n>` and `V<n>` distances are copied exactly. Cell material values are copied by
  logical key/label (`0A`, `0B`, `1A`, ...), so source lower-left `0A` maps to target local
  lower-left `0A`.
- Mirroring is not inferred from proximity. A target's stored `Plane` user string, when present,
  remains the orientation authority. Otherwise the existing deterministic frame orientation is
  used.
- Each copied horizontal offset must fit strictly inside the target height, and each copied vertical
  offset must fit strictly inside the target width under model tolerance. A target whose extents do
  not contain the copied offsets fails with a geometry mismatch.

### Attribute mutation contract

- Prepare a canonical configuration snapshot containing:
  - `CW_2.03_OFFSET_H<n>` values;
  - `CW_2.04_OFFSET_V<n>` values;
  - `CW_4.<column>_CLADDING_<cell>` values;
  - `CW_1.10_CLADDING_TYPE` and `Signature`.
- Before applying the snapshot, remove all existing target keys in those same offset, cell,
  type-code, and signature families. This prevents stale higher-index keys from surviving a match.
- Do not copy or delete `CW_1.*` identity metadata, the `Plane` orientation key, workbook document
  settings, object name, object color, layer, or any unrelated user text.
- Duplicate the attributes for every target and validate all prepared mutations before starting the
  Undo record.
- Apply all target attribute changes inside one `BeginUndoRecord("Match Panel Cladding")` /
  `EndUndoRecord` pair. If a later target update fails, restore the earlier targets' original
  attributes before returning failure.
- Redraw once after a successful batch. The command creates no cladding surface geometry; users can
  subsequently run `_PanelCladdingSpawn` on the matched panels.

### Error contract

Use stable, contextual errors, including:

- `PANEL_CLADDING_MATCH_TARGET_REQUIRED`
- `PANEL_CLADDING_MATCH_SOURCE_REQUIRED`
- `PANEL_CLADDING_MATCH_SOURCE_IS_TARGET`
- `PANEL_CLADDING_MATCH_TARGET_ALREADY_CONFIGURED: <object-id>`
- `PANEL_CLADDING_MATCH_SOURCE_NOT_CONFIGURED: <object-id>`
- `PANEL_CLADDING_MATCH_GEOMETRY_MISMATCH: <object-id>`
- `PANEL_CLADDING_MATCH_ATTRIBUTE_COMMIT_FAILED: <object-id>`

## Involved files

- `src/PanelCladdingEditor/Domain/PanelCladdingModels.cs`
- `src/PanelCladdingEditor/Application/Interfaces/IPanelCladdingServices.cs`
- new `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingMatchPlanningService.cs`
- new `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingMatchService.cs`
- new `src/PanelCladdingEditor/UI/PanelCladdingMatchCommand.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorSmokeCommand.cs`
- `Packaging/PanelCladdingEditor/package-manifest.json`
- new `Project_Test/260805_TEST_panel-cladding-match/`
- new `Project_Exet/260805_EXET_panel-cladding-match.md` after successful execution

## Usage

1. Run `_PanelCladdingMatch`.
2. At the first prompt, select all unconfigured target panel Breps and press Enter.
3. At the second prompt, select one configured source panel Brep.
4. Review the updated-target count.
5. Optionally run `_PanelCladdingSpawn` on the matched targets.

## Acceptance criteria

- The command requires a saved active Rhino document and exposes no new panel/window UI.
- Multiple unconfigured targets can be updated from one configured source in one invocation.
- Source H/V offsets, cell material values, cladding type, and signature are present on every
  successful target with exact canonical keys and values.
- Target PID, release, wall type, CID, name, layer, geometry, and unrelated user text remain
  unchanged.
- A configured target, unconfigured source, source/target identity overlap, unsupported projection,
  or target that cannot contain the copied offsets prevents all mutation.
- Stale target H/V, cell, type, and signature keys are removed before the source snapshot is applied.
- A successful multi-target invocation creates exactly one Rhino Undo entry, and one Undo restores
  every target's prior attributes.
- The command type has a unique, explicit, non-empty GUID and introduces no MCP dependency.
- Dedicated planning/contract smoke tests pass in Debug and Release.
- Existing panel-editor and panel-spawn regression smokes continue to pass.
- Full solution Debug and Release builds finish with zero warnings/errors.
- Before packaging or installation, the compiled RHP assembly GUID is verified directly against the
  manifest and plug-in class GUID.

## Test strategy

- Pure planner fixtures:
  - one configured source to multiple empty targets;
  - exact H/V and column-first/bottom-to-top cell mapping;
  - canonical key-set replacement and stale-key removal;
  - preservation of `CW_1.*`, `Plane`, and unrelated user text;
  - one-cell configuration without divider offsets;
  - assigned-target, unconfigured-source, source-is-target, and unsupported-target rejection;
  - differently sized/profiled supported targets whose extents contain the copied offsets;
  - undersized targets whose extents do not contain the copied offsets.
- Assembly contract:
  - `_PanelCladdingMatch` command exists with a unique GUID;
  - live service accepts one source id and multiple target ids;
  - no MCP_Rhino or MCP SDK assembly reference is introduced.
- Live Rhino smoke documented in the TEST README:
  - match at least two rotated copies of a configured panel;
  - confirm target-specific metadata is unchanged;
  - confirm stale configuration keys are absent;
  - run one Undo and confirm both targets return to their original state;
  - verify an incompatible panel causes zero mutations.

## Risks and rollback plan

- Local-frame orientation can make a visually mirrored panel map differently. The command uses the
  stored `Plane` when available and otherwise reports the deterministic frame behavior; it does not
  guess mirror intent.
- Copied offsets can fall outside a smaller target. Validate them against each target extent and fail
  the match batch before mutation; never scale the stored source distances silently.
- Attribute deletion must not affect identity metadata. Matching is restricted to the explicit
  `CW_2.03_OFFSET_H*`, `CW_2.04_OFFSET_V*`, cladding cell, type, and signature key families, with
  preservation assertions in automated tests.
- Runtime rollback is one Rhino Undo entry. On partial API failure, original target attributes are
  restored before returning. Code rollback is isolated to the new match models, service/interface,
  command, smoke coverage, package version, TEST folder, and EXET file.

## Future extension directions

- Add an explicit proportional mode if future workflows need scaled offsets and recomputed
  type/signature/workbook records instead of exact distance transfer.
- Add an explicit mirror-X or mirror-Y option when panel orientation conventions require it.
- Allow matching from an existing cladding surface set by resolving its source PID/type metadata.
- Add a preview/report mode for mixed selections before applying any attributes.

## Revision record (2026-08-06): source selection filter

Live use showed that the second prompt could not select any configured source panel. The source
`GetObject` already has Rhino's `ObjectType.Brep` filter, but the additional custom geometry callback
also required the callback geometry argument itself to be a `Brep`. Rhino can provide component
geometry to that callback, causing otherwise valid owning Breps to be rejected.

- Remove the redundant custom geometry callback from the source prompt.
- Continue using `ObjectType.Brep`, disabled subobject selection, and the owning-object Brep check.
- Continue rejecting a source id that belongs to the target set after selection and again in the
  planning/live service contract.
- Re-run dedicated match, editor, and spawn regressions plus full Debug/Release builds before
  packaging the correction.

## Revision record (2026-08-07): different-sized target panels

Production use requires configuration transfer between panels whose overall dimensions and surface
profiles differ. The copied H/V offsets define the cladding division locations; panel width and
height are not configuration-identity constraints.

- Remove source/target width, height, geometry-class, and curved-depth equality requirements.
- Continue rejecting unsupported target projections.
- Validate the copied horizontal offsets against each target's usable height and the copied vertical
  offsets against each target's usable width. Every divider must remain strictly inside the target
  bounds under the established geometry tolerance.
- Preserve the source offset numbers exactly; do not scale or normalize them for the target.
- Keep batch planning fail-closed for match: if any selected target cannot contain the copied
  offsets, mutate none of the targets and report the incompatible target.
- Add dedicated coverage proving that differently sized and differently profiled supported targets
  accept the same configuration when its offsets fit, while undersized targets are rejected.

## Revision record (2026-08-07): cladding-only transfer

H/V offsets are panel layout data and must never be transferred by `_PanelCladdingMatch`.

- Copy only normalized cladding cell assignments plus canonical cladding type/signature metadata.
- Do not write or delete any `CW_2.03_OFFSET_H<n>` or `CW_2.04_OFFSET_V<n>` key on a target.
- Parse source and target H/V data only to establish their logical cell topology. Require the same
  ordered cell labels, but do not compare or copy offset distances.
- Preserve every target offset value exactly, including configurations whose divider positions and
  panel dimensions differ from the source.
- Keep unsupported-target and topology-mismatch rejection fail-closed for the whole selected batch.
- Align copied type/signature values with the cladding-only v2 identity schema generated by the
  standalone editor and surface-sync workflows.
- Add regression coverage proving target offsets are neither deleted nor written and that different
  valid target offset distances accept the same cladding assignment.
