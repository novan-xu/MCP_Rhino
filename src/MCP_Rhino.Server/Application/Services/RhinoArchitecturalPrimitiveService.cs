using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoArchitecturalPrimitiveService
{
    private readonly ILiveArchitecturalGeometryBuilder _builder;

    public RhinoArchitecturalPrimitiveService(ILiveArchitecturalGeometryBuilder builder)
    {
        _builder = builder;
    }

    public OperationResponse<ArchitecturalCreationResponse> Create(
        string filePath,
        IReadOnlyList<ArchitecturalPrimitiveSpec> specs,
        ArchitecturalObjectAttributesSpec attributes,
        string undoRecordName)
    {
        if (specs.Count == 0)
        {
            return OperationResponse<ArchitecturalCreationResponse>.Fail("At least one architectural primitive item is required.");
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return OperationResponse<ArchitecturalCreationResponse>.Fail("FilePath is required.");
        }

        return _builder.Create(filePath, specs, attributes, undoRecordName);
    }
}
