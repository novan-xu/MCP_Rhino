# PC editor canvas assignment PLAN

## Background

The user explicitly requested these editor changes on 2026-10-07: separate width
and height with five decimals, display the shortened CID, and move extrusion
assignment from the sidebar to a floating canvas panel shown on curve selection.
This request authorizes the construction work and its required PLAN/EXET/TEST chain.

## Goals

- Show labeled width and height on separate lines, each with five decimals and units.
- Read the panel's CID metadata and shorten it using the existing CID formatter.
- Show selected curves and their assigned profile cards in a floating canvas panel.
- Keep the sidebar catalogue, assignment actions, Undo, and save behavior working.

## Architecture ownership

This is standalone PanelCladdingEditor WPF presentation work in `UI/`. Reuse the
application CID formatter. No Rhino adapter, MCP surface, host, transport, or
document persistence contract changes are required.

## Key design

- Prefer stored `CW_1.02_CID`; derive a CID from PID/unit-type metadata for legacy
  panels without a CID. Show an em dash if neither identifier exists.
- Place the assignment panel inside CanvasWorkspace at the right below the size
  readout, with a bounded scrolling card list and selection-specific heading.
- Show it only in Extrusion view with valid selected curves. Blank selection,
  Escape, or changing view hides it; multi-selection retains existing semantics.
- Retain the existing drag format and assignment/modifier/remove/clear handlers.
- Use the existing in-process WPF test-host pattern and offscreen rendering; no
  Windows UI automation, live Rhino edits, or installation is required.

## Files involved

- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml.cs`
- `Project_Test/261007_TEST_pc-editor-canvas-assignment/`
- Matching PLAN and EXET records.

## Usage

Open PCEditor, switch to Extrusion view, and click a frame or intermediate curve.
Drag a sidebar profile into the floating assignment panel. Review/edit assigned
profiles there; use the existing save actions to persist changes.

## Acceptance criteria

Verify precise two-line dimensions; CID priority and corner suffixes; hidden and
visible panel states across selection, views, and panel reload; successful profile
drop and assignment updates; clear/remove/modifier and Undo behavior; constrained
scrolling and visible controls at normal/minimum editor sizes. Build the standalone
product in Debug and Release and run relevant existing assignment regressions.

## Risks and rollback

The overlay may cover part of a zoomed drawing, as other canvas overlays do. Keep
it narrow and dismissible through selection clearing. Scrolling must not zoom the
underlying canvas. Source rollback reverts the UI changes and matched artifacts;
no installed RHP, registry entry, or Rhino document is changed.

## Future extensions

Movable or dockable placement can be considered after feedback on this interaction.

## Revision (2026-10-07): two-column profile cards

The user requested a denser assignment display after reviewing the installed
single-column panel. Keep its canvas footprint and use two equal card columns.
Put each profile code on its own line and place modifier/remove controls beneath
it, so narrowing the cards does not squeeze the identifiers. Reuse the existing
offscreen rendering/interaction smoke at normal and minimum window sizes. Record
the follow-up in the same EXET and TEST artifacts.
