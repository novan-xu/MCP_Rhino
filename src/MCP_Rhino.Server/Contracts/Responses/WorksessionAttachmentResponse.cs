using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class WorksessionAttachmentResponse
{
    public string FilePath { get; set; } = string.Empty;
    public IReadOnlyList<string> AttachedFiles { get; set; } = Array.Empty<string>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
    public IReadOnlyList<WorksessionAttachmentResult> Results { get; set; } = Array.Empty<WorksessionAttachmentResult>();
}
