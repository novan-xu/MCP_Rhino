namespace MCP_Rhino.Companion;

public static class CompanionAttachmentValidator
{
    public const int MaxAttachmentCount = 4;
    public const long MaxAttachmentBytes = 10L * 1024L * 1024L;
    public const long MaxTotalAttachmentBytes = 20L * 1024L * 1024L;

    private static readonly HashSet<string> AllowedImageMediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/webp"
    };

    public static bool TryValidate(CompanionUserMessage message, out string error)
    {
        error = string.Empty;

        if (!message.HasContent)
        {
            error = "Message text or an image attachment is required.";
            return false;
        }

        if (message.Attachments.Count > MaxAttachmentCount)
        {
            error = $"At most {MaxAttachmentCount} image attachments are allowed per message.";
            return false;
        }

        long totalBytes = 0L;
        foreach (CompanionAttachment attachment in message.Attachments)
        {
            if (!string.Equals(attachment.Kind, CompanionAttachment.ImageKind, StringComparison.OrdinalIgnoreCase))
            {
                error = $"Only image attachments are supported: {attachment.Name}.";
                return false;
            }

            if (!AllowedImageMediaTypes.Contains(attachment.MediaType))
            {
                error = $"Unsupported image type for {attachment.Name}: {attachment.MediaType}. Use PNG, JPEG, or WebP.";
                return false;
            }

            if (attachment.SizeBytes <= 0)
            {
                error = $"Attachment {attachment.Name} is empty.";
                return false;
            }

            if (attachment.SizeBytes > MaxAttachmentBytes)
            {
                error = $"Attachment {attachment.Name} exceeds the {FormatBytes(MaxAttachmentBytes)} per-image limit.";
                return false;
            }

            totalBytes += attachment.SizeBytes;
            if (totalBytes > MaxTotalAttachmentBytes)
            {
                error = $"Attachments exceed the {FormatBytes(MaxTotalAttachmentBytes)} per-message limit.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(attachment.Base64Data))
            {
                error = $"Attachment {attachment.Name} did not include image data.";
                return false;
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(attachment.Base64Data);
            }
            catch (FormatException)
            {
                error = $"Attachment {attachment.Name} contains invalid base64 image data.";
                return false;
            }

            if (bytes.LongLength != attachment.SizeBytes)
            {
                error = $"Attachment {attachment.Name} byte length did not match its declared size.";
                return false;
            }
        }

        return true;
    }

    public static bool IsAllowedImageMediaType(string mediaType)
    {
        return AllowedImageMediaTypes.Contains(mediaType);
    }

    private static string FormatBytes(long value)
    {
        return value >= 1024L * 1024L
            ? $"{value / 1024d / 1024d:0.#} MB"
            : $"{Math.Max(1L, value / 1024L)} KB";
    }
}
