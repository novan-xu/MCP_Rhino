using System.Runtime.InteropServices;

// Rhino resolves a managed plug-in id from the assembly-level GuidAttribute
// (Rhino.PlugIns.PlugIn.Create), not from the [Guid] attribute on the plug-in class.
// Without this attribute Rhino assigns Guid.Empty, which collides with every other
// RHP that also omits it. Keep this value equal to the [Guid] on PanelCladdingEditorPlugin
// and to Packaging/PanelCladdingEditor/package-manifest.json "pluginId".
[assembly: Guid("7C1A4D3B-5E29-4F68-9A72-1D8C6B0F4E35")]
