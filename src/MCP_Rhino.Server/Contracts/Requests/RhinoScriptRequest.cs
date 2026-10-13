namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class RhinoScriptRequest
{
    public string FilePath { get; init; } = "";
    public string Script { get; init; } = "";
    public string ParametersJson { get; init; } = "{}";
    public int TimeoutSeconds { get; init; } = 60;
}
