namespace MCP_Rhino.Companion;

public sealed record CompanionAttachment(
    string Id,
    string Name,
    string MediaType,
    string Kind,
    long SizeBytes,
    string Base64Data)
{
    public const string ImageKind = "image";

    public CompanionAttachmentSummary ToSummary()
    {
        return new CompanionAttachmentSummary(Id, Name, MediaType, Kind, SizeBytes);
    }

    public string SuggestedExtension => MediaType.ToLowerInvariant() switch
    {
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/webp" => ".webp",
        _ => ".bin"
    };
}

public sealed record CompanionAttachmentSummary(
    string Id,
    string Name,
    string MediaType,
    string Kind,
    long SizeBytes);
