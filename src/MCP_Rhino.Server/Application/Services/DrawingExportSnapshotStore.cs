using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services;

public sealed class DrawingExportSnapshotStore : IDrawingExportSnapshotStore
{
    private static readonly TimeSpan SnapshotTtl = TimeSpan.FromMinutes(30);
    private readonly object _gate = new();
    private readonly Dictionary<string, DrawingExportSnapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);

    public DrawingExportSnapshot Store(DrawingExportSnapshot snapshot)
    {
        lock (_gate)
        {
            PurgeExpired();
            snapshot.SnapshotId = string.IsNullOrWhiteSpace(snapshot.SnapshotId)
                ? Guid.NewGuid().ToString("N")
                : snapshot.SnapshotId;
            snapshot.CreatedUtc = snapshot.CreatedUtc == default ? DateTime.UtcNow : snapshot.CreatedUtc;
            snapshot.ExpiresUtc = snapshot.ExpiresUtc == default ? snapshot.CreatedUtc.Add(SnapshotTtl) : snapshot.ExpiresUtc;
            _snapshots[snapshot.SnapshotId] = snapshot;
            return snapshot;
        }
    }

    public bool TryGet(string snapshotId, out DrawingExportSnapshot? snapshot)
    {
        lock (_gate)
        {
            PurgeExpired();
            return _snapshots.TryGetValue(snapshotId, out snapshot);
        }
    }

    public bool Remove(string snapshotId)
    {
        lock (_gate)
        {
            return _snapshots.Remove(snapshotId);
        }
    }

    private void PurgeExpired()
    {
        DateTime now = DateTime.UtcNow;
        string[] expired = _snapshots
            .Where(pair => pair.Value.ExpiresUtc <= now)
            .Select(pair => pair.Key)
            .ToArray();

        foreach (string snapshotId in expired)
        {
            _snapshots.Remove(snapshotId);
        }
    }
}
