namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class GeometryCreationCommonOptions
{
    public string LayerFullPath { get; set; } = string.Empty;
    public ObjectColorRequest? Color { get; set; }
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> UserText { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
