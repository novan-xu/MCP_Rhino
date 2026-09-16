# Panel cladding viewport highlight execution

## Corresponding plan
[PLAN](../Project_Plan/260916_PLAN_panel-cladding-viewport-highlight.md). Execution date: 2026-09-16.

## Related artifacts
[TEST](../Project_Test/260916_TEST_panel-cladding-viewport-highlight/README.md). No commit or PR created.

## Result / implemented scope
Added IPanelViewportHighlight and the infrastructure LivePanelViewportHighlight display conduit. PCEditor composes one conduit per editor window. Successful panel loads supply runtime document serial and object GUID. Visibility and minimize state control the overlay; actual close disposes it and stops the toast timer. Focus can move to Rhino without losing the outline.

The depth-tested outline draws a dark six-pixel halo with a three-pixel cyan wire. Every frame resolves current Brep geometry and rejects deleted/hidden objects, other documents, and incompatible viewport spaces. No selection, geometry, attributes, or Undo changes are made by the adapter.

## Deviations from plan
None in implementation. Live visual acceptance remains pending; this is source/build completion, not live acceptance or deployment completion.

## Issues found and addressed
Global display callbacks need explicit document isolation. Added document serial comparison and viewport-space eligibility to prevent highlighting in unrelated documents/page spaces. Retargeting happens after successful load completion, preserving prior feedback on cancellation/failure.

## Test record
Commands and live acceptance checklist are in the TEST README. Debug and Release editor project builds succeeded with zero warnings/errors; git diff --check passed. Narrow editor builds were used because neither the MCP server nor Router surface/lifecycle is modified. Code review verified cancellation, close, and visibility wiring. No automated Rhino interaction or live smoke command was added or run.

## Acceptance alignment
Build gates passed. Target identity and lifecycle wiring reviewed. Actual viewport appearance, native redraw behavior, layer/detail visibility, and selection/Undo preservation still require the documented live checks.

## Rollback verification
No production installation, registry write, or existing RHP relocation occurred. Source rollback consists of removing the new interface/adapter and reverting the two editor UI file changes. A runtime rollback was not exercised.

## Remaining work
Live Rhino acceptance and any subsequent requested deployment. Do not treat build output as an installed or host-validated release.

## Conclusion
Feature implemented and builds validated in both configurations. Live visual acceptance pending.

## Packaging follow-up (2026-09-16)
The user closed Rhino to proceed with deployment. No Rhino process was found. Bumped the package manifest from 1.0.73 to 1.0.74 and built a fresh bundle at `Packaging/PanelCladdingEditor/artifacts/viewport-highlight-1.0.74/PanelCladdingEditor-1.0.74`. Publish and direct Release rebuild succeeded without warnings/errors.

Verified the compiled assembly GUID before packaging and ran `Project_Test/260805_TEST_rhino-plugin-assembly-identity/Verify-PluginAssemblyIdentity.ps1 -SkipBuild -PluginPath <MCP Release RHP>,<packaged editor RHP>` afterward. Both product identities passed and were distinct. Editor GUID remains `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`.

Production activation was not attempted: host-persistent registry access is not independently established in this agent context, and AGENTS.md requires staging only in that case. The installed 1.0.73 product remains untouched. The staged installer is ready for ordinary user PowerShell Install and Validate. Independent host registration/timestamp attestation and post-start loaded-RHP verification remain required before reporting installation fully validated.

## Missing-preview investigation (2026-09-16)
The user subsequently reported no viewport preview with PCEditor open and a clearly visible panel loaded. Process module inspection confirmed Rhino PID 38696 loaded the installed 1.0.74 RHP. Its SHA-256 matches the staged RHP: `4686D35EA12274D27E4B4246BAE58142403CDCAA203C2D5D26A380E504C8B60C`. This confirms the new binary is loaded, but does not establish complete registry timestamp attestation or visual success.

The Router twice reported only an unavailable unsaved/untitled document, preventing live document inspection through the existing tools. Source inspection confirmed layout runtime serial population and successful-load wiring. Depth occlusion is only a hypothesis, not a confirmed cause; the user says the original panel is clearly visible.

Added `ProbeViewportHighlight.py` under the matching TEST folder for the user to run inside Rhino. It records actual editor/conduit target and visibility state, object eligibility, and display callback document identity/errors, without changing document objects. It writes `%LOCALAPPDATA%/PanelCladdingEditor/diagnostics/viewport-highlight.txt`. Python syntax check passed. Native diagnostic execution and root-cause determination remain pending. No speculative code change or replacement package was made.

## Drawing correction (2026-09-16, package 1.0.75)
The user ran the probe. Its output confirms a normal visible editor, matching layout/conduit/document serials and object IDs, an enabled and undisposed conduit, an existing visible unselected Brep, viewport eligibility, and one successful Perspective draw callback. There was no callback exception. This rules out the observed target/visibility/lifetime gates as the cause in that frame.

Local installed RhinoCommon XML exposed a concrete API misuse: the third argument of `DrawBrepWires(Brep, Color, int)` is **wireDensity**, not thickness. The earlier description of 6/3-pixel lines was incorrect. Both drawing calls therefore used thin wires at different densities while competing with model depth; this could leave the intended identification outline indistinguishable. Exact pixel-level disappearance was not captured.

Replaced the two Brep-wire calls with two passes over actual trimmed Brep edges, using `DrawCurve(Curve, Color, int thickness)` for a six-pixel dark halo and three-pixel cyan stroke. Moved the drawing override to `DrawForeground`, where Rhino disables depth testing/writing, to keep the identification outline legible on coincident or covering geometry. Document/object/viewport and editor lifecycle guards remain intact. This is a drawing-strategy deviation from the original depth-tested implementation, without changing document state.

Validation:
- Debug and Release standalone editor builds passed with zero warnings/errors.
- `Test-ViewportHighlightDrawing.ps1` passed against installed RhinoCommon XML; it rejects the incorrect overload and the old depth-tested drawing stage. This is source/API regression coverage, not a rendered visual test.
- Compiled assembly GUID/cross-product uniqueness gate passed before packaging.
- Release package 1.0.75 publish and rebuild passed; packaged and checked Release RHP hashes match.
- `git diff --check` passed with line-ending notices only.

Bundle: `Packaging/PanelCladdingEditor/artifacts/viewport-highlight-1.0.75/PanelCladdingEditor-1.0.75`. No production activation was performed. The loaded 1.0.74 installation remains unchanged. User-host installation/Validate and live visual acceptance of 1.0.75 remain pending under AGENTS.md's registry-attestation rule.
