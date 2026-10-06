# Panel cladding unit type PLAN

## Background and authorization

The user explicitly requested replacing parent=1 / child=1 role detection with
CW_1.06_UNIT_TYPE. Supported values are flat, corner_parent, and corner_child.
This is a capability modification authorized by that request.

## Goals and design

Use the existing shared Application CID service as the single role interpreter.
Trim values and compare both keys and values without case sensitivity.
corner_parent maps to -P, corner_child to -C, and flat to no suffix. Recognized
unit types normalize the panel CID from its PID, including clearing a stale role
suffix when a panel becomes flat. Surface and curve CIDs use the same rule.
Ignore legacy parent/child attributes, even when they conflict with unit type.
Missing or unsupported unit types retain the existing no-role behavior: preserve
a stored custom CID and derive a base CID only when missing. Do not invent or
write unit-type metadata, modify PID, or migrate legacy flags.

## Architecture and files

Application/PanelCladdingCidService owns pure metadata interpretation. Existing
Infrastructure callers continue to handle Rhino writes and Undo. No UI, MCP,
command registration, dependency ownership, or geometry rules change.
Update the role-CID and PCUpdate regression executables, package documentation,
and package manifest. Record matching EXET and TEST artifacts.

## Usage and acceptance

Set CW_1.06_UNIT_TYPE on source panels, then use the existing PC commands.
- All three supported values produce correct panel, surface, and curve CIDs.
- Create, all editor save scopes, update selection, and both sync scopes follow
  unit type; shared-PID parent/child panels can update together.
- Old flags never determine role. Case/whitespace and role transitions work.
- Missing/unsupported types preserve existing custom CIDs; missing PID does not
  produce a panel CID write. Existing dependency isolation remains intact.
- Focused Debug/Release regressions and standalone product builds pass.
- Verify compiled GUID before packaging, stage 1.0.85, verify staged identity
  and all bundle hashes. Preserve the active RHP while Rhino remains open.

## Risks, rollback, and future work

Recognized flat panels now normalize custom CIDs as explicitly typed panels.
Documents relying only on old flags must populate the new attribute; no fallback
is intended. Rollback is limited to this shared interpretation change and its
fixtures/version documentation. Native Rhino command acceptance remains a user
session check. Any activation must follow the host-persistent registration and
mandatory installer Validate gates in AGENTS.md.
