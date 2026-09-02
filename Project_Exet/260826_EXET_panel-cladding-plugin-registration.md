# EXET — PanelCladdingEditor plug-in registration repair

## Corresponding plan

- Plan: `Project_Plan/260826_PLAN_panel-cladding-plugin-registration.md`
- Execution date: 2026-08-26

## Related artifacts

- Focused test folder: `Project_Test/260826_TEST_panel-cladding-plugin-registration/`
- Package bundle: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.68/`
- Commit / PR: none created in this execution.

## Execution result / actual scope

- Replaced the installer's pre-expanded root/child registry write with Rhino's clean abbreviated
  first-load registration.
- Added ownership checks for every existing root and child RHP path before replacing the owned GUID
  key.
- Made validation phase-aware: it accepts either pending first load or a complete Rhino-expanded
  command registration.
- Made validation explicitly reject the malformed pending/expanded hybrid that version 1.0.67
  incorrectly accepted.
- Required an expanded registration to expose exactly the ten supported `PC*` commands.
- Bumped, built, and activated standalone PanelCladdingEditor `1.0.68`.
- Left all panel geometry, UI, persistence, extrusion, and command behavior unchanged.

## Deviations from the plan

- No in-Rhino automated command was added. Repository rules prohibit unrequested Windows UI
  automation, so the final Plug-in Manager check remains a user-initiated first-start verification.
- The installed RHP hash is unchanged from 1.0.67 because this repair changes only packaging and
  registration; the plug-in assembly itself did not require modification.

## Problems found and fixed during execution

- The old validator required both root `FileName` and `PlugIn\FileName`, thereby validating the
  same incomplete hybrid state that Rhino had not expanded and Plug-in Manager did not list.
- Existing ownership validation inspected only one preferred path. It now rejects the registration
  if either a root or child path belongs to an unexpected RHP.
- A direct plug-in constructor cannot be used outside Rhino because RhinoCommon reserves plug-in
  instantiation for its manager. Package verification therefore uses metadata/type enumeration and
  the existing assembly GUID probe, without misrepresenting an out-of-process constructor attempt
  as a live load test.

## Test record

- PowerShell parser: both changed scripts passed.
- `git diff --check`: passed for the focused implementation/artifacts.
- Debug RHP build: exit `0`, zero warnings/errors.
- Release RHP build: exit `0`, zero warnings/errors.
- Package build for `1.0.68`: exit `0`.
- Isolated registration lifecycle smoke: exit `0`; pending, expanded, malformed, and repaired states
  behaved as specified.
- Production install: activated at
  `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.68`.
- Production installer validation: passed as `pending first load registration`.
- Installed/package RHP SHA-256:
  `8C86BAC88C68AD3218F102933E19E0DC1407E8D59658ED652A468CD8420B88E7`.
- Installed assembly enumeration: 504 managed types and ten concrete Rhino command types.
- Assembly identity regression: both shipped plug-in IDs declared, non-empty, correct, and distinct.

Detailed reproducible commands and assertions are recorded in
`Project_Test/260826_TEST_panel-cladding-plugin-registration/RESULTS.md`.

## Acceptance criteria alignment

- Clean pending registration: passed in isolated and production installs.
- Pending/expanded validation: passed in the isolated lifecycle regression.
- Old hybrid rejected and repaired: passed.
- Correct RHP GUID and ten command types: passed.
- Debug/Release build and exact package installation: passed.
- Plug-in Manager/live command presence: pending the first user-initiated Rhino start after
  activation.

## Rollback verification

The installer retained its ownership-aware file rollback flow and moved the prior `1.0.67` active
package into its installer-owned rollback record before activating `1.0.68`. The registration
replacement is restricted to the verified PanelCladdingEditor GUID key. No Rhino document or
project workbook was changed.

## Current remaining item

Start Rhino once. Rhino should expand the pending registration and list PanelCladdingEditor with
the ten `PC*` commands. If that live expansion does not occur, preserve the resulting registry state
and Rhino load message for a second-stage assembly-load diagnosis.

## Conclusion

The installer defect is repaired, regression-tested, packaged, and activated as
PanelCladdingEditor `1.0.68`. The production registration is clean and ready for Rhino's next-start
discovery pass.

## Second-stage correction — complete installer-owned registration

The 1.0.68 conclusion above was superseded by live Rhino evidence. Rhino 8.33 started at 17:27,
loaded MCP_Rhino, but left the PanelCladdingEditor shorthand record untouched and did not load the
RHP. The live process contained no PanelCladdingEditor module; the registry had no `PlugIn` or
`CommandList`; and searches found no duplicate GUID, load-protection state, command collision, or
application crash event.

Following the plan revision, the installer now writes the complete Rhino record itself:

- full root identity and startup metadata with `LoadMode=1`, managed `Type=16`, and
  `DirectoryInstall=0`;
- canonical double-leading-slash `RegPath`;
- one `PlugIn\FileName` targeting the active versioned RHP;
- exactly `PCClear`, `PCCreate`, `PCCrvTemplate`, `PCEditor`, `PCMatchCrv`, `PCMatchSrf`,
  `PCSpawnCrv`, `PCSpawnSrf`, `PCSyncCrv`, and `PCSyncSrf` under `CommandList`;
- no shorthand root `FileName`.

Validation now rejects shorthand-only and incomplete command registrations. The isolated regression
installed the complete state, rejected both defects, repaired each, and passed final validation.
Debug and Release RHP builds passed with zero warnings/errors, the assembly identity regression
passed, and the source/bundled installer hashes match.

PanelCladdingEditor `1.0.69` was rebuilt and activated while Rhino was closed at:

```text
C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.69
```

Production `Validate` reports `complete registration`, and the production registry inspection shows
the canonical child RHP path plus all ten command values. Packaged/installed RHP hashes match at
`8C86BAC88C68AD3218F102933E19E0DC1407E8D59658ED652A468CD8420B88E7`.

The only remaining item is user-initiated Rhino startup and live confirmation. Unlike 1.0.68, this
startup does not need to manufacture registry metadata; Plug-in Manager and command discovery can
read the complete record immediately.

## Third-stage live verification — plug-in confirmed loading

Date: 2026-08-26, 17:45–17:55 local.

The second-stage conclusion above recorded 1.0.69 as activated and validated, with only a live
Rhino start outstanding. That start was performed in this round, and it first exposed a drifted
production record.

### Drift found before the live start

Production state at the beginning of this round:

```text
HKCU\...\Plug-ins\7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35\PlugIn
    FileName = C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.66\PanelCladdingEditor.rhp
