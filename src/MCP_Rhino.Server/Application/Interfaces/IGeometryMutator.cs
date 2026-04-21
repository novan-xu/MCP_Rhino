using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface IGeometryMutator
{
    OperationResponse<ObjectEditOperationResult> Transform(RhinoObjectInfo target, GeometryTransformSpec spec);
    OperationResponse<ObjectEditOperationResult> Replace(RhinoObjectInfo target, GeometryReplacementSpec spec);
    OperationResponse<ObjectEditOperationResult> Delete(RhinoObjectInfo target);
    OperationResponse<ObjectEditOperationResult> EditControlPoints(RhinoObjectInfo target, IReadOnlyList<ControlPointEditSpec> specs);
}
