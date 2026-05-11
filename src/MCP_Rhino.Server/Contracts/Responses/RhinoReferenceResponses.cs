namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class RhinoReferenceModuleListResponse
{
    public string Source { get; set; } = string.Empty;
    public string RhinoVersion { get; set; } = string.Empty;
    public int ModuleCount { get; set; }
    public IReadOnlyList<RhinoReferenceModuleResponse> Modules { get; set; } = Array.Empty<RhinoReferenceModuleResponse>();
}

public sealed class RhinoReferenceModuleResponse
{
    public string Name { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public IReadOnlyList<string> FunctionNames { get; set; } = Array.Empty<string>();
}

public sealed class RhinoReferenceSearchResponse
{
    public string Query { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public int ResultCount { get; set; }
    public IReadOnlyList<RhinoReferenceFunctionSummaryResponse> Results { get; set; } = Array.Empty<RhinoReferenceFunctionSummaryResponse>();
}

public sealed class RhinoReferenceFunctionSummaryResponse
{
    public string ModuleName { get; set; } = string.Empty;
    public string FunctionName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();
}

public sealed class RhinoReferenceFunctionDetailResponse
{
    public string Source { get; set; } = string.Empty;
    public string RhinoVersion { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string FunctionName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public IReadOnlyList<string> Notes { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();
}

public sealed class McpRhinoToolHelpResponse
{
    public int ToolCount { get; set; }
    public IReadOnlyList<McpRhinoToolHelpEntryResponse> Tools { get; set; } = Array.Empty<McpRhinoToolHelpEntryResponse>();
}

public sealed class McpRhinoToolFamilyListResponse
{
    public int FamilyCount { get; set; }
    public IReadOnlyList<McpRhinoToolFamilyResponse> Families { get; set; } = Array.Empty<McpRhinoToolFamilyResponse>();
}

public sealed class McpRhinoToolFamilyResponse
{
    public string Family { get; set; } = string.Empty;
    public int ToolCount { get; set; }
    public IReadOnlyList<string> ToolNames { get; set; } = Array.Empty<string>();
}

public sealed class McpRhinoToolHelpEntryResponse
{
    public string Family { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool ReadOnly { get; set; }
    public bool Destructive { get; set; }
    public bool OpenWorld { get; set; }
    public IReadOnlyList<string> Parameters { get; set; } = Array.Empty<string>();
}
