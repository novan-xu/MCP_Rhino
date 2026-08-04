extern alias rhinocommon;

using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILiveTakeoffMetricReader
{
    OperationResponse<TakeoffLiveSnapshot> Read(RhinoDoc document, string filePath, TakeoffScopeRequest? scope);
}
