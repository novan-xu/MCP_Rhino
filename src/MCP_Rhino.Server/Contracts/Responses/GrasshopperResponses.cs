using MCP_Rhino.Server.Domain.Models.Grasshopper;

namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class GrasshopperDefinitionListResponse
{
    public GrasshopperEngine Engine { get; init; }
    public bool RuntimeLoaded { get; init; }
    public List<GrasshopperDefinitionSummary> Definitions { get; init; } = [];
}

public sealed class GrasshopperDefinitionSummary
{
    public string DefinitionSessionId { get; init; } = string.Empty;
    public GrasshopperEngine Engine { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public Guid NativeDocumentId { get; init; }
    public bool IsDefinitionSaved { get; init; }
    public uint RhinoRuntimeSerialNumber { get; init; }
    public bool TargetBindingVerified { get; init; }
    public int ObjectCount { get; init; }
    public int WireCount { get; init; }
    public string SolutionState { get; init; } = string.Empty;
    public string DefinitionRevision { get; init; } = string.Empty;
}

public sealed class GrasshopperComponentSearchResponse
{
    public string Query { get; init; } = string.Empty;
    public int TotalMatches { get; init; }
    public bool Truncated { get; init; }
    public List<GrasshopperComponentSummary> Components { get; init; } = [];
}

public sealed class GrasshopperComponentSummary
{
    public Guid ComponentGuid { get; init; }
    public string Name { get; init; } = string.Empty;
    public string NickName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string SubCategory { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ObjectKind { get; init; } = string.Empty;
    public Guid LibraryGuid { get; init; }
    public string AssemblyName { get; init; } = string.Empty;
    public string AssemblyVersion { get; init; } = string.Empty;
    public bool Hidden { get; init; }
    public bool Obsolete { get; init; }
    public bool IsExecutableCodeComponent { get; init; }
    public string? CodeLanguageOrHost { get; init; }
}

public sealed class GrasshopperComponentDescriptionResponse
{
    public GrasshopperComponentSummary Component { get; init; } = new();
    public List<GrasshopperParameterDescription> Inputs { get; init; } = [];
    public List<GrasshopperParameterDescription> Outputs { get; init; } = [];
}

public sealed class GrasshopperParameterDescription
{
    public int Index { get; init; }
    public string Name { get; init; } = string.Empty;
    public string NickName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Access { get; init; } = string.Empty;
    public bool Optional { get; init; }
    public string TypeName { get; init; } = string.Empty;
}

public sealed class GrasshopperGraphResponse
{
    public string DefinitionSessionId { get; init; } = string.Empty;
    public string DefinitionRevision { get; init; } = string.Empty;
    public List<GrasshopperGraphNode> Nodes { get; init; } = [];
    public List<GrasshopperGraphWire> Wires { get; init; } = [];
    public List<GrasshopperObjectDiagnostic> Diagnostics { get; init; } = [];
    public bool DataTruncated { get; init; }
}

public sealed class GrasshopperGraphNode
{
    public Guid ObjectId { get; init; }
    public Guid ComponentGuid { get; init; }
    public string Name { get; init; } = string.Empty;
    public string NickName { get; init; } = string.Empty;
    public string ObjectKind { get; init; } = string.Empty;
    public float CanvasX { get; init; }
    public float CanvasY { get; init; }
    public bool IsExecutableCodeComponent { get; init; }
    public List<GrasshopperParameterDataSummary> Inputs { get; init; } = [];
    public List<GrasshopperParameterDataSummary> Outputs { get; init; } = [];
}

public sealed class GrasshopperParameterDataSummary
{
    public int Index { get; init; }
    public string Name { get; init; } = string.Empty;
    public string NickName { get; init; } = string.Empty;
    public int VolatileDataCount { get; init; }
    public bool Truncated { get; init; }
    public List<string> Samples { get; init; } = [];
}

public sealed class GrasshopperGraphWire
{
    public Guid SourceObjectId { get; init; }
    public int SourceParameterIndex { get; init; }
    public Guid DestinationObjectId { get; init; }
    public int DestinationParameterIndex { get; init; }
}

public sealed class GrasshopperGraphPreviewResponse
{
    public string PreviewToken { get; init; } = string.Empty;
    public string DefinitionRevision { get; init; } = string.Empty;
    public int PredictedNodeAdditions { get; init; }
    public int PredictedWireAdditions { get; init; }
    public List<ResolvedGrasshopperNode> ResolvedNodes { get; init; } = [];
    public List<ResolvedGrasshopperWire> ResolvedWires { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
}

public sealed class GrasshopperGraphApplyResponse
{
    public string DefinitionRevision { get; init; } = string.Empty;
    public Dictionary<string, Guid> CreatedObjectIds { get; init; } = new(StringComparer.Ordinal);
    public int AddedNodeCount { get; init; }
    public int AddedWireCount { get; init; }
    public bool RollbackCompleted { get; init; }
    public GrasshopperSolveResponse? Solve { get; init; }
}

public sealed class GrasshopperSolveResponse
{
    public string CompletionPhase { get; init; } = string.Empty;
    public bool Solved { get; init; }
    public int WarningCount { get; init; }
    public int ErrorCount { get; init; }
    public int FaultCount { get; init; }
    public string DefinitionRevision { get; init; } = string.Empty;
    public List<GrasshopperObjectDiagnostic> Diagnostics { get; init; } = [];
    public GrasshopperGraphResponse? Graph { get; init; }
}

public sealed class GrasshopperObjectDiagnostic
{
    public Guid ObjectId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string NickName { get; init; } = string.Empty;
    public GrasshopperDiagnosticSeverity Severity { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class GrasshopperClearPreviewResponse
{
    public string PreviewToken { get; init; } = string.Empty;
    public string DefinitionRevision { get; init; } = string.Empty;
    public List<Guid> ObjectIds { get; init; } = [];
    public int WireCount { get; init; }
}

public sealed class GrasshopperClearApplyResponse
{
    public string DefinitionRevision { get; init; } = string.Empty;
    public int RemovedObjectCount { get; init; }
    public int RemovedWireCount { get; init; }
}
