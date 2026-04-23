# GitIgnore Baseline

## Background

The repository did not yet have a root-level `.gitignore`, while local development had already produced typical generated artifacts such as `bin/`, `obj/`, and validation build outputs. Without a baseline ignore policy, these machine-specific or generated files can accidentally enter version control and add noise to diffs.

## Goal

Add a root `.gitignore` that fits the current MCP_Rhino repository and prevents common development artifacts from being committed, while keeping source code, project guides, project plans, and Rhino test assets under version control.

## Architecture Mapping

- Repository root: `.gitignore` is a workspace-level source control policy file
- `Project_Plan/`: capability summary for the newly added repository baseline rule

## Key Design Decisions

### 1. Start with .NET / C# Defaults

The repository is a `.NET 8` solution, so the ignore rules cover standard C# outputs such as:

- `bin/`
- `obj/`
- `TestResults/`
- Visual Studio user files
- NuGet temporary restore artifacts

### 2. Add Project-Specific Local Artifact Guides

The repository also contains local/generated artifacts that are not part of the source model and should stay untracked:

- `_validation/file-safeguard-build/`
- Rhino backup files like `*.3dmbak`
- temporary local scripts such as `_tmp*.csx`
- `.cline/` assistant-local working state

### 3. Avoid Over-Ignoring Real Project Assets

The ignore file intentionally does **not** exclude repository content that appears to be real project assets, including:

- `Project_Guides/`
- `Project_Plan/`
- `Runtime_Test/*.3dm`

This keeps Rhino sample/test files, guide documents, and planning documents versioned.

## Files

### Added

- `.gitignore`
- `Project_Plan/260415-gitignore-baseline.md`

## Usage

After the file is added, new generated artifacts that match the ignore patterns will stay out of `git status` automatically.

If any ignored files were already tracked before this change, they will need a one-time cleanup via Git index removal, for example with `git rm --cached` on the specific paths.

## Future Extensions

- add more ignore rules if new test runners or coverage tools are introduced
- tune editor-specific rules if the team decides to version selected workspace settings
- clean previously tracked generated files if they appear in repository history or working tree
