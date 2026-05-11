using System.Text.Json;

namespace MCP_Rhino.Companion;

public sealed record CompanionUserMessage(
    string Text,
    IReadOnlyList<CompanionAttachment> Attachments)
{
    public bool HasContent =>
        !string.IsNullOrWhiteSpace(Text) || Attachments.Count > 0;

    public IReadOnlyList<CompanionAttachmentSummary> AttachmentSummaries =>
        Attachments.Select(attachment => attachment.ToSummary()).ToArray();

    public static bool TryReadFromJson(
        JsonElement root,
        out CompanionUserMessage message,
        out string error)
    {
        error = string.Empty;
        string text = ReadString(root, "text") ?? string.Empty;
        var attachments = new List<CompanionAttachment>();

        if (root.TryGetProperty("attachments", out JsonElement attachmentsElement))
        {
            if (attachmentsElement.ValueKind != JsonValueKind.Array)
            {
                message = new CompanionUserMessage(text, Array.Empty<CompanionAttachment>());
                error = "The attachments payload must be an array.";
                return false;
            }

            int index = 0;
            foreach (JsonElement attachmentElement in attachmentsElement.EnumerateArray())
            {
                index++;
                if (attachmentElement.ValueKind != JsonValueKind.Object)
                {
                    message = new CompanionUserMessage(text, Array.Empty<CompanionAttachment>());
                    error = $"Attachment {index} must be an object.";
                    return false;
                }

                string id = ReadString(attachmentElement, "id") ?? Guid.NewGuid().ToString("N");
                string name = ReadString(attachmentElement, "name") ?? $"image-{index}";
                string mediaType = ReadString(attachmentElement, "mediaType") ?? string.Empty;
                string kind = ReadString(attachmentElement, "kind") ?? CompanionAttachment.ImageKind;
                long sizeBytes = ReadInt64(attachmentElement, "sizeBytes");
                string base64Data = ReadString(attachmentElement, "base64Data") ?? string.Empty;

                attachments.Add(new CompanionAttachment(
                    id,
                    name,
                    mediaType,
                    kind,
                    sizeBytes,
                    base64Data));
            }
        }

        message = new CompanionUserMessage(text, attachments);
        return CompanionAttachmentValidator.TryValidate(message, out error);
    }

    public string ToHistoryText()
    {
        if (Attachments.Count == 0)
        {
            return Text;
        }

        var builder = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(Text))
        {
            builder.AppendLine(Text);
            builder.AppendLine();
        }

        builder.AppendLine("[Attached images supplied with this turn]");
        foreach (CompanionAttachment attachment in Attachments)
        {
            builder
                .Append("- ")
                .Append(attachment.Name)
                .Append(" (")
                .Append(attachment.MediaType)
                .Append(", ")
                .Append(attachment.SizeBytes)
                .AppendLine(" bytes)");
        }

        return builder.ToString().TrimEnd();
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static long ReadInt64(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return 0L;
        }

        if (property.ValueKind == JsonValueKind.Number
            && property.TryGetInt64(out long value))
        {
            return value;
        }

        if (property.ValueKind == JsonValueKind.String
            && long.TryParse(property.GetString(), out long stringValue))
        {
            return stringValue;
        }

        return 0L;
    }
}
