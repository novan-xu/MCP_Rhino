using System.Security.Cryptography;
using MCP_Rhino.Offline.Application.Interfaces;
using MCP_Rhino.Offline.Contracts;
using Rhino.FileIO;

namespace MCP_Rhino.Offline.Infrastructure;

public sealed class OfflineRhinoStore : IOfflineRhinoStore
{
    public IOfflineRhinoSource OpenReadOnly(string filePath) => new Source(filePath);

    public (string Source, string Hash) ReadLibrary(string libraryPath)
    {
        if (!Path.IsPathFullyQualified(libraryPath) || !new[] { ".cs", ".csx" }.Contains(Path.GetExtension(libraryPath).ToLowerInvariant()))
            throw new OfflineEditException("INVALID_LIBRARY", "Library must be an absolute .cs/.csx path to trusted code.");
        using var stream = new FileStream(libraryPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > 1048576) throw new OfflineEditException("LIBRARY_SIZE_LIMIT", "Library must be 1 byte–1 MiB.");
        byte[] bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes);
        using var reader = new StreamReader(new MemoryStream(bytes), detectEncodingFromByteOrderMarks: true);
        return (reader.ReadToEnd(), Convert.ToHexString(SHA256.HashData(bytes)));
    }

    public void ValidateOutput(byte[] bytes, RhinoWorkerResult result)
    {
        if (bytes.Length is <= 0 or > 268435456) throw new OfflineEditException("OUTPUT_SIZE_LIMIT", "Rhino output must be at most 256 MiB.");
        using var reread = File3dm.FromByteArray(bytes) ?? throw new OfflineEditException("INVALID_OUTPUT", "Generated Rhino output cannot be reopened.");
        if (reread.Objects.Count != result.ObjectCountAfter || reread.Settings.ModelUnitSystem.ToString() != result.ModelUnits)
            throw new OfflineEditException("INVALID_OUTPUT", "Generated Rhino output failed count/units verification.");
        var ids = new HashSet<Guid>();
        foreach (File3dmObject obj in reread.Objects)
            if (obj.Id == Guid.Empty || !ids.Add(obj.Id) || !obj.Geometry.IsValid)
                throw new OfflineEditException("INVALID_OUTPUT", "Generated output contains invalid geometry or duplicate/empty object IDs.");
    }

    public string PublishCopy(string sourcePath, byte[] bytes, CancellationToken cancellationToken) =>
        OfflineCopyWriter.Publish(sourcePath, bytes, cancellationToken);

    private sealed class Source : IOfflineRhinoSource
    {
        private readonly FileStream _lease;
        public string SourcePath { get; }
        public string SourceSha256 { get; }
        public byte[] Snapshot { get; }
        public Source(string path)
        {
            if (!Path.IsPathFullyQualified(path) || !new[] { ".dwg", ".dxf", ".3dm" }.Contains(Path.GetExtension(path).ToLowerInvariant()))
                throw new OfflineEditException("INVALID_SOURCE_PATH", "Use an absolute .dwg, .dxf or .3dm source path.");
            SourcePath = Path.GetFullPath(path);
            _lease = new FileStream(SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                if (_lease.Length is <= 0 or > 268435456) throw new OfflineEditException("SOURCE_SIZE_LIMIT", "Source must be 1 byte–256 MiB.");
                Snapshot = new byte[(int)_lease.Length]; _lease.ReadExactly(Snapshot);
                SourceSha256 = Convert.ToHexString(SHA256.HashData(Snapshot));
            }
            catch { _lease.Dispose(); throw; }
        }
        public void Dispose() => _lease.Dispose();
    }
}
