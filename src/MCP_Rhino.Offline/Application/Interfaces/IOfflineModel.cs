namespace MCP_Rhino.Offline.Application.Interfaces;

public interface IOfflineModel : IDisposable
{
    string SourcePath { get; }
    string SourceSha256 { get; }
    int ObjectCount { get; }
    int MutationCount { get; }
    IReadOnlyList<string> Log { get; }
    object Inspect(int offset, int limit);
    void ExecuteScript(string script, CancellationToken cancellationToken);
    byte[] SerializeVerified();
}

public interface IOfflineFileStore
{
    IOfflineModel OpenReadOnly(string filePath);
    string PublishCopy(string sourcePath, byte[] bytes, CancellationToken cancellationToken);
}
