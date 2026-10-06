# PC editor offset layout preservation PLAN

## Background

The user explicitly requests that changing H/V values preserve the saved extrusion
layout and cladding assignments. Both UpdateDividerOffset and UpdateDimension
currently clear the editor's merge, delete, and hide state. Editor atom IDs contain
physical offsets, while persisted masks identify atoms by axis/track/bay. Simply
retaining old IDs would also lose state when their coordinates move.

## Goals

- H/V numeric edits move the existing indexed tracks without changing their masks.
- Row/column dimension edits preserve the same layout when redistributing offsets.
- Preserve material/parent assignments and extrusion profile assignments.
- Retain validation, total-size/lock constraints, undo, and save/reload behavior.

## Architecture ownership

This is editor state handling in PanelCladdingEditorWindow. Existing domain mask
coordinates are the topology authority. Use CaptureTopologyState before offset
changes and ApplyTopologyState afterward to recreate coordinate-based display IDs.
No new service, persistence schema, live repository, or MCP/Router change is needed.

## Key design

Capture current indexed topology after edit validation and before changing H/V
arrays. Share a small editor method that clears stale display IDs, reapplies the
captured topology using updated coordinates, clears obsolete extrusion selection,
rebuilds the working layout, and marks the edit for saving. Keep cell values and
index-based frame assignments unchanged. Existing insertion/removal workflows keep
their explicit topology remapping; this fix covers numeric edits with stable track
counts/order, not adding/deleting a track.

## Files

- src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml.cs
- Project_Test/260818_TEST_panel-cladding-topology-persistence/Program.cs
- Packaging/PanelCladdingEditor/README.md and package-manifest.json
- Matching 261006 PLAN, EXET, and TEST records for pc-editor-offset-layout.

## Usage

Edit H/V offsets or unlocked row/column dimensions in PCEditor, then Save Extrusions
or Save Both. Deleted/hidden/merged segments and material/parent assignments retain
their indexed layout while their coordinates change.

## Acceptance criteria

- A persisted fixture with H/V delete, hide, and merge masks retains byte-identical
  masks after direct H edits, direct V edits, row edits, and column edits.
- UI-state edits preserve cladding materials/parents and frame profile assignments.
- Undo restores coordinates and the same topology/assignments.
- Save Both and Save Extrusions followed by Save Cladding retain masks and cladding
  attributes through repository parsing and editor reload.
- Invalid/no-op offsets do not alter masks, assignments, or undo history.
- Topology persistence, scoped-save, hide-mask, and relevant prior PCCreate
  regressions pass. In-process tests use unshown WPF objects and a fake repository;
  they do not automate Windows or control a live Rhino session.
- Standalone Debug and Release builds pass. Narrow product builds are appropriate
  because the MCP host/Router/tool surface is unchanged.
- Verify RHP assembly identity, build/stage version 1.0.82 containing all three
  fixes in this chat, and verify bundle hashes; no production activation.

## Risks and rollback

The snapshot must precede coordinate mutation, or old display IDs cannot resolve
to their original tracks. Preserve in-progress topology edits as well as saved
masks. Revert only this editor change and its tests to roll back, preserving the
prior PCCreate fixes. Native Rhino command/Undo validation remains separate.

## Future extensions

None required. Adding/removing/reordering tracks remains governed by the existing
explicit topology-edit operations.
