using System.IO;

namespace MCP_Rhino.Companion;

internal sealed class CompanionAttachmentFileSet : IDisposable
{
    private readonly string? _directoryPath;
    private bool _disposed;

    private CompanionAttachmentFileSet(IReadOnlyList<string> imagePaths, string? directoryPath)
    {
        ImagePaths = imagePaths;
        _directoryPath = directoryPath;
    }

    public IReadOnlyList<string> ImagePaths { get; }

    public static CompanionAttachmentFileSet Empty { get; } =
        new CompanionAttachmentFileSet(Array.Empty<string>(), null);

    public static CompanionAttachmentFileSet WriteImages(
        CompanionUserMessage message,
        string workingDirectory)
    {
        if (message.Attachments.Count == 0)
        {
            return Empty;
        }

        string directoryPath = Path.Combine(
            workingDirectory,
            "attachments",
            DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);

        var paths = new List<string>();
        for (int i = 0; i < message.Attachments.Count; i++)
        {
            CompanionAttachment attachment = message.Attachments[i];
            string fileName = $"{i + 1:D2}_{SanitizeFileName(Path.GetFileNameWithoutExtension(attachment.Name))}{attachment.SuggestedExtension}";
            string path = Path.Combine(directoryPath, fileName);
            File.WriteAllBytes(path, Convert.FromBase64String(attachment.Base64Data));
            paths.Add(path);
        }

        return new CompanionAttachmentFileSet(paths, directoryPath);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (string.IsNullOrWhiteSpace(_directoryPath))
        {
            return;
        }

        try
        {
            if (Directory.Exists(_directoryPath))
            {
                Directory.Delete(_directoryPath, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string SanitizeFileName(string value)
    {
        string candidate = string.IsNullOrWhiteSpace(value) ? "image" : value.Trim();
        char[] invalid = Path.GetInvalidFileNameChars();
        string sanitized = string.Concat(candidate.Select(ch => invalid.Contains(ch) ? '_' : ch));
        return string.IsNullOrWhiteSpace(sanitized) ? "image" : sanitized;
    }
}
