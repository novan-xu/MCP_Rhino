using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Skills.Inspection;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Analysis;

[McpServerToolType]
public sealed class GetCurvatureSamplesByFilterTool
{
    private readonly SelectionScopedAnalysisSkill _analysisSkill;

    public GetCurvatureSamplesByFilterTool(SelectionScopedAnalysisSkill analysisSkill)
    {
        _analysisSkill = analysisSkill;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Sample live curve or surface curvature for objects resolved server-side from layer, type, and/or user attribute filters. Applies one common curvature recipe to all matched objects to avoid large per-object entry payloads.")]
    public OperationResponse<GetCurvatureSamplesInLiveResponse> GetCurvatureSamplesByFilter(
        string filePath,
        GeometryCurvatureSamplingMode mode = GeometryCurvatureSamplingMode.EvenByCount,
        int sampleCount = 10,
        double stepLength = 0d,
        List<double>? parameters = null,
        List<GeometryAnalysisUvSampleRequest>? uvSamples = null,
        string? entryIdPrefix = null,
        List<string>? layerQueries = null,
        List<string>? confirmedLayerFullPaths = null,
        List<string>? objectTypes = null,
        List<UserAttributeConditionRequest>? userAttributeConditions = null,
        FilterMatchMode matchMode = FilterMatchMode.All,
        FilterMatchMode userAttributeMatchMode = FilterMatchMode.All)
    {
        return _analysisSkill.GetCurvatureSamplesByFilter(new GetCurvatureSamplesByFilterRequest
        {
            FilePath = filePath,
            EntryIdPrefix = entryIdPrefix ?? "curvature",
            Mode = mode,
            SampleCount = sampleCount,
            StepLength = stepLength,
            Parameters = parameters ?? new List<double>(),
            UvSamples = uvSamples ?? new List<GeometryAnalysisUvSampleRequest>(),
            LayerQueries = layerQueries ?? new List<string>(),
            ConfirmedLayerFullPaths = confirmedLayerFullPaths ?? new List<string>(),
            ObjectTypes = objectTypes ?? new List<string>(),
            UserAttributeConditions = userAttributeConditions ?? new List<UserAttributeConditionRequest>(),
            MatchMode = matchMode,
            UserAttributeMatchMode = userAttributeMatchMode
        });
    }
}
