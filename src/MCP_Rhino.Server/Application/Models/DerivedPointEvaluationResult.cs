using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Models;

public sealed class DerivedPointEvaluationResult
{
    public IReadOnlyList<int> ResolvedPointIndices { get; set; } = Array.Empty<int>();
    public IReadOnlyList<EditablePointInput> NewPoints { get; set; } = Array.Empty<EditablePointInput>();
    public DerivedOperationApplied DerivedOperationApplied { get; set; } = new();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}
