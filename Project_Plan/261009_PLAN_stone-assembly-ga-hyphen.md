# Stone assembly GA- naming

## Background

The user confirmed on 2026-10-09 that the current assembly convention is
`STN-MET-GA-XXXXX-XXX`. The installed 1.3.1 stone wireframe bake capability rejects
that convention because it recognizes only the retired `STN-MET-GAXXXXX-XXX` form.
The current checkout also lost the installed capability's source during a prior
sync. Its original files are recoverable from Git snapshot `c4f1a80` and registration
changes from `f31020a`; restore only the relevant capability, without applying the stash.

## Goals

- Recognize the current GA- convention and exclude `_EL`, `_V`, and legacy GA names.
- Preserve the existing upper horizontal curve selection, left-end base point,
  `WF_` output naming, exact parent `Assembly_ID`, transforms, and idempotency.
- Build and validate the updated capability, then complete the user's live bake
  once the updated plugin is loaded through the supported Router.

## Architecture ownership

The existing Domain naming model owns recognition. Restore the existing thin
Tools/Blocks wrappers, fixed Skill, Application service/interface, DTOs, live
Rhino adapter, registrations, and original smoke coverage.

## Key design

Use `STN-MET-GA-` as the assembly prefix. Keep stone family naming unchanged.
Update routing descriptions, validation errors, and regression expectations.
Inspect restored enumeration for complete model-object coverage. Preserve startup
fixes required by the installed version when staging its replacement.
Do not rename source blocks as a workaround. Do not modify or read the .3dm on disk.

## Files involved

- `src/MCP_Rhino.Server/**/StoneWireframeBake*` and related live/service/tool classes.
- Server registration and DeveloperCommandHandler smoke registration.
- Original `Project_Test/260901_TEST_stone-wireframe-bake/` coverage.
- `Project_Test/261009_TEST_stone-assembly-ga-hyphen/` validation records/scripts.
- MCP package manifest and necessary startup compatibility source.

## Usage

Preview and apply through `preview_stone_wireframe_bake` and
`apply_stone_wireframe_bake` on an explicitly selected live document. Preview
must resolve the requested GA- assemblies before apply. Verify attributed output
instances and a second preview proposing no duplicate work.

## Acceptance criteria

- Current GA- names pass; legacy names, suffixed views, and malformed names fail.
- Flat/vertical stone recognition and exact `WF_` naming remain valid.
- Debug/Release plugin builds and tool safety/capability smoke checks pass.
- The staged RHP assembly GUID matches the declared plugin identity.
- No production activation without independent host registry attestation.
- Live bake and second-preview verification run only after the updated plugin loads.

## Risks and rollback

The installed source is ahead of this checkout for this capability, requiring
targeted recovery. Keep prior installed files reachable; do not replace a loaded
plugin or restart Rhino with unsaved documents. Rhino Undo owns the eventual bake.
If installation/reload requires user action, record it as pending, not passed.

## Future extensions

None requested. Assembly attributes retain the exact source name.
