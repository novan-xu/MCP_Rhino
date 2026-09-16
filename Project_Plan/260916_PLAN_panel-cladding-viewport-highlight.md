# Panel cladding viewport highlight

## Background
The user requested a Grasshopper-like viewport indication of the panel currently being edited in PCEditor. This is an explicitly requested capability construction task.

## Goals
Show a cyan outline around the loaded panel while the editor is visible and not minimized. Keep the outline during Rhino navigation; clear it on editor close, hide, or minimize.

## Architecture ownership
An application interface exposes target/visibility/lifetime operations. A Rhino infrastructure display conduit implements the temporary overlay. The editor window supplies its successfully loaded layout identity and visibility; PCEditor composes the adapter.

## Key design
- Bind by document runtime serial and object GUID, never foreground document or file path.
- Resolve the current Brep during drawing so replacements, deletion, undo, and visibility changes do not leave cached geometry.
- Draw a contrasting cyan wire outline without modifying selection, attributes, geometry, or Undo.
- A failed/cancelled panel load retains the previous target.
- Disabling, retargeting, and disposing redraw the affected document(s).

## Files
Application/Interfaces/IPanelViewportHighlight.cs, Infrastructure/Rhino/LivePanelViewportHighlight.cs, UI/PanelCladdingEditorWindow.xaml.cs, and UI/PanelCladdingEditorCommand.cs under src/PanelCladdingEditor.

## Usage
Run PCEditor and select a panel. Its viewport outline follows the panel loaded in the editor automatically.

## Acceptance
Debug and Release standalone editor builds pass. Live acceptance covers panel switching, failed/cancelled loads, minimize/restore/close, Rhino navigation, multiple documents, hide/delete/undo, and unchanged selection/document state.

## Risks and rollback
Viewport appearance requires live Rhino visual acceptance. Revert the interface, adapter, and editor wiring to remove the feature. No installation or registry change is required for source implementation.

## Future extensions
Configurable preview color or filled shading can be added separately if requested.
