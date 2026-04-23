namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class LinkedBlockResultResponse
{
    public Guid DefinitionId { get; set; }
    public int DefinitionIndex { get; set; }
    public string DefinitionName { get; set; } = string.Empty;
    public Guid? InstanceObjectId { get; set; }
    public string? SourceFilePath { get; set; }
    public bool Success { get; set; }
    public bool Updated { get; set; }
    public bool WasAlreadyDefined { get; set; }
    public string Message { get; set; } = string.Empty;
}
