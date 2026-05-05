using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class DrawingExportPackageResponse
{
    public DrawingExportResponseStatus Status { get; set; } = DrawingExportResponseStatus.Completed;
    public string FilePath { get; set; } = string.Empty;
    public IReadOnlyList<RhinoLayerCandidate> CandidateLayers { get; set; } = Array.Empty<RhinoLayerCandidate>();
    public IReadOnlyList<string> ResolvedLayerFullPaths { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ViewNames { get; set; } = Array.Empty<string>();
    public IReadOnlyList<DrawingExportItemResponse> ExportedFiles { get; set; } = Array.Empty<DrawingExportItemResponse>();
    public bool RestoreSucceeded { get; set; } = true;
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
    public IReadOnlyList<ObjectEditWarning> RestoreWarnings { get; set; } = Array.Empty<ObjectEditWarning>();
}
