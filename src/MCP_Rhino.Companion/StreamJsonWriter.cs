using System.IO;
using System.Text.Json;

namespace MCP_Rhino.Companion;

public static class StreamJsonWriter
{
    public static async Task WriteUserMessageAsync(TextWriter writer, string text, CancellationToken cancellationToken)
    {
        var payload = new
        {
            type = "user",
            message = new
            {
                role = "user",
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text
                    }
                }
            }
        };

        string line = JsonSerializer.Serialize(payload);
        await writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
