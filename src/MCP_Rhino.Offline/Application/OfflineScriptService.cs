using System.Security.Cryptography;
using MCP_Rhino.Offline.Contracts;
using MCP_Rhino.Offline.Application.Interfaces;

namespace MCP_Rhino.Offline.Application;

public sealed class OfflineScriptService(IOfflineFileStore store, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private sealed record Preview(string SourcePath, string SourceHash, byte[] Bytes, DateTimeOffset ExpiresUtc);
    private readonly Dictionary<string, Preview> _previews = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public object Inspect(string filePath, int offset = 0, int limit = 200)
    {
        using var source = store.OpenReadOnly(filePath);
        return new { sourcePath = source.SourcePath, sourceSha256 = source.SourceSha256, state = "saved-disk-only",
            document = source.Inspect(offset, limit) };
    }

    public OfflinePreviewResponse PreviewScript(string filePath, string script, CancellationToken cancellationToken = default)
    {
        // Serialize execution and bound retained results; Jint limits cannot account for native geometry memory.
        lock (_gate)
        {
            Expire();
            using var source = store.OpenReadOnly(filePath);
            int before = source.ObjectCount;
            source.ExecuteScript(script, cancellationToken);
            byte[] bytes = source.SerializeVerified();
            cancellationToken.ThrowIfCancellationRequested();
            if (bytes.Length > 268435456 || _previews.Values.Sum(x => (long)x.Bytes.Length) + bytes.Length > 536870912 || _previews.Count >= 4)
                throw new OfflineEditException("PREVIEW_CAPACITY", "At most four previews/512 MiB are retained for ten minutes. Apply an existing preview or wait for expiry.");
            string token = Guid.NewGuid().ToString("N");
            DateTimeOffset expires = _time.GetUtcNow().AddMinutes(10);
            _previews.Add(token, new(source.SourcePath, source.SourceSha256, bytes, expires));
            return new(token, source.SourcePath, source.SourceSha256, expires, before, source.ObjectCount,
                source.MutationCount, source.Log.ToArray(), "<source-stem>_<local-yyyyMMdd_HHmmss>.3dm");
        }
    }

    public OfflineApplyResponse ApplyScript(string filePath, string previewToken, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            Expire();
            if (!_previews.TryGetValue(previewToken, out Preview? preview))
                throw new OfflineEditException("PREVIEW_NOT_FOUND", "Preview is missing, consumed, expired, or belongs to another Router session. Preview the script again.");
            using var source = store.OpenReadOnly(filePath);
            if (!string.Equals(source.SourcePath, preview.SourcePath, StringComparison.OrdinalIgnoreCase) || source.SourceSha256 != preview.SourceHash)
                throw new OfflineEditException("STALE_PREVIEW", "Source path or contents changed. Preview the script again before applying.");
            string output = store.PublishCopy(source.SourcePath, preview.Bytes, cancellationToken);
            _previews.Remove(previewToken);
            return new(source.SourcePath, output, source.SourceSha256, Convert.ToHexString(SHA256.HashData(preview.Bytes)), preview.Bytes.Length);
        }
    }

    private void Expire()
    {
        foreach (string token in _previews.Where(x => x.Value.ExpiresUtc <= _time.GetUtcNow()).Select(x => x.Key).ToArray())
            _previews.Remove(token);
    }
}
