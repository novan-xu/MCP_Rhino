namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class ReferenceImageBriefSchemaResponse
{
    public string SchemaName { get; set; } = "BuildReferenceImageModelBriefRequest";
    public IReadOnlyList<string> RequiredConcepts { get; set; } = Array.Empty<string>();
    public string Markdown { get; set; } = string.Empty;
    public string JsonExample { get; set; } = string.Empty;
}
