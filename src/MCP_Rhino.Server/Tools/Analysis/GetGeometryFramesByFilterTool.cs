using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Skills.Inspection;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Analysis;

[McpServerToolType]
public sealed class GetGeometryFramesByFilterTool
{
    private readonly SelectionScopedAnalysisSkill _analysisSkill;

    public GetGeometryFramesByFilterTool(SelectionScopedAnalysisSkill analysisSkill)
    {
        _analysisSkill = analysisSkill;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Inspect live curve or surface frames for objects resolved server-side from layer, type, and/or user attribute filters. Applies one common frame recipe to all matched objects to avoid large per-object entry payloads.")]
    public OperationResponse<GetGeometryFramesInLiveResponse> GetGeometryFramesByFilter(
        string filePath,
        GeometryFrameKind kind = GeometryFrameKind.SurfaceFrame,
        double? parameter = null,
        double? u = null,
        double? v = null,
        GeometryFrameParameterSpecRequest? parameterSpec = null,
        string? entryIdPrefix = null,
        List<string>? layerQueries = null,
        List<string>? confirmedLayerFullPaths = null,
        List<string>? objectTypes = null,
        List<UserAttributeConditionRequest>? userAttributeConditions = null,
        FilterMatchMode matchMode = FilterMatchMode.All,
        FilterMatchMode userAttributeMatchMode = FilterMatchMode.All)
    {
        return _analysisSkill.GetGeometryFramesByFilter(new GetGeometryFramesByFilterRequest
        {
            FilePath = filePath,
            EntryIdPrefix = entryIdPrefix ?? "frame",
            Kind = kind,
            Parameter = parameter,
            U = u,
            V = v,
            ParameterSpec = parameterSpec,
            LayerQueries = layerQueries ?? new List<string>(),
            ConfirmedLayerFullPaths = confirmedLayerFullPaths ?? new List<string>(),
            ObjectTypes = objectTypes ?? new List<string>(),
            UserAttributeConditions = userAttributeConditions ?? new List<UserAttributeConditionRequest>(),
            MatchMode = matchMode,
            UserAttributeMatchMode = userAttributeMatchMode
        });
    }
}
