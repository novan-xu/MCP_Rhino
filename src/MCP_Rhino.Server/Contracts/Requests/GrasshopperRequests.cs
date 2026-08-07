using MCP_Rhino.Server.Domain.Models.Grasshopper;

namespace MCP_Rhino.Server.Contracts.Requests;

public class GrasshopperEngineRequest
{
    public string FilePath { get; init; } = string.Empty;
    public GrasshopperEngine Engine { get; init; } = GrasshopperEngine.Gh1;
}

public class GrasshopperDefinitionRequest : GrasshopperEngineRequest
{
    public string DefinitionSessionId { get; init; } = string.Empty;
}

public sealed class SearchGrasshopperComponentsRequest : GrasshopperEngineRequest
{
    public string Query { get; init; } = string.Empty;
    public int MaxResults { get; init; } = 25;
}

public sealed class DescribeGrasshopperComponentRequest : GrasshopperEngineRequest
{
    public Guid ComponentGuid { get; init; }
}

public sealed class GetGrasshopperGraphRequest : GrasshopperDefinitionRequest
{
    public int DataSampleSize { get; init; }
}

public sealed class PreviewApplyGrasshopperGraphRequest : GrasshopperDefinitionRequest
{
    public GrasshopperGraphSpec Graph { get; init; } = new();
}

public sealed class ApplyGrasshopperGraphRequest : GrasshopperDefinitionRequest
{
    public GrasshopperGraphSpec Graph { get; init; } = new();
    public string PreviewToken { get; init; } = string.Empty;
}

public sealed class SolveGrasshopperDefinitionRequest : GrasshopperDefinitionRequest
{
    public bool ExpireAllObjects { get; init; } = true;
    public int DataSampleSize { get; init; }
}

public sealed class ApplyClearGrasshopperDefinitionRequest : GrasshopperDefinitionRequest
{
    public string PreviewToken { get; init; } = string.Empty;
}
