using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class CurveEditSpec
{
    public GeometryEditOperationSpec Operation { get; set; } = new();
    public PointSelectorSpec? PointSelector { get; set; }
    public List<EditablePointInput> Points { get; set; } = new();
}

public sealed class GeometryEditOperationSpec
{
    public GeometryEditOperationKind Kind { get; set; } = GeometryEditOperationKind.DirectOverride;
    public DerivedPointOperationKind? DerivedKind { get; set; }
    public DerivedPointOperationParameters? DerivedParameters { get; set; }
}
