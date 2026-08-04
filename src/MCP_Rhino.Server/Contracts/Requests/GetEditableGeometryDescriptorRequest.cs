using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class GetEditableGeometryDescriptorRequest
{
    public string FilePath { get; set; } = string.Empty;
    public Guid ObjectId { get; set; }
    public DescriptorDetail? Detail { get; set; }
}
