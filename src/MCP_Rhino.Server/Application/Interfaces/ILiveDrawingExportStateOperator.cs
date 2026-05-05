extern alias rhinocommon;

using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILiveDrawingExportStateOperator
{
    DrawingExportSnapshot Capture(RhinoDoc document, string filePath, IReadOnlyList<Guid> objectIds);

    OperationResponse ApplyObjectColor(RhinoDoc document, DrawingExportSnapshot snapshot, RhinoDisplayColor? color);

    OperationResponse SetBackground(RhinoDoc document, DrawingExportSnapshot snapshot, RhinoDisplayColor color);

    OperationResponse<IReadOnlyList<ObjectEditWarning>> Restore(RhinoDoc document, DrawingExportSnapshot snapshot);
}
