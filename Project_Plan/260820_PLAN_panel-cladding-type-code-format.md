# Panel Cladding Type Code Format

## Background

The current generated cladding type code uses the format
`<system>-CL-<columns>X<rows>-<digest>`. The `CL` marker is redundant because the value is already
stored in `CW_1.10_CLADDING_TYPE` and presented in the dedicated cladding editor.

## Goal

- Remove the literal `CL` segment from newly generated cladding type codes.
- Use `<system>-<columns>X<rows>-<digest>` as the canonical visible format.
- Keep the v4 canonical payload, full SHA-256 digest, topology/material identity semantics, and
  workbook collision expansion unchanged.
- Keep existing stored codes readable as opaque metadata; no live-panel migration is performed.

## Architecture Ownership

- `Application/Services/PanelCladdingTypeSignatureService.cs`: canonical visible type-code
  construction and digest-length collision expansion.
- `UI/PanelCladdingEditorWindow.xaml.cs`: pending-preview fallback formatting only.
- Existing and focused tests: generated-format assertions and regression coverage.

This change does not affect MCP tools, Rhino command registration, live routing, topology encoding,
or the package plug-in GUID.

## Key Design

1. Change the generated visible code from
   `<system>-CL-<columns>X<rows>-<digest8>` to
   `<system>-<columns>X<rows>-<digest8>`.
2. Preserve system-code normalization: uppercase alphanumeric, fallback `PANEL`, maximum ten
   characters, with length trimming if the final code would exceed 31 characters.
3. Update collision expansion to accept the three-part minimum shape and continue replacing only
   the final digest segment.
4. Update the editor's failure/pending preview to the same marker-free shape.
5. Do not alter the canonical v4 payload or full digest. The same panel before and after this code
   change has the same full SHA-256 identity, but a different visible type-code string.
6. Do not rewrite existing Rhino panels automatically. Their type code updates the next time a
   cladding save/sync recomputes identity.

## Files Involved

- `src/PanelCladdingEditor/Application/Services/PanelCladdingTypeSignatureService.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml.cs`
- `Project_Test/260804_TEST_standalone-panel-cladding-editor/Program.cs`
- `Project_Test/260820_TEST_panel-cladding-type-code-format/` (new)
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`
- `Packaging/PanelCladdingEditor/README.md`
- `Project_Exet/260820_EXET_panel-cladding-type-code-format.md` (after verification)

## Usage

For a four-column, three-row panel whose layer-derived normalized system code is `WT01`, the new
code will resemble:

`WT01-4X3-A1B2C3D4`

Use `Save Cladding`, `Save Both`, `PCSyncSrf`, or `PCSyncCrv` to recompute the stored type code for
an existing panel.

## Acceptance Criteria

- Generated type codes contain no `-CL-` marker.
- The standard format is exactly `<system>-<columns>X<rows>-<digest8>`.
- Normalized system prefix, grid dimensions, and uppercase digest remain present.
- Collision expansion replaces the digest with 10-20 characters without reintroducing a marker.
- The editor pending preview uses the same marker-free structure.
- Full digest and canonical payload remain unchanged by the requested system prefix.
- Focused Debug/Release tests, relevant existing regressions, Debug/Release builds, direct RHP
  identity validation, and `git diff --check` pass.

## Risks And Rollback

- Existing workbook/Rhino codes containing `-CL-` remain historical identifiers until recomputed;
  the change intentionally avoids broad document mutation.
- Any external consumer that assumes four hyphen-separated parts must migrate to three parts. The
  repository collision logic is updated in the same change.
- Rollback restores the two format strings and the former four-part collision precondition. Full
  SHA-256 identities are unaffected either way.

## Future Extensions

- If needed, add an explicit batch migration command for stored legacy type-code strings.
- Consider documenting a formal external parser contract if downstream systems begin consuming the
  visible code structurally.