```

That path did not exist. `%LOCALAPPDATA%\PanelCladdingEditor\plugin\` held exactly one directory,
`1.0.69`, and `install-manifest.json` recorded `"version": "1.0.69"` installed at
`2026-08-26T21:38:35Z`. The bundled installer's read-only mode reported:

```text
VALIDATE FAILED: The complete PanelCladdingEditor registry registration is inconsistent.
```

Rhino could not load a plug-in whose registered file was absent, which is why Plug-in Manager and
the `PC*` command surface stayed empty after the 1.0.69 activation.

Ruled out during diagnosis: assembly identity (the installed RHP carries a non-empty assembly-level
`7C1A4D3B-5E29-4F68-9A72-1D8C6B0F4E35` and reports `AssemblyName: PanelCladdingEditor`), duplicate
loaders (one RHP in the tree, no `.dll` twin, nothing under `%APPDATA%\McNeel\Rhinoceros\packages`),
and load protection or a disable entry (no such value anywhere under `HKCU\...\Rhinoceros\8.0` for
this GUID).

### Repair and live confirmation

Rhino was closed, so the bundled installer ran directly rather than staging:

```powershell
.\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.69\Installer\Install-PanelCladdingEditor.ps1 -Mode Repair
.\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.69\Installer\Install-PanelCladdingEditor.ps1 -Mode Validate
```

Results:

```text
PanelCladdingEditor 1.0.69 installed registry-only at C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.69.
PanelCladdingEditor 1.0.69 registry-only installation is valid (complete registration).
```

Rhino 8 was then launched and the live process inspected without any UI automation:

```text
LOADED MODULE: C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.69\PanelCladdingEditor.rhp
PanelCladdingEditor module loaded: True
```

This is the first live load confirmation for this capability. The pending acceptance criterion
"Plug-in Manager/live command presence" is now satisfied at the module-load level; the registry
record Rhino wrote back is the same shape the loading sibling `MCP_Rhino.Server` carries.

### New finding — Rhino never writes `PlugIn\FileName`

Registry key last-write times immediately after the successful load:

```text
(root)         2026-08-26 17:54:36   <- rewritten by Rhino on load
\CommandList   2026-08-26 17:54:36   <- rewritten by Rhino on load
\PlugIn        2026-08-26 17:50:31   <- installer's write, untouched by Rhino
```

Rhino refreshes the root product record and `CommandList` on every successful load, but it does not
touch `PlugIn\FileName`. That value is installer-owned for its whole lifetime.

This retroactively explains the drifted state. Before the repair the same three keys read
`root/CommandList = 2026-08-26 15:44:16` and `PlugIn = 2026-08-25 18:58:10` — the latter being the
1.0.66 install. Rhino start-ups on 2026-08-26 kept refreshing the root and command records of a
registration whose file pointer had been stale since the previous day, so the failure presented as
a silent no-load rather than an error.

Operational consequence: a stale `PlugIn\FileName` is invisible to Rhino-side refresh and survives
any number of Rhino sessions. Only the installer's `-Mode Validate` detects it, which makes that
check the required post-install gate rather than an optional one.

### Residual item — undetermined drift origin

How `PlugIn\FileName` came to hold the 1.0.66 path after the 1.0.69 install is not established.
`install-manifest.json` is written only after `Assert-PanelPluginRegistration` passes, so the value
must have been correct at `2026-08-26T21:38:35Z`, yet the key's last-write time showed no write
since 2026-08-25 18:58:10. Rhino started at 17:39:49–17:39:55 in the interval. No installer log
exists to reconstruct the sequence, and the evidence available does not distinguish the candidate
explanations. This is recorded rather than guessed at.

Mitigation in force today: `-Mode Validate` reliably detects the condition, and `-Mode Repair`
corrects it. If the drift recurs on the next version bump, the reproduction is now cheap — compare
the three key timestamps immediately after install and again after the first Rhino start.

### Status

- Plug-in loading in Rhino: confirmed live.
- Production registration: complete and validating.
- Remaining: watch for drift recurrence at the next version bump; no code change made this round.

### Residual item narrowed — Rhino shutdown ruled out

A full Rhino start/shutdown cycle was completed at 17:52–18:00 and the registration survived intact:

```text
PlugIn FileName = C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.69\PanelCladdingEditor.rhp

(root)         2026-08-26 17:54:36   (unchanged across shutdown)
\PlugIn        2026-08-26 17:50:31   (unchanged across shutdown)
\CommandList   2026-08-26 17:54:36   (unchanged across shutdown)
```

Rhino writes this key only while loading a plug-in; closing Rhino writes nothing to it. Rhino
shutdown is therefore eliminated as the source of the earlier `PlugIn\FileName` reversion, and the
combination of one Rhino start plus one clean shutdown is now a verified-safe cycle for an already
valid registration.
