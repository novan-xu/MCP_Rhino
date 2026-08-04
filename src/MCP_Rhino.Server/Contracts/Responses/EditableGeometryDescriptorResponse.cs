using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class EditableGeometryDescriptorResponse
{
    public string FilePath { get; set; } = string.Empty;
    public Guid ObjectId { get; set; }
    public EditableGeometryDescriptor? Descriptor { get; set; }
}
