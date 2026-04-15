using System.ComponentModel;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class ObjectScopedUserTextEntryRequest
{
    [Description("目标对象的 GUID。")]
    public Guid ObjectId { get; set; }

    [Description("要写入的 user text key。")]
    public string Key { get; set; } = string.Empty;

    [Description("要写入的 user text value。可来自其他工具/技能/外部端口产出的对象级信息。")]
    public string Value { get; set; } = string.Empty;
}