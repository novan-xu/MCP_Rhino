using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class DocumentSummaryResponse
{
    public string FilePath { get; set; } = string.Empty;
    public string DocumentName { get; set; } = string.Empty;
    public string UnitSystem { get; set; } = string.Empty;
    public double AbsoluteTolerance { get; set; }
    public double AngleToleranceDegrees { get; set; }
    public int ObjectCount { get; set; }
    public int LayerCount { get; set; }
    public CurrentLayerResponse? CurrentLayer { get; set; }
    public int SelectedObjectCount { get; set; }
    public IReadOnlyList<DocumentObjectTypeCountResponse> ObjectCountsByType { get; set; } = Array.Empty<DocumentObjectTypeCountResponse>();
    public IReadOnlyList<DocumentLayerObjectCountResponse> ObjectCountsByLayer { get; set; } = Array.Empty<DocumentLayerObjectCountResponse>();
    public IReadOnlyList<string> NamedViews { get; set; } = Array.Empty<string>();
    public IReadOnlyList<DocumentMaterialSummaryResponse> Materials { get; set; } = Array.Empty<DocumentMaterialSummaryResponse>();
    public IReadOnlyList<RhinoObjectInfo> ObjectSummaries { get; set; } = Array.Empty<RhinoObjectInfo>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class DocumentObjectTypeCountResponse
{
    public RhinoObjectType ObjectType { get; set; } = RhinoObjectType.Unknown;
    public int Count { get; set; }
}

public sealed class DocumentLayerObjectCountResponse
{
    public int LayerIndex { get; set; }
    public string LayerName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public int ObjectCount { get; set; }
}

public sealed class DocumentMaterialSummaryResponse
{
    public int MaterialIndex { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class CurrentLayerResponse
{
    public string FilePath { get; set; } = string.Empty;
    public Guid LayerId { get; set; }
    public int LayerIndex { get; set; }
    public string LayerName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool Visible { get; set; }
    public bool Locked { get; set; }
    public int ObjectCount { get; set; }
}

public sealed class CurrentLayerMutationResponse
{
    public string FilePath { get; set; } = string.Empty;
    public CurrentLayerResponse? PreviousLayer { get; set; }
    public CurrentLayerResponse? CurrentLayer { get; set; }
    public bool Changed { get; set; }
}

public sealed class SelectedObjectsResponse
{
    public string FilePath { get; set; } = string.Empty;
    public int SelectedCount { get; set; }
    public IReadOnlyList<RhinoObjectInfo> Objects { get; set; } = Array.Empty<RhinoObjectInfo>();
}

public sealed class SelectionMutationResponse
{
    public string FilePath { get; set; } = string.Empty;
    public RhinoSelectionMode SelectionMode { get; set; } = RhinoSelectionMode.Replace;
    public int RequestedObjectCount { get; set; }
    public int MatchedObjectCount { get; set; }
    public int PreviousSelectedCount { get; set; }
    public int CurrentSelectedCount { get; set; }
    public IReadOnlyList<Guid> MissingObjectIds { get; set; } = Array.Empty<Guid>();
    public IReadOnlyList<RhinoObjectInfo> SelectedObjects { get; set; } = Array.Empty<RhinoObjectInfo>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class ViewportCaptureResponse
{
    public string FilePath { get; set; } = string.Empty;
    public string ViewName { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public string ContentType { get; set; } = "image/png";
    public string DataBase64 { get; set; } = string.Empty;
    public int ByteCount { get; set; }
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}
