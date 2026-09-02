# Panel Cladding Topology Persistence Execution

## Corresponding Plan

- Plan: `Project_Plan/260818_PLAN_panel-cladding-topology-persistence.md`
- Execution date: 2026-08-18

## Related Artifacts

- Focused tests: `Project_Test/260818_TEST_panel-cladding-topology-persistence/`
- Production implementation:
  - `src/PanelCladdingEditor/Domain/PanelCladdingModels.cs`
  - `src/PanelCladdingEditor/Application/Services/PanelCladdingKeyService.cs`
  - `src/PanelCladdingEditor/Application/Services/PanelCladdingTypeSignatureService.cs`
  - `src/PanelCladdingEditor/Application/Services/PanelCladdingSaveService.cs`
  - `src/PanelCladdingEditor/Infrastructure/Rhino/LivePanelCladdingRepository.cs`
  - `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml.cs`
  - `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSurfaceSyncService.cs`
  - `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSurfaceSyncRepository.cs`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.38/`
- Installed plug-in: `%LOCALAPPDATA%/PanelCladdingEditor/plugin/1.0.38/PanelCladdingEditor.rhp`
- Commit / PR: none created in this execution.

## Execution Result / Actual Delivered Scope

Structural edits in the extrusion view are now first-class saved panel data. Save is enabled for a
projectable panel even when extrusion segments, merged curve runs, mullions, divider offsets, or
dimensions changed. The former structural-preview refusal dialog and Save-disable condition were
removed.

The panel retains the existing canonical H/V offset keys and base-cell cladding keys. Two new object
user-text attributes carry the complete irregular extrusion topology:

- `CW_2.05_SEGMENT_MASK`: all atomic segment-presence bits for the panel;
- `CW_2.06_MERGE_MASK`: all adjacent-segment join bits for the panel.

No per-curve topology keys are written. Each value is a compact Base64 representation of a
versioned binary payload. Its header identifies the payload kind and records horizontal-track,
vertical-track, column, and row counts. The remaining bytes pack the canonical bits. Horizontal
tracks/bays are ordered bottom-to-top and left-to-right; vertical tracks/bays are ordered
left-to-right and bottom-to-top.

The segment and merge semantics remain independent:

- a zero segment bit removes that cell boundary and merges the adjacent logical cells;
- a one merge bit joins two adjacent present segments into one continuous curve without changing
  the logical cells;
- merge runs crossing an absent segment are rejected.

Legacy panels without either key decode as all intermediate segments present and no merged curve
runs. Payload version, kind, dimensions, byte length, padding, segment coordinates, run ranges, and
merge/presence consistency are validated. A bounded bit-count guard prevents invalid payload
dimensions from causing excessive allocation.

`PanelCladdingEditorWindow` now maps atomic extrusion ids to stable topology coordinates when
saving, and reconstructs `_deletedExtrusions` and `_mergedGroups` from the decoded state when the
panel reloads. Added H/V tracks and their complete current cell dictionary are submitted to Save;
the prior source-cell filter that discarded new cells was removed. Obsolete source grid keys are
deleted during the same Rhino attribute commit.

Type identity advances from v3 to v4. The canonical payload now includes both compact topology
masks in addition to unit-normalized size, H/V offsets, and normalized cell values. Blank cells are
valid in the identity so structural-only edits can be saved before material assignment. Surface
sync carries and rewrites both masks when it commits a panel grid, preventing stale-dimension masks.

## Differences From Plan

The implementation followed the planned two-key schema and application/UI ownership. Two bounded
integration adjustments were added during execution:

1. Type identity permits blank cladding cells. Without this, Save would be enabled after a
   structural edit but still reject the common unassigned state created by deleting or adding a
   divider.
2. Surface-sync panel writes now preserve/rewrite topology masks. This prevents a surface-inferred
   grid change from leaving an old mask whose embedded dimensions no longer match.

The existing `PCMatch` contract intentionally remains target-offset-owned and was not broadened to
copy these masks; its established regression confirms that behavior.

## Problems Found And Fixed During Construction

1. Save used the live source layout rather than the editor's working offsets/cells, so even enabling
   the button alone would have persisted the old grid. Save now validates and signs a requested
   working layout.
2. The UI filtered `CellValues` to the source keys, silently excluding cells created by Add H/V.
   Save now submits the full working cell dictionary.
3. Load unconditionally cleared `_deletedExtrusions` and `_mergedGroups`. It now restores both from
   decoded topology state before building the working layout.
4. Type signature v3 did not distinguish absent boundaries or curve continuity. v4 hashes both
   masks.
5. The MCP server project compiles repository test sources by default. The standalone WPF focused
   test folder was added to the established precise exclusion list so server builds remain isolated.
6. The first broad match regression failed after an exploratory mask-transfer change. That change
   was removed because `PCMatch` deliberately preserves target-owned divider configuration; the
   regression then passed without weakening its contract.

## Test Record

### Focused topology-persistence smoke

Commands:

```powershell
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-topology-persistence\PanelCladdingTopologyPersistenceSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-topology-persistence\PanelCladdingTopologyPersistenceSmoke.csproj -c Release
```

Both final runs exited `0` and reported:

```text
[OK] One panel-level segment mask and one panel-level merge mask round-trip.
[OK] Structural Save writes edited offsets/cells/topology and a topology-sensitive v4 identity.
[OK] Deleted boundaries and merged curve runs survive editor Save/reload.
```

The smoke verifies:

- segment and merge mask round-trip with exactly two panel topology keys;
- legacy defaults when both keys are absent;
- explicit failure for lattice-dimension mismatch;
- rejection of a merge run crossing a missing segment;
- expanded H grid and new cell-key writes;
- no `INT-*` or other per-curve topology attribute;
- different identities for identical segment topology with merged versus separate curves;
- Save remains enabled while `_structuralDirty` is true; and
- an editor Save/reload restores one deleted horizontal boundary and one three-segment continuous
  vertical curve, then returns both dirty flags to false.

The stateful test repository invokes the real application Save service and reparses the committed
user-text writes. It does not mutate Rhino, a workbook, package, registry entry, or installation.

### Existing regression coverage

The following final regression groups exited `0`:

- cell topology Debug and Release;
- regions/signature Debug and Release;
- standalone editor Debug;
- surface sync Debug and Release;
- panel match Debug;
- direct interactions Release;
- latest UI Release;
- extrusion visuals Release;
- extrusion fidelity Release; and
- wheel zoom/aspect Release.

They retain delete/add/renumber/undo behavior, material/parent assignment, selection, curve merge and
explode interaction, dimension editing, wheel zoom, type normalization, surface sync, and the
target-owned `PCMatch` configuration contract.

### Builds and identity

Final commands and results:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug
dotnet build .\MCP_Rhino.sln -c Release
```

