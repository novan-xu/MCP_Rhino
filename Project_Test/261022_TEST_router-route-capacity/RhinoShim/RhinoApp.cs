namespace Rhino;

public static class RhinoApp
{
    private static int _connectedLines;

    // Counts the plug-in's "pipe client connected" log lines, as a live check would read them.
    public static int ConnectedLineCount => Volatile.Read(ref _connectedLines);

    public static void WriteLine(string message)
    {
        if (message.StartsWith("MCP_Rhino pipe client connected", StringComparison.Ordinal))
        {
            Interlocked.Increment(ref _connectedLines);
        }
    }
}
