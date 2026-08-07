using System.Runtime.InteropServices;

// Rhino resolves a managed plug-in id from the assembly-level GuidAttribute
// (Rhino.PlugIns.PlugIn.Create), not from the [Guid] attribute on the plug-in class.
// Without this attribute Rhino assigns Guid.Empty, which collides with every other
// RHP that also omits it. Keep this value equal to McpRhinoPlugin.PluginIdText and to
// Packaging/MCP_Rhino/package-manifest.json "pluginId".
[assembly: Guid("7A3FC2F0-24A8-4B79-BE58-5A08CFB0D10A")]
