using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Server;

public static class ResourceRegistration
{
    public static IMcpServerBuilder AddRhinoResources(this IMcpServerBuilder builder)
    {
        return builder.WithResourcesFromAssembly(typeof(ResourceRegistration).Assembly);
    }
}
