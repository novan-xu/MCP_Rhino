using MCP_Rhino.Router.Routing;
using MCP_Rhino.Transport.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

if (args.Length > 0)
{
    return RunCommand(args);
}

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton<RouteRegistry>();
builder.Services.AddSingleton<RouteDiscovery>();
builder.Services.AddSingleton<BackendCatalog>();
builder.Services.AddSingleton<RouterRuntime>();
builder.Services.AddHostedService<RouterCatalogMonitor>();

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation
        {
            Name = "MCP_Rhino.Router",
            Version = typeof(RouterRuntime).Assembly.GetName().Version?.ToString() ?? "0.0.0.0"
        };
        options.Capabilities = new ServerCapabilities
        {
            Tools = new ToolsCapability { ListChanged = true },
            Resources = new ResourcesCapability
            {
                ListChanged = false,
                Subscribe = false
            }
        };
        options.ServerInstructions = "Use rhino_router_list_documents and rhino_router_select_document to choose an exact saved live Rhino document before calling proxied Rhino tools.";
    })
    .WithListToolsHandler((request, cancellationToken) =>
        GetRuntime(request.Services).ListToolsAsync(request, cancellationToken))
    .WithCallToolHandler((request, cancellationToken) =>
        GetRuntime(request.Services).CallToolAsync(request, cancellationToken))
    .WithListResourcesHandler((request, cancellationToken) =>
        GetRuntime(request.Services).ListResourcesAsync(request, cancellationToken))
    .WithListResourceTemplatesHandler((request, cancellationToken) =>
        GetRuntime(request.Services).ListResourceTemplatesAsync(request, cancellationToken))
    .WithReadResourceHandler((request, cancellationToken) =>
        GetRuntime(request.Services).ReadResourceAsync(request, cancellationToken))
    .WithStdioServerTransport();

await builder.Build().RunAsync().ConfigureAwait(false);
return 0;

static RouterRuntime GetRuntime(IServiceProvider? services)
{
    return (services ?? throw new InvalidOperationException("The MCP request service provider is unavailable."))
        .GetRequiredService<RouterRuntime>();
}

static int RunCommand(string[] commandArgs)
{
    if (commandArgs.Length == 1
        && (string.Equals(commandArgs[0], "--help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(commandArgs[0], "-h", StringComparison.OrdinalIgnoreCase)))
    {
        Console.WriteLine("Usage: MCP_Rhino.Router.exe [--validate-install] [--help]");
        Console.WriteLine("With no arguments, runs one MCP stdio Router session for the launching agent.");
        return 0;
    }

    if (commandArgs.Length == 1
        && string.Equals(commandArgs[0], "--validate-install", StringComparison.OrdinalIgnoreCase))
    {
        string executableDirectory = AppContext.BaseDirectory;
        string transportPath = Path.Combine(executableDirectory, "MCP_Rhino.Transport.dll");
        if (!File.Exists(transportPath))
        {
            Console.Error.WriteLine($"Missing required transport assembly: {transportPath}");
            return 1;
        }

        var registry = new RouteRegistry();
        Directory.CreateDirectory(registry.DirectoryPath);
        Console.WriteLine($"MCP_Rhino Router installation is valid. Registry: {registry.DirectoryPath}");
        return 0;
    }

    Console.Error.WriteLine("Unknown Router argument. Use --help.");
    return 2;
}
