using System.Security.Cryptography;
using MCP_Rhino.Offline.Contracts;
using Rhino.FileIO;

namespace MCP_Rhino.Offline.Infrastructure;

internal sealed class OfflineSource : IDisposable
{
    // This handle is never writable. Denying write/delete sharing pins the source through apply.
    private readonly FileStream _handle;
    public string Path { get; }
    public string Sha256 { get; }
    public File3dm Model { get; }

    public OfflineSource(string filePath)
    {
        if (!System.IO.Path.IsPathFullyQualified(filePath) ||
            !string.Equals(System.IO.Path.GetExtension(filePath), ".3dm", StringComparison.OrdinalIgnoreCase))
            throw new OfflineEditException("INVALID_SOURCE_PATH", "Supply an absolute path to an existing .3dm file.");
        Path = System.IO.Path.GetFullPath(filePath);
        _handle = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            if (_handle.Length is <= 0 or > 268435456)
                throw new OfflineEditException("SOURCE_SIZE_LIMIT", "Offline files must be nonempty and at most 256 MiB.");
            byte[] bytes = new byte[checked((int)_handle.Length)];
            _handle.ReadExactly(bytes);
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes));
            Model = File3dm.FromByteArray(bytes) ?? throw new OfflineEditException("INVALID_3DM", "Cannot read this .3dm file.");
            if (Model.ArchiveVersion is < 2 or > 80)
            {
                Model.Dispose();
                throw new OfflineEditException("UNSUPPORTED_3DM_VERSION", "Offline editing supports Rhino 2–8 files only.");
            }
        }
        catch { _handle.Dispose(); throw; }
    }

    public void Dispose() { Model.Dispose(); _handle.Dispose(); }
}
