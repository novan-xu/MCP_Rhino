extern alias rhinocommon;

using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface IGeometryMetadataOperator
{
    GeometryMetadataSnapshot Snapshot(RhinoObject rhinoObject);

    GeometryMetadataSummary Summarize(RhinoDoc document, RhinoObject rhinoObject);

    OperationResponse<IReadOnlyList<ObjectEditWarning>> Replay(RhinoDoc document, Guid objectId, GeometryMetadataSnapshot snapshot);
}
