# Panel viewport highlight validation

## Build verification (2026-09-16)

Run from the repository root:

```powershell
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Debug --no-restore
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Release --no-restore
git diff --check
```

Both configurations passed with zero warnings and zero errors. Diff whitespace check passed (Git reports only existing CRLF normalization policy). Narrow builds are appropriate: this feature changes only the standalone editor and introduces no MCP host, Router, registration, or tool changes.

## Code review checks

- A successfully completed LoadPanel sets the document runtime serial and object GUID; both load failure and dirty-discard cancellation return before retargeting.
- Hide/minimize clears Enabled and redraws; show/restore reenables it. Foreground focus loss does not clear feedback during Rhino navigation.
- OnClosed disposes; cancelled Closing does not dispose. Disposal is idempotent and prevents subsequent reactivation.
- The callback rejects other documents and resolves live object geometry, checking deletion, visibility, and viewport space. No copied Brep remains after deletion or replacement.
- No selection, attributes, document geometry, or Undo APIs are mutated by the highlight adapter.

## Live Rhino acceptance — pending

No Rhino UI automation was performed. Run these checks after loading the new build in a test Rhino session:

1. Open PCEditor for panel A: check the cyan outline in Wireframe, Shaded, and Rendered, including against dark/light backgrounds and while A is selected.
2. Navigate Rhino while the editor remains open: outline persists. In corrected 1.0.75 it renders in the foreground over coincident or occluding objects, with genuine 6/3-pixel halo/cyan strokes.
3. Load panel B: only B remains outlined. Cancel a panel switch with unsaved edits, then attempt an invalid panel load: A/B's prior successful target remains unchanged as appropriate.
4. Minimize/restore and hide/show the editor: outline clears/returns. Cancel closing with unsaved edits: outline remains. Actually close: outline clears. Reopen: one outline, no duplicate conduit.
5. Open another Rhino document: the overlay appears only in the original document. Save As retains targeting. Close the source document: no stale overlay or error.
6. Hide/delete the panel, turn its layer off, then undo/restore: no stale geometry; highlight returns when the same object is visible again. Replace/transform the panel: outline follows live geometry.
7. Check model view/page/detail isolation and viewport-specific layer visibility.
8. Compare selected object IDs, attributes, document modified state, and Undo before/after editor visibility changes: highlighting itself changes none of them.

The feature identifies the stored source panel; it does not preview unsaved editor subdivisions in Rhino. Installation and host registry validation have not been performed.

## Package follow-up

Version 1.0.74 packaged successfully at `Packaging/PanelCladdingEditor/artifacts/viewport-highlight-1.0.74/PanelCladdingEditor-1.0.74`. Release publish/rebuild and the existing compiled assembly GUID/cross-product uniqueness gate passed. Production install/Validate and independent host attestation remain pending; installed 1.0.73 was not modified.

## 1.0.75 correction verification

User-run `ProbeViewportHighlight.py` confirmed the loaded 1.0.74 conduit was enabled with correct target/document, visible eligible Brep, and a returning Perspective draw callback. The source/API inspection found that `DrawBrepWires`' integer controls wire density, not thickness. Earlier visual-width claims were incorrect.

Run `./Project_Test/260916_TEST_panel-cladding-viewport-highlight/Test-ViewportHighlightDrawing.ps1` from PowerShell. It passed against the installed RhinoCommon API docs and corrected source. Debug/Release editor builds, compiled GUID uniqueness gate, package publish/rebuild, and package/build hash equality also passed. The correction draws actual Brep edges with pixel-width DrawCurve calls in DrawForeground. Live appearance remains pending installation of staged version 1.0.75; no claim of successful rendered visual verification is made.
