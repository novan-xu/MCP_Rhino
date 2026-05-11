using System.ComponentModel;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class DocumentUserStringEntryRequest
{
    [Description("Optional document user string section name. Use with Key to address namespaced document user strings.")]
    public string? Section { get; set; }

    [Description("Document user string key or entry name. Without Section, this is a top-level key; with Section, this is the entry under that section.")]
    public string Key { get; set; } = string.Empty;

    [Description("Document user string value to write. This is ignored for delete requests.")]
    public string Value { get; set; } = string.Empty;
}
