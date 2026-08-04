using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class SubDCagePreviewResponse
{
    public int VertexCount { get; set; }
    public int FaceCount { get; set; }
    public int BoundaryEdgeCount { get; set; }
    public int NonManifoldEdgeCount { get; set; }
    public GeneralPrimitiveBoundingBoxResponse? BoundingBox { get; set; }
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class SubDCreationResponse
{
    public string FilePath { get; set; } = string.Empty;
    public SubDModelingOperationKind Operation { get; set; }
    public int RequestedCount { get; set; }
    public int CreatedCount { get; set; }
    public int FailedCount { get; set; }
    public IReadOnlyList<SubDCreatedObjectResponse> CreatedObjects { get; set; } = Array.Empty<SubDCreatedObjectResponse>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class SubDCreatedObjectResponse
{
    public Guid ObjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string GeometryTypeName { get; set; } = string.Empty;
    public string LayerFullPath { get; set; } = string.Empty;
    public GeneralPrimitiveBoundingBoxResponse? BoundingBox { get; set; }
}

public sealed class SubDInspectionResponse
{
    public string FilePath { get; set; } = string.Empty;
    public int RequestedCount { get; set; }
    public int InspectedCount { get; set; }
    public int FailedCount { get; set; }
    public IReadOnlyList<SubDInspectionEntryResponse> Results { get; set; } = Array.Empty<SubDInspectionEntryResponse>();
}

public sealed class SubDInspectionEntryResponse
{
    public Guid ObjectId { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string GeometryTypeName { get; set; } = string.Empty;
    public int? VertexCount { get; set; }
    public int? EdgeCount { get; set; }
    public int? FaceCount { get; set; }
    public GeneralPrimitiveBoundingBoxResponse? BoundingBox { get; set; }
}
