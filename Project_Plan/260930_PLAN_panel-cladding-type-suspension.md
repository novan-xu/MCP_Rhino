# Suspend cladding type metadata

## Background and authorization

On 2026-09-30 the user requested removal of the cladding_type key/value set while
they reconsider the factors and need for a combined identity. This explicitly
authorizes implementation and cleanup in the previously specified open document.

## Goals and architecture ownership

Suspend the derived cladding type in the standalone PanelCladdingEditor application.
Keep material assignments, topology masks, cladding logic, and frame typology intact.
No MCP capability, transport, command registration, or workbook schema changes.

## Design and affected files

- Stop type generation, reading, and persistence in editor saves and surface/curve sync.
- Use region resolution directly for existing material/owner validation; retain
  unsupported-geometry and topology validation independently of hashing.
- Remove the calculated cladding type UI. Preserve dormant identity/workbook utilities
  and compatibility DTOs for future redesign; active save/sync results carry no type.
- Centralize exact retired attribute names (2.14, 2.13, 1.10, and legacy 4.00).
  Saves in all scopes and changed-panel sync remove them; PCClear recognizes them.
- Extend existing scoped-save, clear, and sync regressions, and document results in
  `Project_Test/260930_TEST_panel-cladding-type-suspension/` and the matching EXET.
- Refresh the unactivated 1.0.78 package to include this follow-up schema change.
- Through existing Router-selected live tools, preview and remove only the retired
  type attributes, verify all unrelated object attributes and metadata, and log activity.

## Usage and acceptance

PCEditor and sync commands no longer produce a combined cladding type after the new
RHP is activated. Debug/Release builds and affected regressions must pass. Verify
assembly GUID and bundle hashes. Live cleanup must remove every targeted entry with
no unrelated differences and retain Rhino Undo. Saving the document remains in Rhino.

## Risks and rollback

The currently loaded 1.0.76 RHP cannot be hot-replaced and may recreate old keys.
Build and stage only; preserve active files until independent host registry
attestation permits activation under AGENTS.md. Do not claim installed validation.
Undo reverts the document cleanup. Source changes can be narrowly reverted; dormant
identity code is preserved without exposing it in the active editor or sync flow.

## Future work

Reintroduce an explicitly designed identity only after the user chooses its factors.
Automatic whole-document migration of other files is outside this request.
