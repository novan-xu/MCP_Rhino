using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface IDrawingExportSnapshotStore
{
    DrawingExportSnapshot Store(DrawingExportSnapshot snapshot);

    bool TryGet(string snapshotId, out DrawingExportSnapshot? snapshot);

    bool Remove(string snapshotId);
}
