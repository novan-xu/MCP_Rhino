# PC editor canvas assignment EXET

## Corresponding plan

[PLAN](../Project_Plan/261007_PLAN_pc-editor-canvas-assignment.md).
Execution date: 2026-10-07.

## Related artifacts

[TEST](../Project_Test/261007_TEST_pc-editor-canvas-assignment/README.md), standalone
smoke project, and five offscreen screenshots in the same folder.
Implementation commit: `0c3c0c298c99923c46f514663f31c365538f6121`.
GitHub publication: [PR #9](https://github.com/novan-xu/MCP_Rhino/pull/9).

## Implemented scope

- The canvas size badge now has labeled Width and Height lines with five decimal
  places and units.
- Current Panel reads and shortens the stored CID rather than displaying the Rhino
  object name. It preserves role suffixes and exposes the full CID in a tooltip.
  Missing CID falls back to PID/unit-type resolution; missing identifiers show an
  em dash.
- The assignment section is now a narrow floating panel inside CanvasWorkspace,
  below the size badge. It displays selected curve names and assigned profile
  cards, uses a bounded scrolling list, and retains profile drop, modifier,
  remove, clear, and Undo behavior. The sidebar catalogue stays in place.
- The floating panel appears only for valid curve selections in Extrusion view;
  clearing selection, Escape, Cladding view, or reload hides it.

## Deviations from plan

The server project also required two exact standalone-test exclusions in its
recursive test-source include. One is for this new WPF smoke; the other corrects
the already-present object-names smoke being compiled into the unrelated server.
Neither standalone test was retired or disabled. No production server logic changed.

## Issues discovered and fixed

- The old heading actually read ObjectName, not PID metadata. CID is now the
  explicit display source.
- The server Debug build initially failed with 15 missing editor-type errors due
  to the existing test inclusion described above. Debug/Release builds pass after
  the precise exclusion, and the object-names smoke passes independently.
- The new test's first Escape event was missing its RoutedEvent; corrected the
  synthetic event fixture and reran the complete smoke in both configurations.

## Test record

See TEST for exact commands, fixture scope, and output summary. The new behavioral
smoke passes in Debug and Release. Existing assignment serialization/formulas,
drop-zone contracts, scoped save, and object-name regressions pass. Standalone
editor and server Debug/Release builds pass with zero warnings and errors.
Focused project builds are sufficient because no Router, transport, host, MCP
surface, or document routing implementation changed.

## Acceptance alignment

All three requested presentation changes are implemented. In-process tests verify
selection visibility, routed profile drops, catalogue refresh, modifiers,
clear/remove, Undo, and bounded scrolling at normal and minimum window sizes.
Rendered output was inspected at both sizes. The overlay can overlap drawing
content at narrow sizes or high zoom, and disappears when selection is cleared.

## Rollback verification

Editor Undo restores removed/cleared assignment cards in the smoke. Source rollback
can revert the UI changes and exact test-inclusion additions. No production RHP,
registry registration, or live document was modified, so deployment rollback was
not needed; native Rhino Undo was not exercised.

## Remaining items

Live Rhino review and deployment are not performed. No package/version bump or
installer activation is part of this source/UI change.

## Conclusion

Implementation and local verification are complete. The changes are in the desktop
checkout and built successfully; they are not installed into Rhino.

## Installation follow-up (2026-10-07)

The user subsequently requested installation. Bumped the product package to
1.0.87 and documented the three UI changes in the packaging README. Release
publish/direct RHP rebuild passed with zero warnings/errors. Direct PE metadata
verification passed before packaging and for the final shared bundle: the editor
assembly GUID is `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, matches manifest/class,
and differs from MCP_Rhino. All 27 bundle file hashes passed.

The agent's registry view pointed at 1.0.73 while the Windows provider and host
process correctly reported the reachable 1.0.76 installation. An agent-local
LocalAppData staging copy was also invisible to the host. No production activation
was attempted through either unverified view. Used the shared repository bundle
and existing host activation helper instead. Windows PowerShell could not launch
in that host context; the available bundled PowerShell runtime launched normally.
Its harmless nonce was independently confirmed through the Windows registry
provider's explicit HKEY_USERS/current-SID view and then removed.

Only after that attestation passed, the host helper checked filesystem targets,
installed 1.0.87, and ran mandatory installer Validate. After it exited, a separate
host process ran Validate again. Installed RHP hash matches the bundle, the exact
eleven commands are registered, and all three registry timestamps advanced. The
Windows registry provider independently confirms the new FileName. See
[activation-summary.json](../Project_Test/261007_TEST_pc-editor-canvas-assignment/activation-summary.json).

Installation and independent validation passed. Rhino was closed throughout;
verification of the exact loaded RHP and post-start registry timestamp behavior
remains pending until Rhino starts. No live model was opened or modified.

## Two-column refinement and installation (2026-10-07)

The user requested two columns in the assigned-profile display. Kept the existing
220-DIP floating panel width and used a two-column UniformGrid. Each compact card
now has its thumbnail, a separate profile-code line, and a modifier/remove row.
Scrolling, full-name tooltips, and existing assignment event handlers remain.

The existing in-process smoke passed in Debug and Release, including modifier
width checks and all drag/drop, selection, removal, clear, and Undo interactions.
Added a three-profile render to show the odd final row. Inspected that render and
the minimum-size scrolling render. Standalone Debug/Release builds and package
publish/rebuild passed with zero warnings/errors. No new behavior-only tests or
server changes were needed for this presentation refinement.

The previously installed 1.0.87 RHP was observed loaded in Rhino process 13780.
Its host post-start snapshot showed root/CommandList advancement with PlugIn
unchanged. Rhino subsequently closed before activation of the refinement.

Packaged and installed 1.0.88 using the established host activation helper. Direct
compiled assembly identity verification passed before and after packaging. A new
host nonce passed the independent Windows registry-provider check before any
activation. Install plus mandatory Validate passed, followed by Validate in a
different host process after installer exit. Exact RHP path/hash, eleven command
entries, all three advanced timestamps, and provider agreement passed. See
[two-column-activation-summary.json](../Project_Test/261007_TEST_pc-editor-canvas-assignment/two-column-activation-summary.json).

The two-column refinement is installed. Rhino remained closed after installation;
its 1.0.88 live-load check is pending next startup. No live document was changed.
