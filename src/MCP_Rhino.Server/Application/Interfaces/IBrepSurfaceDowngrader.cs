extern alias rhinocommon;

using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;
using Surface = rhinocommon::Rhino.Geometry.Surface;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface IBrepSurfaceDowngrader
{
    OperationResponse<(BrepDowngradeResult Result, Surface? Surface)> Evaluate(RhinoObject rhinoObject, double tolerance);
}
