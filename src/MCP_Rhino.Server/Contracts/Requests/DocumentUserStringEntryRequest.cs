using System.ComponentModel;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class DocumentUserStringEntryRequest
{
    [Description("可选的 section 名称。配合 Entry 使用，可定位到具有命名空间的文档级 user string。")]
    public string? Section { get; set; }

    [Description("user string 的 key 或 entry 名称。当未提供 Section 时，作为顶层 key；当提供 Section 时，作为该 section 下的 entry。")]
    public string Key { get; set; } = string.Empty;

    [Description("要写入的 user string value。删除场景下可忽略。")]
    public string Value { get; set; } = string.Empty;
}
