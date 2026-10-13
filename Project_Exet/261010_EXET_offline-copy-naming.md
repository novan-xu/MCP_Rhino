# 261010_EXET_offline-copy-naming

## Corresponding plan and artifacts

[PLAN](../Project_Plan/261010_PLAN_offline-copy-naming.md), [TEST](../Project_Test/261010_TEST_offline-copy-naming/README.md). Executed 2026-10-10 America/New_York. No commit/PR created; unrelated worktree changes retained.

## Delivered scope

Both offline script workflows now publish `<stem>_YYYYMMDD_HHMMSS.3dm` with invariant numeric formatting and host local date/24-hour time. The shared copy writer strips a recognized old/new suffix before appending the new one. Final filenames no longer include `offline-edit` or a GUID. Private staging files still use GUIDs and create-new semantics.

Atomic non-replacing publication preserves the source and every previous output. If a same-second name is occupied, the writer waits for the next actual second, honoring cancellation and a ten-second retry bound. On failure, staging is removed and the preview remains available for retry. Preview naming metadata, tool descriptions and current README/runtime/architecture guidance are aligned.

The remaining verified project output was renamed to `ROW 1_LINTEL STONE_20261010_223354.3dm`, preserving its original Eastern local generation time and its SHA-256 `65BB9146A0C094F5CECFF26131E18E2E9C6636D58B7C1755CD269721143ACD29`. It remains 17,412,983 bytes. The original DWG hash/timestamp still match the previous acceptance record. This was an explicit user-requested filename change; the offline writer itself does not rename old files.

## Deviations and issues resolved

The plan initially covered both test copies, but the first copy was already absent when inspected. No file was recreated; only the remaining verified copy was renamed. Preflight rejected the missing file before making any change, then the remaining file was independently hash-checked and moved without replacement.

The solution build emits PanelCladdingEditor's test-host shape, so the initial identity check with `-SkipBuild` correctly rejected the missing RHP. The standard identity workflow rebuilt the actual Debug/Release RHPs and passed. No production installation was attempted.

## Validation

- `Project_Test/261010_TEST_offline-copy-naming/Run-Validation.ps1`: exit 0. Debug/Release solution builds: zero warnings/errors. Existing JS/offline copy smoke passed with new assertions for exact short suffix, actual local time, metadata, no suffix accumulation, four concurrent copies, preservation of earlier copies and legacy naming. Five offline tools' safety metadata checks and actual Router stdio preview/apply passed.
- Collision cancellation test: current/near-future target names preoccupied with sentinel data; cancellation during retry left every sentinel/source intact and removed staging. Concurrent applies produced four distinct timestamp-only names.
- `Verify-PluginAssemblyIdentity.ps1 -Configuration Debug` and `Release`: exit 0. Nonempty, distinct compiled assembly GUIDs matched manifests. Packaged MCP_Rhino RHP metadata was also checked directly: `7a3fc2f0-24a8-4b79-be58-5a08cfb0d10a`.
- `Build-McpRhinoPackage.ps1`: exit 0; version 1.3.5 staged. All 181 bundle hashes matched.
- Native `OfflineRhinoSmoke.csproj` against packaged 1.3.5 Release Router: exit 0. Short naming, native C# edits, source protection, exact copy publication, freshness/replay rejection, script failure/timeout, DXF conversion and actual MCP round-trip passed. Packaged Router `--validate-install`: exit 0.
- User file rename verified by unchanged SHA-256. Original DWG hash `2CA3FAA9614B539C76AA59261AC93D693EF21A16D278DEDC1765CA1FA26B1654` and last-write UTC `2026-10-07T14:00:27Z` remain unchanged.
- `git diff --check`: exit 0.

Logs are in ignored `.validation/offline-copy-naming/`. Private old/new path mapping is in ignored `Runtime_Test/local/offline-copy-naming/renamed-copy.json`.

## Acceptance and rollback

Requested name shape, local time, both offline workflows, source preservation and collision safety passed. The remaining output's rename can be reversed using the recorded mapping only if its old path is free. No original model or existing output content was edited. No live document or geometry code changed, so the earlier lintel bake did not need rerunning.

## Remaining items and conclusion

Package 1.3.5 is staged and validated, not production-installed; the existing registry-attestation requirement remains in force. The current file and future offline copy naming implementation are updated as requested. No new tools, transports, schemas or safety annotations were introduced.
