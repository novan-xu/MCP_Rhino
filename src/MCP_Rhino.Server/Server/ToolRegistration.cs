using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Server;

public static class ToolRegistration
{
    public static IMcpServerBuilder AddRhinoTools(this IMcpServerBuilder builder)
    {
        return builder.WithToolsFromAssembly(typeof(ToolRegistration).Assembly);
    }
}