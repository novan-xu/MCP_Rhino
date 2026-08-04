using Microsoft.Extensions.DependencyInjection;
using MCP_Rhino.Server.Infrastructure.Runtime;
using MCP_Rhino.Server.Server;

namespace MCP_Rhino.Server.Infrastructure.Plugin;

internal static class PluginServiceRegistration
{
    public static void Configure(IServiceCollection services)
    {
        services
            .AddLiveRhinoAdapters()
            .AddRhinoApplication()
            .AddRhinoAgents();
    }
}
