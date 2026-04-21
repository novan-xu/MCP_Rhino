extern alias rhinocommon;

using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILiveGeometryValidator
{
    OperationResponse ValidateAgainstDocument(RhinoDoc document, ControlPointEditSpec spec);
}
