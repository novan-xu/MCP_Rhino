extern alias rhinocommon;

using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;

namespace MCP_Rhino.Server.Application.Models;

public sealed class BoundaryRebuildResult
{
    public SurfaceRebuildRouteKind Route { get; set; }
    public GeometryBase Geometry { get; set; } = null!;
    public SurfaceRebuildPreviewSummary Summary { get; set; } = new();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class SurfaceRebuildPreviewSummary
{
    public string GeometryKind { get; set; } = string.Empty;
    public SurfaceRebuildRouteKind Route { get; set; }
    public int PointCount { get; set; }
    public bool IsValid { get; set; }
    public GeometryBoundingBoxData BoundingBox { get; set; } = new();
}
