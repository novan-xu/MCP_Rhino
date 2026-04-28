using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class SurfaceEditSpec
{
    public SurfaceEditOperationSpec Operation { get; set; } = new();
    public SurfacePointSelectorSpec? PointSelector { get; set; }
    public EditableSurfacePointGrid? Grid { get; set; }
}

public sealed class SurfaceEditOperationSpec
{
    public GeometryEditOperationKind Kind { get; set; } = GeometryEditOperationKind.DirectOverride;
    public DerivedPointOperationKind? DerivedKind { get; set; }
    public SurfaceEditDerivedOperationParameters? DerivedParameters { get; set; }
}
