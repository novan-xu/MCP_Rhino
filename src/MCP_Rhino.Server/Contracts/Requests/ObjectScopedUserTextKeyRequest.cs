using System.ComponentModel;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class ObjectScopedUserTextKeyRequest
{
    [Description("目标对象的 GUID。")]
    public Guid ObjectId { get; set; }

    [Description("要删除的 user text key。")]
    public string Key { get; set; } = string.Empty;
}
