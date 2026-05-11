using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class CurveDerivedGeometryResponse
{
    public string FilePath { get; set; } = string.Empty;
    public CurveDerivedOperationKind Operation { get; set; }
    public int RequestedCount { get; set; }
    public int SucceededCount { get; set; }
    public int FailedCount { get; set; }
    public int CreatedObjectCount { get; set; }
    public int DeletedSourceObjectCount { get; set; }
    public IReadOnlyList<CurveDerivedGeometryEntryResponse> Results { get; set; } = Array.Empty<CurveDerivedGeometryEntryResponse>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class CurveDerivedGeometryEntryResponse
{
    public int EntryIndex { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<Guid> SourceObjectIds { get; set; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> CreatedObjectIds { get; set; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> DeletedSourceObjectIds { get; set; } = Array.Empty<Guid>();
    public IReadOnlyList<string> CreatedGeometryTypeNames { get; set; } = Array.Empty<string>();
    public string LayerFullPath { get; set; } = string.Empty;
    public int SegmentCount { get; set; }
}

public sealed class CurveSplitPreviewResponse
{
    public string FilePath { get; set; } = string.Empty;
    public int RequestedCount { get; set; }
    public int PreviewedCount { get; set; }
    public int FailedCount { get; set; }
    public IReadOnlyList<CurveSplitPreviewEntryResponse> Results { get; set; } = Array.Empty<CurveSplitPreviewEntryResponse>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class CurveSplitPreviewEntryResponse
{
    public int EntryIndex { get; set; }
    public Guid CurveObjectId { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<double> SplitParameters { get; set; } = Array.Empty<double>();
    public int ExpectedSegmentCount { get; set; }
    public bool SourceWillBeDeletedOnReplace { get; set; }
}
