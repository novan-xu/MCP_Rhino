# Panel Cladding Latest UI Execution

## Corresponding Plan

- Plan: `Project_Plan/260813_PLAN_panel-cladding-latest-ui.md`
- Execution date: 2026-08-13
- Authoritative design: `Design/PanelCladdingEditor/Panel-Cladding-Editor.zip!/panel-cladding-editor.html`

## Related Artifacts

- Focused tests: `Project_Test/260813_TEST_panel-cladding-latest-ui/`
- Design-source audit: `Project_Test/260813_TEST_panel-cladding-latest-ui/design-source-audit.txt`
- Release bundle: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.24/`
- Installed root: `%LOCALAPPDATA%/PanelCladdingEditor/plugin/1.0.24/`
- Commit / PR: none created in this execution.

## Execution Result / Actual Delivered Scope

The stale `DESIGN-MANIFEST.json` entry was not used. Archive inspection established that
`panel-cladding-editor.html` is newer (2026-08-12 22:14:32, 116,167 bytes, 79 JavaScript functions,
27 buttons) than `panel-cladding-editor-v2.html` (2026-08-12 19:53:54, 66,061 bytes,
46 JavaScript functions, 19 buttons).

The standalone editor remains a WPF top-level desktop window owned by Rhino's main HWND. It contains
no Eto, Rhino.UI, HTML, WebView, or browser-host dependency. The following latest-design behaviors
were implemented:

- enabled toolbar-level **Extrusion view / Cladding view** switch;
- washed-out cladding context underneath the extrusion overlay;
- frame and atomic divider segment rendering with stable `FRM-*` / `INT-*` codes;
- click, modifier, blank-canvas, and marquee selection scoped to the active view;
- red extrusion selection, merge, explode, and intermediate-segment deletion;
- **Add H / Add V** cell-placement mode with crosshair preview and scoped five-decimal dialog;
- working-layout cell split/remap, affected assignment reset, parent-reference remap, and partial
  segment deletion outside a scoped placement cell;
- editable five-decimal row/column dimensions with lock toggles, minimum-size validation, total-size
  preservation, and unlocked-neighbor redistribution;
- full structural and value-state undo snapshots;
- current-panel divider-offset list and mode-specific extrusion-assignment placeholder card;
- existing material setup, material drag/drop, parent references, workbook selection, type preview,
  dirty protection, Rhino attribute save, and Excel synchronization retained;
- structural preview disclosure and save guard until a future Rhino geometry/apply contract exists.

The release package version advanced from `1.0.23` to `1.0.24` and was activated through the
repository-owned current-user registry installer while Rhino was closed.

## Deviation From Plan

The planned editor-session structural boundary was retained. Structural extrusion, mullion, and
dimension edits are fully interactive and reversible in the window, but are not written to Rhino
geometry or offset user text. **Save & sync** is disabled while such a preview is active and explains
that those edits must be undone before supported cell values can be committed. This is intentional:
the current `PanelCladdingSaveRequest` only accepts cell values and has no preview/apply contract for
geometry, divider offsets, synthetic cell keys, or segment topology.

No WebView or alternate UI framework was introduced. No live Rhino document or workbook was mutated
by the off-screen smoke.

## Problems Found And Fixed During Construction

1. The package manifest's design `entryFile` was stale and selected the older v2 export. Timestamp,
   size, function-count, and action-count evidence now records the actual latest file.
2. The first WPF version treated extrusion and dimensions as disabled placeholders. The latest
   source instead requires an enabled structural workspace, so the canvas and window state were
   extended rather than merely restyled.
3. Initial extrusion rendering removed the cladding colors completely. Visual comparison with the
   latest CSS (`opacity: .34`, increased saturation/brightness) showed that the cell context must
   remain visible; WPF now renders a lightened material context and ghost cell IDs.
4. Initial **Add H / Add V** handling opened a global offset dialog. The latest script uses a
   cell-placement mode first; WPF now toggles a crosshair/preview and opens a cell-relative dialog.
5. The numeric-dialog test rendered its transparent root against black. The dialog root now owns the
   intended light surface background, matching the real window and deterministic snapshot.
6. The server project compiles historical TEST sources. The new WPF console smoke was initially
   included in the non-Windows server build during identity verification; an exact TEST-folder
   exclusion was added beside the prior WPF-smoke exclusion. The subsequent Debug/Release identity
   verification passed.

## Test Record

### Latest-design focused smoke

Commands:

```powershell
dotnet run --project .\Project_Test\260813_TEST_panel-cladding-latest-ui\PanelCladdingLatestUiSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260813_TEST_panel-cladding-latest-ui\PanelCladdingLatestUiSmoke.csproj -c Release
```

Both configurations exited `0` and asserted:

- latest action model present;
- WPF `Window` base type and no Eto/WebView references;
- fixture topology contains four frames plus seventeen divider segments;
- **Add H** enters and exits cell-placement mode;
- merge, undo, and add-mullion session behaviors pass;
- desktop, compact, merged-selection, and mullion-dialog renders are non-empty.

Visual evidence:

- `panel-cladding-latest-cladding-1440x900.png` — 104,378 bytes
- `panel-cladding-latest-extrusion-1440x900.png` — 112,570 bytes
- `panel-cladding-latest-extrusion-1024x768.png` — 102,631 bytes
- `panel-cladding-latest-merged-1440x900.png` — 114,514 bytes
- `panel-cladding-latest-mullion-dialog-430x272.png` — 13,100 bytes

All five images were inspected. The cladding view matches the source grid/dimension hierarchy; the
extrusion view retains light material context, dark segment runs, blue segment labels, and red merged
selection; the compact view remains legible; and the dialog matches the latest light modal.

### Production builds

```powershell
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Debug
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Release
```

Both exited `0` with zero warnings and zero errors.

### Existing PanelCladdingEditor regressions

All eight prior project-reference smokes ran in Release and exited `0`:

- standalone editor core/workbook/preview;
- panel spawn;
- panel match;
- panel clear;
- panel surface sync;
- offset sync;
- cladding regions;
- prior WPF UI smoke.

Native Rhino-only probes in the offset/region smokes reported their expected `[SKIP]` outside a
running Rhino native host; all managed assertions passed.

### Assembly identity

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Debug
powershell -NoProfile -ExecutionPolicy Bypass -File .\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Release
```

