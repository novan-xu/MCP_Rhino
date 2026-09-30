# Panel cladding key renumbering

## Background and authorization

On 2026-09-30 the user explicitly requested the plug-in behavior change and then
the same key renames in their specified open Rhino document. This authorizes the
construction PLAN -> EXET -> TEST chain and the subsequent live attribute migration.

## Goals

| Existing key | Canonical replacement |
| --- | --- |
| `CW_2.06_MERGE_MASK` | `CW_2.10_MERGE_MASK` |
| `CW_2.07_HIDE_MASK` | `CW_2.11_HIDE_MASK` |
| `CW_2.05_SEGMENT_MASK` | `CW_2.12_DELETE_MASK` |
| `CW_2.08_CLADDING_LOGIC` | `CW_2.13_CLADDING_LOGIC` |
| `CW_1.10_CLADDING_TYPE` | `CW_2.14_CLADDING_TYPE` |

Cladding logic remains material-independent ownership JSON, separate from the
binary hidden-segment mask. The renamed delete mask retains the segment mask's
existing binary encoding and polarity. Payload formats and geometry behavior stay intact.

## Architecture ownership

The standalone PanelCladdingEditor application key service owns canonical names.
Existing services and live repository consumers already reference these constants.
Packaging owns the release bundle. No MCP tool, transport, or registration changes.
Document migration uses existing Router-selected live attribute tools only.

## Design and affected files

- Replace the five constants in `src/PanelCladdingEditor/Application/Services/PanelCladdingKeyService.cs`.
- Update current package documentation and affected runnable regression expectations.
- Increment `Packaging/PanelCladdingEditor/package-manifest.json` to 1.0.78.
- Record verification in `Project_Test/260930_TEST_panel-cladding-key-renumbering/`
  and the matching EXET report. Preserve historical execution results.
- Read all old/new live attributes, reject conflicting destination values, preview
  per-key copy/remove recipes, then apply and verify every original value and all
  unrelated attributes. Keep the changes in Rhino Undo; saving remains in Rhino.

## Usage

Existing PC commands read and write the new canonical keys after the updated RHP
is activated. Migrate this requested document through the existing MCP bulk
attribute tools. No automatic migration of other documents is introduced.

## Acceptance criteria

- Standalone plug-in Debug and Release builds pass.
- Existing type/save, topology, hide, logic, clear, and update regressions pass.
- Compiled RHP assembly GUID matches the plug-in class and package manifest.
- Release bundle has only the direct RHP identity and verified hashes.
- All requested live old keys disappear, values remain byte-for-byte equal under
  the new names, and unrelated attributes/object counts are unchanged.
- Report staged versus installed state accurately; verify production activation
  independently before claiming it succeeded.

## Risks and rollback

Rhino currently has version 1.0.76 loaded. A running managed RHP cannot be replaced
in that process. Build and stage while Rhino is open, preserve its active files,
and clearly report that new PC behavior requires activation and a restart. Follow
AGENTS.md production registry attestation before any activation. Do not run the old
PC commands on migrated attributes. Revert source changes narrowly if necessary;
document recipes each create an Undo entry and the in-session preflight snapshot
supports exact verification. No disk .3dm reads or writes and no UI automation.

## Future extensions

Bulk migration of other documents and compatibility aliases are outside this request.

## Revision record (2026-09-30)

The user corrected the third destination from `CW_2.12_HIDE_MASK` to
`CW_2.12_CLADDING_LOGIC`. Correct the canonical constant and current documentation,
rebuild and refresh the staged 1.0.78 bundle, rerun the affected logic regression,
and migrate the interim key in the same live document with exact value verification.

## Second revision record (2026-09-30)

The user requested a further schema adjustment: `CW_2.05_SEGMENT_MASK` becomes
`CW_2.12_DELETE_MASK`, `CW_2.12_CLADDING_LOGIC` becomes `CW_2.13_CLADDING_LOGIC`,
and `CW_2.13_CLADDING_TYPE` becomes `CW_2.14_CLADDING_TYPE`. Update these three
canonical constants and current documentation/test expectations; preserve internal
segment-mask models and binary payloads. Rebuild and refresh the still-unactivated
1.0.78 package. Apply these exact per-key value-preserving renames to the same live
document after checking destination collisions, then verify complete readback.
Keep the first two migration summaries as history and add a third-pass summary.

## Third revision record (2026-09-30): release to lot

The user explicitly requested `CW_1.05_RELEASE` -> `CW_1.05_LOT` in the plug-in only,
having already renamed the Rhino data. Change the shared spawn metadata constant
used by surfaces, extrusion curves, update, and sync. Retain internal release DTO
names to avoid unnecessary contract changes; the required-metadata diagnostic names
the lot. Do not introduce a fallback to the former key or mutate the Rhino document.

Update current documentation and literal-key fixtures; use the existing spawn,
surface/curve inheritance, update, clear, and match tests to check the new canonical
key, leading-zero preservation, missing-lot behavior, and unrelated metadata.
Bump to 1.0.79 because 1.0.78 is already installed. Verify standalone Debug/Release
builds, compiled RHP GUID, and all package hashes. Stage while Rhino is running;
activation continues to require the production host-attestation contract. Record
this follow-up in the existing matching EXET/TEST artifacts.
