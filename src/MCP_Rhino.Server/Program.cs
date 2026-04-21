using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MCP_Rhino.Server.Infrastructure.CLI;
using MCP_Rhino.Server.Server;

BootstrapRhinoRuntime();

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOfflineRhinoAdapters()
    .AddCliFallbackLiveRhinoAdapters()
    .AddRhinoApplication()
    .AddRhinoAgents();

using var host = builder.Build();

using IServiceScope scope = host.Services.CreateScope();
var handler = scope.ServiceProvider.GetRequiredService<DeveloperCommandHandler>();

if (handler.TryHandle(args))
{
    return;
}

Console.Error.WriteLine("CLI mode only supports explicit developer commands. Load the Rhino plugin and use MCP_Rhino.Bridge for MCP transport.");
Environment.ExitCode = 1;

static void BootstrapRhinoRuntime()
{
    const string RhinoSystemDirectory = @"C:\Program Files\Rhino 8\System";
    const string RhinoNetcoreDirectory = @"C:\Program Files\Rhino 8\System\netcore";

    string currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
    var segments = currentPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).ToList();

    if (!segments.Contains(RhinoSystemDirectory, StringComparer.OrdinalIgnoreCase))
    {
        segments.Insert(0, RhinoSystemDirectory);
    }

    if (!segments.Contains(RhinoNetcoreDirectory, StringComparer.OrdinalIgnoreCase))
    {
        segments.Insert(0, RhinoNetcoreDirectory);
    }

    Environment.SetEnvironmentVariable("PATH", string.Join(Path.PathSeparator, segments));
}
