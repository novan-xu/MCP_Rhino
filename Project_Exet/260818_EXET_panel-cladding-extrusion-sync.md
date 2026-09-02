# Panel Cladding Extrusion Spawn and Geometry Sync Execution

## Corresponding Plan

- Plan: `Project_Plan/260818_PLAN_panel-cladding-extrusion-sync.md`
- Execution date: 2026-08-19

## Related Artifacts

- Focused test: `Project_Test/260818_TEST_panel-cladding-extrusion-sync/`
- Updated regressions: spawn, surface sync, offset sync, regions, command surface, and topology
  persistence test projects under `Project_Test/`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.41/`
- Installed plug-in:
  `%LOCALAPPDATA%/PanelCladdingEditor/plugin/1.0.41/PanelCladdingEditor.rhp`
- Commit / PR: none created in this execution.

## Execution Result / Actual Delivered Scope

`PCSpawn` now creates cladding surfaces and extrusion curves in the same preparation/Undo/rollback
operation. A shared application planner expands the stored H/V offsets, segment mask, and merge
mask into:

- `FRM_0` through `FRM_3` frame curves;
- one curve for every present unmerged atomic segment; and
- one joined curve for every saved merge run.

Every curve is created on `02_CW Extrusions::Curves-PNL::Main Frame`, inherits the panel PID, stores
its editor code in `CRV`, and receives `<panel CID>-<curve code>` as its CID. Curve and frame labels
in the editor use the same underscore convention.

New cladding surfaces are planned below `04_STEP Surfaces`. The exact legacy
`03_Material Surfaces (STEP)` root remains readable by PCSync so existing project geometry can be
migrated without moving it first.

The old `PCSyncFromSurfaces` command type was replaced by `PCSync` while retaining its explicit
command GUID. PCSync now enumerates both supported material-surface layers and the exact extrusion
curve layer. It associates objects to selected panels using geometric coincidence instead of their
existing PID/CID/CRV values, then:

1. derives divider offsets from surface boundaries and curves;
2. derives missing atomic segments from absent curve spans;
3. derives merge runs when one physical curve covers adjacent atomic bays;
4. reconstructs surface coverage and canonical material-owner/parent-cell values from geometry and
   material leaf layers;
5. recalculates the panel type/signature and topology masks; and
6. repairs PID/CID/Cladding on surfaces plus PID/CID/CRV/name on curves.

PCSync also writes:

```text
CW_2.01_UNIT_WIDTH=<width with five decimals>
CW_2.02_UNIT_HEIGHT=<height with five decimals>
CW_2.00_UNIT_DIMENSION=<width>x<height with five decimals>
```

All Rhino mutations remain within one Undo record and roll back original attributes if any object
or external finalization fails.

## Differences From Plan

- Existing surfaces under the legacy root are repaired in place rather than automatically moved to
  the new root. This preserves manually organized project layers while making `04_STEP Surfaces`
  authoritative for every new PCSpawn operation.
- Geometry association requires curves/surfaces to be coincident with the selected panel within the
  document tolerance envelope. Ambiguous coincident selected panels fail closed and are selected for
  inspection.
- Rhino-native Brep/curve probes cannot load `rhcommon_c` from a standalone `dotnet` host, so those
  existing probes reported their designed `[SKIP]` status. The RhinoCommon code compiled in Debug
  and Release; managed topology, planning, orchestration, command, and UI regressions all passed.

## Problems Found And Fixed During Construction

- The existing sync repository discarded surfaces with missing PID before geometry analysis. It now
  discovers by layer and associates by geometry, allowing stale/missing object metadata to be fixed.
- The old planning tests assumed duplicate/stale CIDs and wrong PIDs were authoritative errors. The
  geometry-owner contract now treats them as repairable metadata; true geometry overlaps remain
  fail-closed.
- `MPL-*` material codes were not recognized as the metal family despite the existing material
  catalogue using that prefix. `MPL` now routes to `Surfaces-Metal`.
- General offset formatting intentionally trims trailing zeroes, so a separate unit-dimension
  formatter was added to guarantee exactly five decimals.
- One parallel Debug solution build briefly encountered a compiler PDB file lock from lingering
  test build servers. A serialized `-m:1` rerun passed with zero warnings/errors.

## Test Record

### Focused extrusion/sync smoke

Debug and Release both exited `0` and reported:

```text
[OK] topology expands into underscore-named frame/segment/merge curves with canonical metadata.
[OK] new surfaces target 04_STEP Surfaces, legacy root remains readable, and the command is PCSync.
[OK] PCSync plans surface/curve repairs and five-decimal unit dimensions from associated layout data.
```

The fixture includes one deleted atomic segment, a complete horizontal merge, and a partial vertical
merge. It verifies exact curve counts/codes/layers/CIDs and then feeds deliberately wrong surface and
curve metadata through the real sync planner/orchestrator to prove geometry ownership controls the
repair plan.

### Existing regressions

- Every PanelCladdingEditor-linked test project passed in Debug.
- Focused, spawn, surface-sync, offset-sync, region, command-surface, and topology-persistence tests
  passed in Release.
- Existing WPF visual tests emitted their known `System.Drawing.Common` version-resolution warnings;
  all exited `0`.
- Rhino-native geometry probes reported their designed standalone-host skip.

### Builds, package, and identity

- `MCP_Rhino.sln` Debug (`-m:1`): PASS, zero warnings/errors.
- `MCP_Rhino.sln` Release: PASS, zero warnings/errors.
- Package build for version `1.0.41`: PASS.
- Direct Debug, direct Release, packaged Release, staged Release, and installed Release RHP identity
  checks: PASS.
- PanelCladdingEditor assembly/manifest/plugin ID:
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`.
- Packaged and staged RHP SHA-256:
  `3DAB931822169A74ADC5A04995077E342C3D140E20842A5A6BD887A119EE1228`.
