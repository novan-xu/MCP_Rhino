namespace MCP_Rhino.Server.Domain.Models;

public sealed class EditableCurveStructure
{
    public string CurveKind { get; set; } = string.Empty;
    public int Degree { get; set; }
    public bool IsClosed { get; set; }
    public bool IsPeriodic { get; set; }
}
