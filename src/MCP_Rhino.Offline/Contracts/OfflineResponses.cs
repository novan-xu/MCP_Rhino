namespace MCP_Rhino.Offline.Contracts;

public sealed record OfflinePreviewResponse(string PreviewToken, string SourcePath, string SourceSha256,
    DateTimeOffset ExpiresUtc, int ObjectCountBefore, int ObjectCountAfter, int MutationCount,
    IReadOnlyList<string> Log, string OutputNaming);

public sealed record OfflineApplyResponse(string SourcePath, string OutputPath, string SourceSha256,
    string OutputSha256, long OutputBytes, bool SourceUnchanged = true);

public sealed class OfflineEditException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
