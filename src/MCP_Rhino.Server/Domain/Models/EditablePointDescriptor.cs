using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class EditablePointDescriptor
{
    public int Index { get; set; }
    public int? UIndex { get; set; }
    public int? VIndex { get; set; }
    public EditablePointRole Role { get; set; } = EditablePointRole.ControlPoint;
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}
