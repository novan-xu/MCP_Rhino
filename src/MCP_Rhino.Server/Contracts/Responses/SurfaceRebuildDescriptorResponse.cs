using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class SurfaceRebuildDescriptorResponse
{
    public string FilePath { get; set; } = string.Empty;
    public IReadOnlyList<SurfaceRebuildDescriptor> Descriptors { get; set; } = Array.Empty<SurfaceRebuildDescriptor>();
}