After the exact WPF-smoke exclusion fix, both exited `0`. The compiled PanelCladdingEditor RHP
declares `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35` in Debug and Release, matching the plug-in class and
package manifest. The package probe also reported `declared=True` for the same GUID.

### Package and installation

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1 -Version 1.0.24
powershell -NoProfile -ExecutionPolicy Bypass -File <bundle>\Installer\Install-PanelCladdingEditor.ps1 -Mode Repair -BundleRoot <bundle>
powershell -NoProfile -ExecutionPolicy Bypass -File <bundle>\Installer\Install-PanelCladdingEditor.ps1 -Mode Validate -BundleRoot <bundle>
```

All exited `0`. The final packaged and installed RHP is 343,040 bytes with SHA-256
`5a2867533f27fe1c2b0815349a7f4e775c6978278e643a7aaa367534a0e8c917`. The package contains no
`PanelCladdingEditor.dll`, Eto, Rhino.UI, or WebView assembly.

Installer validation reported:

- version `1.0.24`;
- registry-only root `%LOCALAPPDATA%\PanelCladdingEditor\plugin\1.0.24`;
- `LoadMode=1` and `DirectoryInstall=0`;
- registry `FileName` points to the versioned `1.0.24` RHP;
- no Rhino Package Manager duplicate was found;
- the prior `1.0.23` payload was moved to installer-owned rollback storage.

## Acceptance Criteria Alignment

- Latest file selected by reproducible evidence: passed.
- Enabled extrusion workspace and latest action shelf: passed.
- Segment selection/merge/explode/add/delete and structural undo: passed.
- Cell-placement and five-decimal lock-aware dimensions: passed.
- Divider list and mode-specific sidebar: passed.
- Existing assignment/workbook behavior retained: passed managed regressions.
- WPF-only desktop window, no Eto/web host: passed source and assembly scans.
- Debug/Release builds, GUID, package, and installation: passed.
- Desktop/compact visual QA: passed.

## Rollback Verification

The installer transaction moved the prior installed `1.0.23` payload to
`%LOCALAPPDATA%\PanelCladdingEditor\rollback\785b6e3f522e4b8983547de825099578\prior-plugin-1.0.23`.
The current install can therefore be removed or repaired using the repository installer without
touching Rhino documents. Structural UI tests were off-screen and did not mutate a live document.

## Current Remaining Items

- Persisting extrusion topology, mullion geometry, divider offsets, and synthetic cell keys requires
  a future preview/apply application contract and one Rhino Undo record. It is deliberately not
  simulated by attribute-only writes.
- Extrusion profile and finish assignment remains the latest design's own “coming next” placeholder.
- A live Rhino launch check is still useful after the user restarts Rhino, but package installation,
  registry ownership, and command assembly identity are already validated.

## Conclusion

The installed `1.0.24` PanelCladdingEditor now follows the actual newest design file and reproduces
its cladding/extrusion window architecture in WPF. The interactive latest-design behaviors are
implemented within safe persistence boundaries, all managed regression/build/identity/package checks
pass, and the current-user installation is valid and uniquely registered.
