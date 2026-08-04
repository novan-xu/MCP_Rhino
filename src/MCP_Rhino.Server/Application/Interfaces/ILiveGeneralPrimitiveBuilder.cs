extern alias rhinocommon;

using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILiveGeneralPrimitiveBuilder
{
    OperationResponse<GeometryBase> Build(GeneralPrimitiveCreationSpec spec);
}
