# Panel Cladding Canvas Notification

## Background

The transient notification is currently a child of the full window root, so its horizontal center is
calculated across both the canvas and the right sidebar. At the bottom of the window it also competes
with the extrusion action buttons.

## Goal

Anchor the notification to the canvas workspace, center it on the canvas independently of panel pan or
zoom, and reserve a fixed gap above the extrusion action buttons.

## Architecture Ownership

- `UI/PanelCladdingEditorWindow.xaml`: notification ownership and fixed canvas-relative placement.
- `Project_Test/260813_TEST_panel-cladding-canvas-notification/`: capability test record delegating
  the shared layout and render regression coverage to the canvas-alignment smoke.

## Key Design

1. Name the canvas workspace and move `ToastBorder` inside that grid.
2. Keep `HorizontalAlignment="Center"` so the toast center is always the canvas center, not the full
   editor or panel center.
3. Use a fixed bottom clearance above the extrusion action band. Do not derive notification placement
   from panel bounds, pan offset, or zoom.
4. Keep the existing timer, content, visual style, and transient behavior unchanged.

## Files Involved

- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml`
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`
- `Packaging/PanelCladdingEditor/package-manifest.json`
- `Project_Test/260813_TEST_panel-cladding-canvas-alignment/Program.cs`
- `Project_Test/260813_TEST_panel-cladding-canvas-notification/README.md`
- `Project_Plan/260813_PLAN_panel-cladding-canvas-notification.md`
- `Project_Exet/260813_EXET_panel-cladding-canvas-notification.md`

## Acceptance Criteria

- The notification is a child of the canvas workspace.
- Its horizontal midpoint equals the canvas midpoint at desktop and compact sizes.
- Its horizontal position does not change when the panel is panned or zoomed.
- Its bottom edge remains above the extrusion action buttons with a visible gap.
- Existing notification text, timing, rounded style, and other UI behavior remain unchanged.
- Focused Debug/Release tests, all panel-cladding regressions, builds, visual QA, identity checks, and
  packaging pass.

## Risks And Rollback

- Long messages may exceed narrow canvas width. Preserve the existing no-wrap presentation and verify
  compact layout with the longest current notification.
- Roll back by reinstalling PanelCladdingEditor 1.0.31; no Rhino or workbook data is migrated.

## Future Extensions

- A bounded max-width and multiline notification treatment for future longer messages.
