using System.Reflection;
using MCP_Rhino.Offline.Application;
using MCP_Rhino.Offline.Tools;
using MCP_Rhino.Offline.Infrastructure;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Offline.Server;

public static class OfflineToolRegistration
{
    public const string ReservedPrefix = "rhino_offline_";

    public static IReadOnlyDictionary<string, McpServerTool> CreateTools()
    {
        object[] instances = [new OfflineScriptTool(new OfflineScriptService(new OfflineFileStore())),
            new OfflineRhinoScriptTool(new OfflineRhinoScriptService(new OfflineRhinoStore(), new RhinoWorkerProcess()))];
        var owners = instances.ToDictionary(instance => instance.GetType());
        // Method attributes are the source for names, schemas and safety metadata, not a second catalog.
        return typeof(OfflineToolRegistration).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(type => type.GetMethods())
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .Select(method => McpServerTool.Create(method, owners[method.DeclaringType!]))
            .ToDictionary(tool => tool.ProtocolTool.Name, StringComparer.Ordinal);
    }
}
