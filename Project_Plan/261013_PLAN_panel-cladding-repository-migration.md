# Panel cladding repository migration

## Background

The user explicitly requested migration of the panel cladding plug-in out of
MCP_Rhino into `C:\Projects\PanelCladding` and creation of a private GitHub
repository. That instruction authorizes this PLAN and its execution.

## Goals

Create an independently buildable PanelCladding repository, preserve the selected
plug-in history and development records, publish it privately under the existing
GitHub account, and remove active panel ownership from MCP_Rhino.

## Architecture ownership

The standalone `src/PanelCladdingEditor` project already has no MCP project
dependency. Its source, `Packaging/PanelCladdingEditor`, design assets, and
panel-specific PLAN/EXET/TEST records belong in the new repository. Shared identity
validation is retained in MCP_Rhino and copied/adapted for PanelCladding.

## Key design

- Preserve relative paths within the extracted repository so project references
  and packaging paths remain valid; introduce `PanelCladding.sln` and root guidance.
- Filter history only in a fresh clone. Never rewrite MCP_Rhino history.
- Move all panel-specific development records, including active/incomplete ones,
  as a repository migration, not an archival operation.
- Update executable scripts that locate the old solution/root; historical reports
  retain their original evidence and links.
- Remove panel solution entries and obsolete exact TEST exclusions from MCP_Rhino.
- Preserve the existing unrelated `.gitignore` edits.
- Keep the product name, version, GUID, command names, and installed RHP unchanged.
- Publish a private `novan-xu/PanelCladding` repository after build validation.

## Affected files

The standalone source/packaging/design trees; panel-specific Project_Plan,
Project_Exet, Project_Test records; both solution files; root README/AGENTS/guidance;
the MCP Server csproj; shared plug-in identity validation.

## Usage

From the new root, build `PanelCladding.sln` in Debug and Release. Existing package
and smoke commands continue to use the same relative paths.

## Acceptance

1. Selected tracked files and their relevant history are present in the new repo.
2. Panel and MCP solutions build in Debug and Release independently.
3. Representative managed panel regressions pass from the new location.
4. Compiled RHP assembly identity matches the manifest and plug-in declaration.
5. Packaging succeeds with matching hashes and no MCP dependency.
6. New remote is private and its default branch matches the local commit.
7. MCP checkout contains no active panel implementation or build dependency.

## Risks and rollback

Root-discovery logic and cross-product tests can retain hidden MCP assumptions;
scan executable files and adapt them before validation. Preserve local ignored
assets when moving trees. Verify resolved source/destination boundaries before
filesystem moves. The original source commit and unchanged MCP history provide
rollback; do not remove the original files until the extracted tracked tree is
verified. Production registry activation is outside this source migration.

## Future extensions

Independent releases and CI can be added in PanelCladding as later work.
