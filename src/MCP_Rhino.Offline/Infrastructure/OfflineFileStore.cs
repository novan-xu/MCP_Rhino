using MCP_Rhino.Offline.Application.Interfaces;

namespace MCP_Rhino.Offline.Infrastructure;

public sealed class OfflineFileStore : IOfflineFileStore
{
    public IOfflineModel OpenReadOnly(string filePath) => new Document(filePath);
    public string PublishCopy(string sourcePath, byte[] bytes, CancellationToken cancellationToken) =>
        OfflineCopyWriter.Publish(sourcePath, bytes, cancellationToken);

    private sealed class Document : IOfflineModel
    {
        private readonly OfflineSource _source;
        private readonly OfflineModelBridge _bridge;
        public Document(string filePath) { _source = new(filePath); _bridge = new(_source.Model); }
        public string SourcePath => _source.Path;
        public string SourceSha256 => _source.Sha256;
        public int ObjectCount => _source.Model.Objects.Count;
        public int MutationCount => _bridge.MutationCount;
        public IReadOnlyList<string> Log => _bridge.Log;
        public object Inspect(int offset, int limit) => _bridge.Inspect(offset, limit);
        public void ExecuteScript(string script, CancellationToken cancellationToken) => OfflineScriptRunner.Run(script, _bridge, cancellationToken);
        public byte[] SerializeVerified() => _bridge.SerializeVerified();
        public void Dispose() => _source.Dispose();
    }
}
