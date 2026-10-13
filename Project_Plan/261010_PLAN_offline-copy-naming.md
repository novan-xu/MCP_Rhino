# 261010_PLAN_offline-copy-naming

## Background and goal

The user explicitly requested shorter offline copy names using only `_YYYYMMDD_HHMMSS` (date and 24-hour time including seconds). Replace the prior offline-edit/UTC/GUID suffix for both bounded JS and native Rhino workflows, and rename the two just-created test copies accordingly.

## Ownership and design

The shared `OfflineCopyWriter` owns naming and atomic non-replacing publication. Use the host's local time (this host is Eastern Time), invariant numeric formatting, and strip a prior recognized new/legacy suffix when creating the next copy. Retain private GUID staging names. On a same-second collision, wait for an available actual timestamp with cancellation and a bounded timeout; never overwrite or invent an arbitrary extra suffix. Keep the original source and existing output bytes intact.

Update preview naming metadata, tool descriptions and current runtime documentation. No new tool, transport, or geometry logic. Rename only the two exact output files created in the preceding turn, preserving hashes, and record their mapping to the local-time names. This one-off user-authorized rename does not change the writer's rule against renaming source/prior files during an edit.

## Files and usage

Shared writer; JS/native preview metadata and tool descriptions; focused regression assertions; README/runtime/architecture guidance; matching TEST and EXET. Existing preview/apply calls automatically use the new suffix.

## Acceptance

Debug/Release builds and existing offline copy tests, including repeated and concurrent applies. Verify format, local timestamp, no accumulated suffix, collision preservation, cancellation cleanup and legacy-name handling. Run native smoke for the shared writer path. Rebuild the staged package with verified compiled plugin identity; no production activation. Verify renamed output hashes and original DWG hash/time.

## Risks and rollback

Seconds-only names require waiting when copies of one model are published concurrently. Bound the wait and preserve the preview token on failure. Existing timestamp-like source names follow the recognized suffix policy. Rollback is scoped to naming code/docs; renamed copies can be moved back only if the old names are free. No original model edits or output deletion.

## Future work

Additional naming formats only when explicitly requested.
