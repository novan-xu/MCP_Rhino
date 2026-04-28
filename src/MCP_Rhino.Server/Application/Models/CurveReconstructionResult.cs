extern alias rhinocommon;

using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using Curve = rhinocommon::Rhino.Geometry.Curve;

namespace MCP_Rhino.Server.Application.Models;

public sealed class CurveReconstructionResult
{
    public GeometryEditStrategyKind Strategy { get; set; }
    public Curve Curve { get; set; } = null!;
    public ReconstructedCurveSummary Summary { get; set; } = new();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}
