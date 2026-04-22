using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class GeometryAnalysisReferenceRequest
{
    public Guid ObjectId { get; set; }
    public GeometryCreationSpec? Geometry { get; set; }
    public double? Parameter { get; set; }
    public double? U { get; set; }
    public double? V { get; set; }
}
