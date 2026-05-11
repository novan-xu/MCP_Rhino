using System.ComponentModel;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class ObjectScopedUserTextKeyRequest
{
    [Description("Target object GUID.")]
    public Guid ObjectId { get; set; }

    [Description("User text key to delete.")]
    public string Key { get; set; } = string.Empty;
}