- `git diff --check` found no whitespace errors; output contained only the repository's existing
  Windows line-ending notices.

## Package and Activation

- Version increment: `1.0.40` to `1.0.41`.
- Rhino process count at initial installation attempt: `2`; the supported installer staged the
  package rather than overwriting a loaded RHP.
- Rhino process count at activation: `0`.
- Registry-only installation and installer `Validate` mode: PASS.
- Active path: `%LOCALAPPDATA%/PanelCladdingEditor/plugin/1.0.41/PanelCladdingEditor.rhp`.
- Registry `LoadMode=1`, `IsDotNETPlugIn=1`, and `DirectoryInstall=0` point to the active RHP.

## Acceptance Criteria Alignment

- PCSpawn generates exact saved extrusion topology: implemented and focused-tested.
- New curve layer and PID/CRV/CID contract: implemented and focused-tested.
- Surface root changed to `04_STEP Surfaces`: implemented and regression-tested.
- Underscore frame/internal names: implemented in shared planner and editor UI.
- Visible command is only `PCSync`: command-surface regressions passed.
- Layout is inferred from geometry/layers, not stale associated-object metadata: implemented and
  repair-tested.
- Surface/curve/panel attributes are rebuilt: implemented through one transactional commit.
- Width/height/combined dimension keys use five decimals: focused-tested.
- Builds/package/hashes/RHP identity: passed.

## Rollback Verification

Spawn prepares every Brep and curve before opening the Undo record, deletes all newly created
objects if a later creation fails, and disposes prepared geometry. Sync duplicates original object
attributes before mutation and restores them in reverse order on attribute or finalization failure.
No live Rhino document was modified during automated validation.

## Current Remaining Item

Restart Rhino so it loads version 1.0.41, then use a saved panel with at least one deleted segment
and one merge run to perform a final visual `_PCSpawn` / manual geometry edit / `_PCSync` acceptance
pass.

## Conclusion

Panel cladding spawn and synchronization now share one extrusion topology contract. PCSpawn creates
surfaces and exact curve objects; PCSync treats geometry and designated layers as authoritative,
rebuilds panel masks/cells/dimensions, and repairs all associated metadata. Version 1.0.41 is built,
identity/hash verified, installed registry-only, and ready for Rhino restart.
