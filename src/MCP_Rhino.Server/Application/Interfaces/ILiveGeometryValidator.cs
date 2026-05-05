extern alias rhinocommon;

using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILiveGeometryValidator
{
    OperationResponse<IReadOnlyList<ObjectEditWarning>> Validate(GeometryCreationSpec spec, GeometryObjectAttributesSpec attributes);
    OperationResponse<IReadOnlyList<ObjectEditWarning>> Validate(GeometryTransformSpec spec, IReadOnlyList<RhinoObjectInfo> targets);
    OperationResponse<IReadOnlyList<ObjectEditWarning>> Validate(GeometryReplacementSpec spec, RhinoObjectInfo target);
    OperationResponse<IReadOnlyList<ObjectEditWarning>> Validate(ControlPointEditSpec spec, RhinoObjectInfo target);
    OperationResponse ValidateAgainstDocument(RhinoDoc document, ControlPointEditSpec spec);
}
