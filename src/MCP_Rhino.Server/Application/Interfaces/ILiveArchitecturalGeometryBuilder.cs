using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILiveArchitecturalGeometryBuilder
{
    OperationResponse<ArchitecturalCreationResponse> Create(
        string filePath,
        IReadOnlyList<ArchitecturalPrimitiveSpec> specs,
        ArchitecturalObjectAttributesSpec attributes,
        string undoRecordName);
}