- Debug: PASS, zero warnings/errors on the final rerun.
- Release: PASS, zero warnings/errors.
- Debug direct RHP identity: PASS.
- Release direct RHP identity: PASS.
- Packaged Release RHP identity: PASS.
- Installed Release RHP identity: PASS.
- PanelCladdingEditor assembly/manifest/registry plug-in id:
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`.
- Plug-in ids remain distinct from MCP_Rhino.
- `git diff --check`: PASS; existing line-ending notices only.

### Package and installation

- Version increment: `1.0.37` → `1.0.38`.
- Bundle build: PASS.
- Rhino process count before installation: `0`.
- Supported registry-only installer: PASS; version `1.0.38` installed directly.
- Installer validation: PASS.
- Bundle RHP SHA-256:
  `0F14B591FF3947EE2FF02311F6B959DD902B7BB022A75B77EB71084125A43632`.
- Installed RHP SHA-256:
  `0F14B591FF3947EE2FF02311F6B959DD902B7BB022A75B77EB71084125A43632`.

## Acceptance Criteria Alignment

- Save functional after structural extrusion edits: passed focused UI integration.
- Exactly one segment-mask and one merge-mask key per panel: passed.
- No individual curve topology attributes: passed.
- Deleted boundary and merged curve survive Save/reload: passed.
- Added H/V offsets and new/renumbered cells persist through the working-layout request: passed.
- Invalid and mismatched payloads fail explicitly: passed.
- Legacy panels load as complete unmerged lattices: passed.
- Segment and merge topology affect v4 type identity: passed.
- Surface sync cannot leave stale topology dimensions: passed implementation and regression.
- Debug/Release tests, builds, package, installation, hashes, and identity checks: passed.

## Rollback Verification

Every live Save remains one `CommitAttributes` operation under the existing Rhino Undo record. The
focused smoke confirmed editor undo remains intact before Save and that persistence can be reparsed
after Save. No production Rhino document was changed during automated validation.

The installer migrated the owned `1.0.37` installation to `1.0.38` using its recoverable rollback
area and retained the ownership manifest. To roll back the code, revert this capability's production
and test changes and rebuild a new owned version. Existing panels remain readable by `1.0.38`; older
builds ignore the two additional object user-text keys.

## Current Remaining Items

- Restart Rhino so version `1.0.38` is loaded, then confirm one representative project panel by
  deleting a boundary, merging a run, saving, closing the editor, and reopening it.
- Physical spawned cladding geometry still uses the established regular-cell region pipeline. A
  separate future capability can make `PCSpawn` consume irregular logical-cell topology when that
  geometry behavior is approved.

## Conclusion

Extrusion-view structure is no longer session-only. The editor saves the current H/V grid, cells,
one compact segment mask, and one compact merge mask in a single Rhino attribute mutation; reload
reconstructs deleted boundaries and continuous curve runs. The topology is validated, included in
v4 type identity, regression-tested in Debug and Release, packaged and installed as version
`1.0.38`, and verified byte-for-byte against the built bundle.
