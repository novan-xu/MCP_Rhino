# Panel cladding repository migration EXET

## Plan and artifacts

Executed 2026-10-13 under
[PLAN](../Project_Plan/261013_PLAN_panel-cladding-repository-migration.md).
See [TEST](../Project_Test/261013_TEST_panel-cladding-repository-migration/) for the
file inventory, history commit map, reusable validation script and actual commands.

## Result and scope

Extracted the standalone plug-in into `C:\Projects\PanelCladding`, preserving 655
tracked files and 25 relevant historical commits from MCP_Rhino commit
`f6dc9d095c28dfc72ebcfd8a4ba7c06b336dcd71`. Moved 273 product roots/records including
source, packaging, design, active PLAN/EXET/TEST records and ignored local assets.
Copied/adapted shared assembly identity validation so each repository is independent.

Added PanelCladding.sln, root documentation and construction guidance. Removed
panel projects and obsolete exact TEST exclusions from MCP_Rhino, updated ownership
references, and preserved its pre-existing uncommitted `.gitignore` edits.

Created [novan-xu/PanelCladding](https://github.com/novan-xu/PanelCladding) and verified
GitHub reports `PRIVATE`. Product version 1.0.96, assembly/command names and plug-in
GUID are unchanged. No production installation or registry activation occurred.

## Deviations and findings

Eight extracted working files differed only in line endings; all original Git blobs
matched. The extraction checkout was normalized without modifying their contents.
The latest PID regression runner still named MCP_Rhino.sln; it now uses
PanelCladding.sln. Shared product validation now checks only the local product.
The registry test fixture namespace now belongs to PanelCladdingEditor. Historical
MCP-hosted editor smoke remains explicitly historical and is not an active project.

## Tests and acceptance

Both solutions built in Debug and Release, zero warnings/errors. Ten PID regression
runs and the standalone editor smoke passed. All 69 project references resolve
within PanelCladding. Direct compiled RHP metadata matched the manifest in both
configurations; both products retain distinct IDs. Release packaging passed,
including packaged identity and all 27 hashes. Both whitespace checks passed.
See TEST README for commands and limits.

## Rollback verification

MCP_Rhino history was not rewritten. The inventory records every moved path, and
the original commit retains all original tracked blobs. The filtered-history
commit map is preserved in TEST. Installed RHPs were untouched, so no runtime
rollback is needed. Ignored local assets moved with their owning directories.

## Remaining work and conclusion

Local extraction, independent builds/tests and private repository creation passed.
Git publication is recorded below after push confirmation. Existing historical
native/interactive acceptance limitations remain in their original records.

## GitHub publication (2026-10-13)

Private repository creation and visibility readback passed. The first published
standalone commit is `cc4923bdbc10a079cf50d72f4095cd5a68f74bb7`; local master and
`git ls-remote origin refs/heads/master` matched after push. MCP_Rhino's remote
master contained four newer commits ending at `593fd0b`; none changed any of the
655 extracted files. The cleanup integrates that upstream work before publication.

## Upstream integration validation (2026-10-13)

Integrated MCP_Rhino origin/master `593fd0b`. The only merge conflict was solution
membership; retained all upstream projects and removed only PanelCladdingEditor
and its standalone smoke project. Rebuilt the integrated MCP solution in Debug
and Release with zero warnings/errors and reran its compiled identity check.
All 97 extracted production, packaging and design Git blobs remain byte-identical
to the original commit. The pre-existing `.gitignore` diff was restored unchanged
on top of the upstream ignore addition, and remains uncommitted.

## Completed publication

MCP cleanup was merged through [PR #12](https://github.com/novan-xu/MCP_Rhino/pull/12)
as `b4b939d1dbfb39ac39864c692e1ef267da507119`. The integrated MCP Debug/Release builds
and compiled identity verification passed before merge. The PanelCladding remote
reports PRIVATE, its default branch is master, and local/remote commits matched.
Both checkouts are on master; MCP's original uncommitted `.gitignore` edits remain.
The temporary migration branches were removed after successful publication.
