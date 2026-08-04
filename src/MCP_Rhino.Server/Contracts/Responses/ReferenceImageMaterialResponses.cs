using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class ObjectColorResponse
{
    public int R { get; set; }
    public int G { get; set; }
    public int B { get; set; }
}

public sealed class RenderMaterialCreationResponse
{
    public string FilePath { get; set; } = string.Empty;
    public int RequestedCount { get; set; }
    public int CreatedCount { get; set; }
    public int ReusedCount { get; set; }
    public IReadOnlyList<RenderMaterialCreatedResponse> Materials { get; set; } = Array.Empty<RenderMaterialCreatedResponse>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class RenderMaterialCreatedResponse
{
    public int MaterialIndex { get; set; }
    public Guid MaterialId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ObjectColorResponse? BaseColor { get; set; }
    public double Roughness { get; set; }
    public double Transparency { get; set; }
    public bool ReusedExisting { get; set; }
}

public sealed class ObjectMaterialAssignmentResponse
{
    public string FilePath { get; set; } = string.Empty;
    public int RequestedObjectCount { get; set; }
    public int AssignedCount { get; set; }
    public int FailedCount { get; set; }
    public IReadOnlyList<ObjectMaterialAssignmentResultResponse> Results { get; set; } = Array.Empty<ObjectMaterialAssignmentResultResponse>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class ObjectMaterialAssignmentResultResponse
{
    public Guid ObjectId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public int MaterialIndex { get; set; }
    public bool Assigned { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class ProceduralTextureImageResponse
{
    public string OutputPath { get; set; } = string.Empty;
    public int WidthPx { get; set; }
    public int HeightPx { get; set; }
    public ProceduralTexturePatternKind PatternKind { get; set; }
    public bool OverwroteExisting { get; set; }
    public long BytesWritten { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class TexturedRenderMaterialCreationResponse
{
    public string FilePath { get; set; } = string.Empty;
    public int RequestedCount { get; set; }
    public int CreatedCount { get; set; }
    public int UpdatedExistingCount { get; set; }
    public IReadOnlyList<TexturedRenderMaterialCreatedResponse> Materials { get; set; } = Array.Empty<TexturedRenderMaterialCreatedResponse>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class TexturedRenderMaterialCreatedResponse
{
    public int MaterialIndex { get; set; }
    public Guid MaterialId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ObjectColorResponse? BaseColor { get; set; }
    public double Roughness { get; set; }
    public double Transparency { get; set; }
    public string DiffuseTextureImagePath { get; set; } = string.Empty;
    public int MappingChannel { get; set; }
    public bool CreatedNew { get; set; }
    public bool UpdatedExisting { get; set; }
}

public sealed class RenderMaterialTextureInspectionResponse
{
    public string FilePath { get; set; } = string.Empty;
    public int InspectedCount { get; set; }
    public IReadOnlyList<RenderMaterialTextureInspectionItemResponse> Materials { get; set; } = Array.Empty<RenderMaterialTextureInspectionItemResponse>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class RenderMaterialTextureInspectionItemResponse
{
    public int MaterialIndex { get; set; }
    public Guid MaterialId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DiffuseTextureImagePath { get; set; } = string.Empty;
    public int MappingChannel { get; set; }
    public bool HasDiffuseTexture { get; set; }
}

public sealed class TextureMappingPreviewResponse
{
    public string FilePath { get; set; } = string.Empty;
    public int RequestedObjectCount { get; set; }
    public int PreviewableCount { get; set; }
    public int FailedCount { get; set; }
    public IReadOnlyList<TextureMappingResultResponse> Results { get; set; } = Array.Empty<TextureMappingResultResponse>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class TextureMappingApplicationResponse
{
    public string FilePath { get; set; } = string.Empty;
    public int RequestedObjectCount { get; set; }
    public int AppliedCount { get; set; }
    public int FailedCount { get; set; }
    public IReadOnlyList<TextureMappingResultResponse> Results { get; set; } = Array.Empty<TextureMappingResultResponse>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class TextureMappingResultResponse
{
    public Guid ObjectId { get; set; }
    public int MappingChannel { get; set; }
    public TextureMappingKind MappingKind { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public double Width { get; set; }
    public double Depth { get; set; }
    public double Height { get; set; }
}
