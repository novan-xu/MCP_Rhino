#if MCP_RHINO_ISOLATED_RUNTIME
namespace MCP_Rhino.Server.Infrastructure.Plugin;

// Historical developer smoke handlers use McpRhinoPlugin.Instance solely to
// distinguish Rhino execution from CLI execution. The isolated runtime must
// keep that source contract without defining a Rhino PlugIn subclass or GUID.
internal sealed class McpRhinoPlugin
{
    public static McpRhinoPlugin? Instance => null;
}
#endif
