using System.IO.Pipes;

const int ConnectTimeoutMs = 5000;

BridgeOptions? options = BridgeOptions.Parse(args);
if (options is null)
{
    BridgeOptions.WriteUsage(Console.Error);
    return 1;
}

if (options.ShowHelp)
{
    BridgeOptions.WriteUsage(Console.Error);
    return 0;
}

using var pipe = new NamedPipeClientStream(
    ".",
    options.PipeName,
    PipeDirection.InOut,
    PipeOptions.Asynchronous);

try
{
    await pipe.ConnectAsync(ConnectTimeoutMs);
}
catch (TimeoutException)
{
    await Console.Error.WriteLineAsync(
        $"Failed to connect to \\\\.\\pipe\\{options.PipeName} within {ConnectTimeoutMs} ms. Start Rhino and load the MCP_Rhino plugin first.");
    return 1;
}
catch (IOException ex)
{
    await Console.Error.WriteLineAsync(
        $"Failed to connect to \\\\.\\pipe\\{options.PipeName}: {ex.Message}. Start Rhino and load the MCP_Rhino plugin first.");
    return 1;
}

using Stream stdin = Console.OpenStandardInput();
using Stream stdout = Console.OpenStandardOutput();
using var shutdown = new CancellationTokenSource();

Task stdinToPipe = PumpAsync(stdin, pipe, shutdown.Token, completeDestination: true);
Task pipeToStdout = PumpAsync(pipe, stdout, shutdown.Token, completeDestination: false);

Task completed = await Task.WhenAny(stdinToPipe, pipeToStdout);
shutdown.Cancel();

await AwaitQuietly(stdinToPipe);
await AwaitQuietly(pipeToStdout);
await AwaitQuietly(completed);
return 0;

static async Task PumpAsync(Stream source, Stream destination, CancellationToken cancellationToken, bool completeDestination)
{
    try
    {
        await source.CopyToAsync(destination, cancellationToken);
        await destination.FlushAsync(cancellationToken);

        if (completeDestination)
        {
            destination.Close();
        }
    }
    catch (OperationCanceledException)
    {
    }
    catch (IOException)
    {
    }
}

static async Task AwaitQuietly(Task task)
{
    try
    {
        await task;
    }
    catch (OperationCanceledException)
    {
    }
    catch (IOException)
    {
    }
}

internal sealed record BridgeOptions(string PipeName, bool ShowHelp)
{
    private const string DefaultPipeName = "mcp_rhino";

    public static BridgeOptions? Parse(string[] args)
    {
        string pipeName = DefaultPipeName;
        bool showHelp = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase))
            {
                showHelp = true;
                continue;
            }

            if (string.Equals(arg, "--pipe", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
                {
                    return null;
                }

                pipeName = args[++i];
                continue;
            }

            return null;
        }

        return new BridgeOptions(pipeName, showHelp);
    }

    public static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Usage: MCP_Rhino.Bridge.exe [--pipe <name>] [--help]");
        writer.WriteLine();
        writer.WriteLine("Without --pipe, connects to \\\\.\\pipe\\mcp_rhino (Developer Debug Control Path).");
        writer.WriteLine("With --pipe mcp_rhino_<runtimeSerial>, connects to a panel-bound per-document server.");
    }
}
