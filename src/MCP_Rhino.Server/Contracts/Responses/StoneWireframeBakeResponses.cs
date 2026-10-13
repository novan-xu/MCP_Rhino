namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class StoneWireframeBakeResponse
{
    public string FilePath { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string OutputMode { get; set; } = string.Empty;
    public string DocumentUnits { get; set; } = string.Empty;
    public double SurfaceHeightInches { get; set; }
    public double SurfaceHeightModelUnits { get; set; }
    public int AssemblyDefinitionCount { get; set; }
    public int AssemblyInstanceCount { get; set; }
    public int StoneDefinitionCount { get; set; }
    public int StoneOccurrenceCount { get; set; }
    public int WouldCreateDefinitionCount { get; set; }
    public int WouldReuseDefinitionCount { get; set; }
    public int WouldCreateInstanceCount { get; set; }
    public int ExistingInstanceCount { get; set; }
    public int CreatedDefinitionCount { get; set; }
    public int CreatedInstanceCount { get; set; }
    public int FailedCount { get; set; }
    public IReadOnlyList<StoneWireframeDefinitionResponse> Definitions { get; set; } = Array.Empty<StoneWireframeDefinitionResponse>();
    public IReadOnlyList<StoneWireframeAssemblyResponse> Assemblies { get; set; } = Array.Empty<StoneWireframeAssemblyResponse>();
    public IReadOnlyList<StoneBakePlacementResponse> Placements { get; set; } = Array.Empty<StoneBakePlacementResponse>();
    public IReadOnlyList<StoneWireframeBakeFailureResponse> Failures { get; set; } = Array.Empty<StoneWireframeBakeFailureResponse>();
    public IReadOnlyList<Guid> CreatedObjectIds { get; set; } = Array.Empty<Guid>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class StoneWireframeDefinitionResponse
{
    public Guid SourceDefinitionId { get; set; }
    public string SourceDefinitionName { get; set; } = string.Empty;
    public string StoneOrientation { get; set; } = string.Empty;
    public string OutputDefinitionName { get; set; } = string.Empty;
    public string OutputDefinitionAction { get; set; } = string.Empty;
    public string OutputGeometryType { get; set; } = string.Empty;
    public Guid SourceObjectId { get; set; }
    public StoneWireframePointResponse StartPoint { get; set; } = new();
    public StoneWireframePointResponse EndPoint { get; set; } = new();
    public StoneWireframePointResponse BasePoint { get; set; } = new();
    public double Length { get; set; }
    public int OccurrenceCount { get; set; }
    public int WouldCreateInstanceCount { get; set; }
    public int ExistingInstanceCount { get; set; }
}

public sealed class StoneBakePlacementResponse
{
    public Guid AssemblyInstanceId { get; set; }
    public string AssemblyDefinitionName { get; set; } = string.Empty;
    public string OutputDefinitionName { get; set; } = string.Empty;
    public Guid OutputObjectId { get; set; }
    public bool AlreadyExists { get; set; }
    public StoneWireframePointResponse WorldStartPoint { get; set; } = new();
    public StoneWireframePointResponse WorldEndPoint { get; set; } = new();
    public StoneWireframePointResponse WorldTopStartPoint { get; set; } = new();
    public StoneWireframePointResponse WorldTopEndPoint { get; set; } = new();
}

public sealed class StoneWireframeAssemblyResponse
{
    public string AssemblyDefinitionName { get; set; } = string.Empty;
    public int AssemblyInstanceCount { get; set; }
    public int StoneOccurrenceCount { get; set; }
    public IReadOnlyList<string> StoneDefinitionNames { get; set; } = Array.Empty<string>();
}

public sealed class StoneWireframeBakeFailureResponse
{
    public string Code { get; set; } = string.Empty;
    public string AssemblyDefinitionName { get; set; } = string.Empty;
    public string StoneDefinitionName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class StoneWireframePointResponse
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}
