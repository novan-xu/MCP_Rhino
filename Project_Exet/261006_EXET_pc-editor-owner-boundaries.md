# PC editor owner-boundaries EXET

## Corresponding plan

[PLAN](../Project_Plan/261006_PLAN_pc-editor-owner-boundaries.md).
Execution date: 2026-10-06.

## Related artifacts

[TEST commands and renders](../Project_Test/261006_TEST_pc-editor-owner-boundaries/README.md)
and [verification summary](../Project_Test/261006_TEST_pc-editor-owner-boundaries/verification-summary.json).
No commit or PR was requested or created. All earlier fixes and unrelated work
were preserved.

## Implemented scope

The cladding canvas now expands the current logical groups and resolves ownership
using the same Application services as save/spawn. The logical-cell service exposes
its existing expansion over already-built groups, so unsaved deleted boundaries
are included without duplicating expansion rules.

The renderer checks each physical neighbor pair once and uses actual grid-edge
positions. Edges between different logical cells in one resolved cladding region
are dashed. Different owners remain solid even when materials match. Interiors of
deleted-boundary groups and the panel perimeter are excluded. Dash weight and
material-backed gaps are preserved. Rendering does not write masks or assignments.

## Deviations from plan

None. Existing cell-topology and visual-polish smoke projects supply in-process
WPF verification. No new Rhino command, MCP tool, or desktop automation was added.

## Issues found and fixed

The old renderer only compared direct child/parent pairs and required adjacent
representative row/column numbers. A spanning 0D cell can touch 2D despite those
column labels differing by two, and 0B/2A can share a common owner without either
referencing the other. Those cases were skipped or drawn at an incorrect position.
Resolving the complete ownership graph and checking physical edges fixes both.

## Test record

The screenshot reconstruction failed against the old renderer (two dashed segments
instead of five), then passed in Debug and Release. Tests verify all requested
boundaries, indirect/hidden-member references, alternative owners, separate
same-material regions, nonrectangular cells, deleted interiors, cycles, and blanks.
Seven render fixtures were generated; the screenshot reconstruction and L-shaped
fixture were visually inspected. Visual-polish pixel checks confirm the existing
dash/gap weight and no white halo. Logical-cell cleanup, region planning, and
topology/save-reload regressions passed in Debug. The optional native Rhino Brep
probe reported skipped, not passed.

Standalone Debug/Release builds and Release packaging passed with zero warnings
and errors. Narrow product builds are sufficient because no MCP host, Router,
registration, or transport changed. Assembly identity passed before packaging and
on the staged RHP; all 27 bundle hashes matched. RHP SHA-256:
`cd2dc3c7ad9fd355dac46841cafff4d8686a54580467d1209de11f534e78a19b`.
Final `git diff --check` passed. Exact commands are in TEST.

## Acceptance alignment

0D/2D, 0C/2C, 0B/1A, 0B/2A, and 1A/2A are dashed in the reconstructed example.
0A/0B is solid. Moving ownership to a different label or chaining references
preserves the same dashed boundary set. Independent equal-material owners retain
solid boundaries; deleted interiors do not reappear.

## Rollback verification

This change only reads layout state for rendering. Existing editor deletion/Undo
and topology-save/reload tests passed. No installed RHP, registry key, or live
document changed. Source rollback can remove this follow-up independently of the
previous fixes. Historical Add H/Add V PNGs regenerated during testing were
restored instead of replacing unrelated reference artifacts.

## Remaining items

Version 1.0.84 is staged at
`%LOCALAPPDATA%\PanelCladdingEditor\staged\1.0.84-owner-boundaries-261006`.
Installation and native Rhino acceptance with the user's saved panel remain
pending. Production activation was not attempted because independent host registry
attestation is not established, as required by AGENTS.md.

## Conclusion

Dashed boundaries now follow resolved cladding ownership and physical adjacency.
Implementation, off-screen rendering checks, and regression builds are complete;
version 1.0.84 is staged, not installed.

## Production installation follow-up — 2026-10-06

The user requested installation and then confirmed the remaining two Rhino
processes had closed. Independently confirmed zero Rhino processes before running
the installer. Rechecked all 27 staged bundle hashes and the assembly GUID.

Before activation, launched the existing
`Project_Test/260930_TEST_panel-cladding-type-suspension/Host-PanelCladdingActivation.ps1`
helper in a hidden same-user Windows PowerShell process through Win32_Process.Create.
Probe mode wrote a harmless nonce, independently read back through the Windows WMI
registry provider's HKEY_USERS/current-user view. The prior real registration pointed
to an existing 1.0.79 RHP. Removed the temporary probe and verified its absence.

The verified host helper checked installer move paths, preserved the prior manifest
and registration export, then ran the staged installer with Install and mandatory
Validate. Both passed. After the installer exited, a different host process ran
Validate and captured the installed registration. Independent acceptance passed:

- Exact existing 1.0.84 RHP path and staged SHA-256 match.
- Exactly eleven expected PC commands with correct registration values.
- Root, PlugIn, and CommandList timestamps advanced from the preflight snapshot.
- Windows registry provider independently agrees on the new RHP path.
- Ownership manifest reports 1.0.84; the prior 1.0.79 RHP survives in the owned
  rollback directory with its original hash.

Portable evidence is in
[activation-summary.json](../Project_Test/261006_TEST_pc-editor-owner-boundaries/activation-summary.json).
Raw host snapshots, installer output, prior ownership manifest, and registry export
remain ignored local evidence in the same TEST folder. No UI automation was used.

Production installation and independent registration validation are complete.
Rhino remains closed. Exact loaded-module and post-start timestamp-lifecycle
verification remain pending the next Rhino startup; no live command execution is
claimed. This installation includes all fixes through version 1.0.84.

## Post-start registration verification — 2026-10-06

Rhino process 15624 subsequently loaded the exact installed 1.0.84 RHP. A fresh
independent host snapshot confirms the installed SHA-256 and exact eleven-command
registration. Root and CommandList timestamps advanced after startup, while the
installer-owned PlugIn key remained unchanged. Post-start installation validation
passed; the activation summary includes module and timestamp evidence. No live
geometry command acceptance is claimed.
