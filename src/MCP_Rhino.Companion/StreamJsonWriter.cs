using System.IO;
using System.Text.Json;

namespace MCP_Rhino.Companion;

public static class StreamJsonWriter
{
    public static async Task WriteUserMessageAsync(
        TextWriter writer,
        CompanionUserMessage message,
        CancellationToken cancellationToken)
    {
        var content = new List<object>();
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            content.Add(new
            {
                type = "text",
                text = message.Text
            });
        }

        foreach (CompanionAttachment attachment in message.Attachments)
        {
            content.Add(new
            {
                type = "image",
                source = new
                {
                    type = "base64",
                    media_type = attachment.MediaType,
                    data = attachment.Base64Data
                }
            });
        }

        var payload = new
        {
            type = "user",
            message = new
            {
                role = "user",
                content
            }
        };

        string line = JsonSerializer.Serialize(payload);
        await writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
