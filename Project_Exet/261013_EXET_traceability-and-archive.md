# Proportional Records And Archive Cleanup

Record level: lightweight. Execution date: 2026-10-13. Status: complete.

The user requested proportional construction records and archival of eligible older work. This is
bounded policy/housekeeping work; it does not introduce runtime capabilities. One EXET records the
change, with the archive manifest and existing builds/tests supplying validation evidence.

## Scope

Update AGENTS, the Plan Log, Architecture, runtime and Git workflows, and directory READMEs so small
changes use one EXET, substantial changes use PLAN/EXET plus meaningful validation, and routine runtime
work uses activity history. Create standalone TEST assets only when useful. Review older EXET addenda
and archive only completed, successful, inactive matching sets older than three days.

Archive moves must preserve names and contents, apart from necessary executable path repairs;
build inputs, existing exclusions, solution references, and runnable test paths must remain valid.
Retain incomplete, failed, recently published/revalidated, and ambiguous work. Preserve the existing
`.gitignore` edit and the prior script-runtime-routing changes.

## Validation and result

Archived 16 complete sets. The [archive manifest](../Project_Archive/261013_archive-manifest.md)
lists completion evidence, dates, and the reasons 25 other older sets remain active. Recent work
also remains active. All 241 files passed the immediate post-move SHA-256 check; subsequent repairs
were limited to eight archived source/README paths plus their active callers and build references.

Validation completed successfully:

- `dotnet build .\MCP_Rhino.sln -c Debug --nologo`: exit 0, 0 warnings/errors.
- `dotnet build .\MCP_Rhino.sln -c Release --nologo`: exit 0, 0 warnings/errors.
- `dotnet msbuild src/MCP_Rhino.Server/MCP_Rhino.Server.csproj -getItem:Compile -p:Configuration=Debug`: before/after snapshots contain the same 797 items after archive-path normalization.
- `dotnet run --project Project_Archive/Project_Test/260729_TEST_multi-document-rhino-router/RouterProtocolSmoke/RouterProtocolSmoke.csproj -c Debug --no-build`: exit 0; all deterministic route/selection/attestation checkpoints passed.
- `Project_Archive/Project_Test/260804_TEST_router-only-transport/RouterOnlyTransportSmoke.ps1`: all static checks passed.
- `Project_Archive/Project_Test/260730_TEST_computer-use-opt-in-policy/verify-agents-computer-use-opt-in.ps1`: passed.
- Focused policy review confirms full/lightweight/runtime levels agree across the entry point and guides. `git diff --check` passed; Git reported only line-ending conversion notices.
- `.gitignore` SHA-256 remained unchanged from the pre-archive snapshot.

Local raw inventories, hashes, compile snapshots, and build/protocol logs are under ignored
`.validation/traceability-and-archive/`. This record and the manifest retain the durable summary.
No new tests or placeholder TEST folder were needed. No installation, registry mutation, or live
Rhino modeling was performed. The archive checks do not claim to complete the retained work's
missing acceptance tests. Branch: `codex/traceability-and-archive`.

## GitHub publication — 2026-10-13

Published the policy changes, script-runtime-routing records, and archive batch in
[PR #13](https://github.com/novan-xu/MCP_Rhino/pull/13), based on source commit
`cb4f346fd4c983dc0d194bb79ce29ef829bc30d3`. The user requested upload and merge.
The pre-existing `.gitignore` modification is excluded. The GitHub connector rejected PR creation
with HTTP 403; the portable GitHub CLI created the PR using existing repository authentication.
The PR and Git history carry the eventual merge outcome; no additional runtime validation is
claimed by this publication note.

## Rollback

Use the archive manifest to reverse only this batch's moves, then reverse its path/policy changes.
Do not revert unrelated or earlier working-tree changes. No rollback has been executed.
