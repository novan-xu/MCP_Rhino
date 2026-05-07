extern alias rhinocommon;

using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface IGeometryMutator
{
    OperationResponse<ObjectEditOperationResult> Transform(RhinoDoc document, RhinoObjectInfo target, GeometryTransformSpec spec);
    OperationResponse<ObjectEditOperationResult> Replace(RhinoDoc document, RhinoObjectInfo target, GeometryReplacementSpec spec);
    OperationResponse<ObjectEditOperationResult> Delete(RhinoDoc document, RhinoObjectInfo target);
    OperationResponse<ObjectEditOperationResult> EditControlPoints(RhinoDoc document, RhinoObjectInfo target, IReadOnlyList<ControlPointEditSpec> specs);
}
