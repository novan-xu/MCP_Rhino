using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using Rhino.Geometry;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface IGeometryBuilder
{
    OperationResponse<GeometryBase> Build(GeometryCreationSpec spec);
}
