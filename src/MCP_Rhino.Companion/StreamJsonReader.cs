using System.IO;

namespace MCP_Rhino.Companion;

public static class StreamJsonReader
{
    public static async Task ReadEventsAsync(
        TextReader reader,
        Action<ClaudeCodeStreamEvent> onEvent,
        Action<string> onInvalidLine,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (ClaudeCodeStreamEvent.TryParse(line, out ClaudeCodeStreamEvent streamEvent))
            {
                onEvent(streamEvent);
            }
            else
            {
                onInvalidLine(line);
            }
        }
    }
}
