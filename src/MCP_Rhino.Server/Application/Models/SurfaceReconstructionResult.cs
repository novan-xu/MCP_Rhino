extern alias rhinocommon;

using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;

namespace MCP_Rhino.Server.Application.Models;

public sealed class SurfaceReconstructionResult
{
    public GeometryEditStrategyKind Strategy { get; set; }
    public GeometryBase Geometry { get; set; } = null!;
    public SurfaceControlPointGridSnapshot Summary { get; set; } = new();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}
