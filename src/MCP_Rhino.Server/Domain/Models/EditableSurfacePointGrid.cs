using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class EditableSurfacePointGrid
{
    public SurfacePointLayoutKind Layout { get; set; } = SurfacePointLayoutKind.Flat;
    public List<EditablePointInput> Points { get; set; } = new();
    public List<List<EditablePointInput>> Rows { get; set; } = new();
}
