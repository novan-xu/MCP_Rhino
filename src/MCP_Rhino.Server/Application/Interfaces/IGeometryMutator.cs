using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using Rhino.FileIO;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface IGeometryMutator
{
    OperationResponse<ObjectEditOperationResult> Transform(File3dm model, RhinoObjectInfo target, GeometryTransformSpec spec);
    OperationResponse<ObjectEditOperationResult> Replace(File3dm model, RhinoObjectInfo target, GeometryReplacementSpec spec);
    OperationResponse<ObjectEditOperationResult> Delete(File3dm model, RhinoObjectInfo target);
    OperationResponse<ObjectEditOperationResult> EditControlPoints(File3dm model, RhinoObjectInfo target, IReadOnlyList<ControlPointEditSpec> specs);
}
