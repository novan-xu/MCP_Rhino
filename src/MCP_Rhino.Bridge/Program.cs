using System.IO.Pipes;

const string PipeName = "mcp_rhino";
const int ConnectTimeoutMs = 5000;

using var pipe = new NamedPipeClientStream(
    ".",
    PipeName,
    PipeDirection.InOut,
    PipeOptions.Asynchronous);

try
{
    await pipe.ConnectAsync(ConnectTimeoutMs);
}
catch (TimeoutException)
{
    await Console.Error.WriteLineAsync(
        $"Failed to connect to \\\\.\\pipe\\{PipeName} within {ConnectTimeoutMs} ms. Start Rhino and load the MCP_Rhino plugin first.");
    return 1;
}
catch (IOException ex)
{
    await Console.Error.WriteLineAsync(
        $"Failed to connect to \\\\.\\pipe\\{PipeName}: {ex.Message}. Start Rhino and load the MCP_Rhino plugin first.");
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
