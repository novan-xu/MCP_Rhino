using System.ComponentModel;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class ObjectScopedUserTextEntryRequest
{
    [Description("Target object GUID.")]
    public Guid ObjectId { get; set; }

    [Description("User text key to write.")]
    public string Key { get; set; } = string.Empty;

    [Description("User text value to write. This may come from another tool, skill, or external object-level data source.")]
    public string Value { get; set; } = string.Empty;
}
