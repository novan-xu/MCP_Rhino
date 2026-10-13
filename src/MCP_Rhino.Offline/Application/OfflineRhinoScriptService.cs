using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MCP_Rhino.Offline.Application.Interfaces;
using MCP_Rhino.Offline.Contracts;

namespace MCP_Rhino.Offline.Application;

public sealed class OfflineRhinoScriptService(IOfflineRhinoStore store, IRhinoJobExecutor worker, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private sealed record Preview(string Path, string Hash, byte[] Bytes, DateTimeOffset Expires);
    private readonly Dictionary<string, Preview> _previews = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1);

    public async Task<RhinoScriptPreviewResponse> PreviewAsync(string filePath, string script = "", string parametersJson = "{}",
        string? libraryPath = null, string? libraryType = null, string? modelUnits = null,
        int timeoutSeconds = 120, CancellationToken cancellationToken = default)
    {
        if (script.Length > 65536 || parametersJson.Length > 65536 || timeoutSeconds is < 1 or > 600)
            throw new OfflineEditException("INVALID_RHINO_SCRIPT", "Script/parameters limit is 64 KiB each; timeout is 1–600 seconds.");
        if ((libraryPath is null) != (libraryType is null) || libraryType is { Length: 0 })
            throw new OfflineEditException("INVALID_LIBRARY", "Supply libraryPath and its single class libraryType together.");
        using var parsed = JsonDocument.Parse(parametersJson);
        if (parsed.RootElement.ValueKind != JsonValueKind.Object) throw new OfflineEditException("INVALID_PARAMETERS", "parametersJson must be an object.");
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Expire();
            if (_previews.Count >= 4)
                throw new OfflineEditException("PREVIEW_CAPACITY", "Apply or let existing native previews expire before running another script.");
            using var source = store.OpenReadOnly(filePath);
            string? library = null, libraryHash = null;
            if (libraryPath is not null) (library, libraryHash) = store.ReadLibrary(libraryPath);
            // Source read lock remains held until native execution/validation is finished.
            RhinoJobOutput output = await worker.ExecuteAsync(source.Snapshot, Path.GetExtension(source.SourcePath).ToLowerInvariant(),
                script, parametersJson, library, libraryType, modelUnits, timeoutSeconds, cancellationToken);
            store.ValidateOutput(output.Bytes, output.Report);
            cancellationToken.ThrowIfCancellationRequested();
            if (_previews.Count >= 4 || _previews.Values.Sum(p => (long)p.Bytes.Length) + output.Bytes.Length > 536870912)
                throw new OfflineEditException("PREVIEW_CAPACITY", "At most four native previews/512 MiB are retained for ten minutes.");
            string token = Guid.NewGuid().ToString("N"); var expires = _time.GetUtcNow().AddMinutes(10);
            _previews.Add(token, new(source.SourcePath, source.SourceSha256, output.Bytes, expires));
            return new(token, source.SourcePath, source.SourceSha256, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(script))),
                libraryHash, expires, output.Report);
        }
        finally { _gate.Release(); }
    }

    public async Task<OfflineApplyResponse> ApplyAsync(string filePath, string previewToken, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Expire();
            if (!_previews.TryGetValue(previewToken, out var preview)) throw new OfflineEditException("PREVIEW_NOT_FOUND", "Native preview expired, consumed or from another Router session.");
            using var source = store.OpenReadOnly(filePath);
            if (!string.Equals(source.SourcePath, preview.Path, StringComparison.OrdinalIgnoreCase) || source.SourceSha256 != preview.Hash)
                throw new OfflineEditException("STALE_PREVIEW", "Source path/contents changed; preview again.");
            string output = store.PublishCopy(source.SourcePath, preview.Bytes, cancellationToken);
            _previews.Remove(previewToken);
            return new(source.SourcePath, output, source.SourceSha256, Convert.ToHexString(SHA256.HashData(preview.Bytes)), preview.Bytes.Length);
        }
        finally { _gate.Release(); }
    }
    private void Expire()
    {
        foreach (string token in _previews.Where(p => p.Value.Expires <= _time.GetUtcNow()).Select(p => p.Key).ToArray()) _previews.Remove(token);
    }
}
