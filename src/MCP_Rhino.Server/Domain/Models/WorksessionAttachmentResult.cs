using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class WorksessionAttachmentResult
{
    public string SourceFilePath { get; set; } = string.Empty;
    public WorksessionAttachmentStatus Status { get; set; } = WorksessionAttachmentStatus.Attached;
    public bool Attached { get; set; }
    public bool WasAlreadyAttached { get; set; }
    public string Message { get; set; } = string.Empty;
}
