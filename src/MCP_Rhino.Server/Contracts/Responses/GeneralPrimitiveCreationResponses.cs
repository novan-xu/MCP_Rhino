using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class GeneralPrimitiveBoundingBoxResponse
{
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MinZ { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }
    public double MaxZ { get; set; }
}

public sealed class GeneralPrimitiveCreationResponse
{
    public string FilePath { get; set; } = string.Empty;
    public int RequestedCount { get; set; }
    public int CreatedCount { get; set; }
    public int FailedCount { get; set; }
    public IReadOnlyList<GeneralPrimitiveCreatedObjectResponse> CreatedObjects { get; set; } = Array.Empty<GeneralPrimitiveCreatedObjectResponse>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class GeneralPrimitiveCreatedObjectResponse
{
    public Guid ObjectId { get; set; }
    public GeneralPrimitiveKind Kind { get; set; }
    public string GeometryTypeName { get; set; } = string.Empty;
    public string LayerFullPath { get; set; } = string.Empty;
    public GeneralPrimitiveBoundingBoxResponse? BoundingBox { get; set; }
}
