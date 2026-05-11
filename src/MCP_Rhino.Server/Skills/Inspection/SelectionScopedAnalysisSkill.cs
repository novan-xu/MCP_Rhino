using MCP_Rhino.Server.Application.Services.Analysis;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using MCP_Rhino.Server.Skills.Editing;

namespace MCP_Rhino.Server.Skills.Inspection;

public sealed class SelectionScopedAnalysisSkill
{
    private readonly LiveObjectSelectionSkill _objectSelectionSkill;
    private readonly RhinoGeometryMetricsService _metricsService;
    private readonly RhinoGeometryCurvatureService _curvatureService;
    private readonly RhinoGeometryIntersectionService _intersectionService;

    public SelectionScopedAnalysisSkill(
        LiveObjectSelectionSkill objectSelectionSkill,
        RhinoGeometryMetricsService metricsService,
        RhinoGeometryCurvatureService curvatureService,
        RhinoGeometryIntersectionService intersectionService)
    {
        _objectSelectionSkill = objectSelectionSkill;
        _metricsService = metricsService;
        _curvatureService = curvatureService;
        _intersectionService = intersectionService;
    }

    public OperationResponse<GetObjectMetricsInLiveResponse> GetObjectMetricsByFilter(GetObjectMetricsByFilterRequest request)
    {
        OperationResponse<RhinoObjectFilterResult> selection = ResolveSelection(request);
        if (!selection.Success || selection.Data is null)
        {
            return OperationResponse<GetObjectMetricsInLiveResponse>.Fail(selection.Message);
        }

        OperationResponse<GetObjectMetricsInLiveResponse> analysis = _metricsService.GetObjectMetricsInLive(new GetObjectMetricsInLiveRequest
        {
            FilePath = request.FilePath,
            ObjectIds = GetSelectedObjectIds(selection.Data)
        });

        return WithSelectionMessage(analysis, selection.Data, "Selection-scoped object metrics completed.");
    }

    public OperationResponse<GetMassPropertiesInLiveResponse> GetMassPropertiesByFilter(GetMassPropertiesByFilterRequest request)
    {
        OperationResponse<RhinoObjectFilterResult> selection = ResolveSelection(request);
        if (!selection.Success || selection.Data is null)
        {
            return OperationResponse<GetMassPropertiesInLiveResponse>.Fail(selection.Message);
        }

        OperationResponse<GetMassPropertiesInLiveResponse> analysis = _intersectionService.GetMassPropertiesInLive(new GetMassPropertiesInLiveRequest
        {
            FilePath = request.FilePath,
            Kind = request.Kind,
            ObjectIds = GetSelectedObjectIds(selection.Data)
        });

        return WithSelectionMessage(analysis, selection.Data, "Selection-scoped mass properties completed.");
    }

    public OperationResponse<GetGeometryFramesInLiveResponse> GetGeometryFramesByFilter(GetGeometryFramesByFilterRequest request)
    {
        OperationResponse<RhinoObjectFilterResult> selection = ResolveSelection(request);
        if (!selection.Success || selection.Data is null)
        {
            return OperationResponse<GetGeometryFramesInLiveResponse>.Fail(selection.Message);
        }

        List<GeometryFrameEntryRequest> entries = GetSelectedObjectIds(selection.Data)
            .Select((objectId, index) => new GeometryFrameEntryRequest
            {
                EntryId = CreateEntryId(request.EntryIdPrefix, "frame", index),
                ObjectId = objectId,
                Kind = request.Kind,
                Parameter = request.Parameter,
                U = request.U,
                V = request.V,
                ParameterSpec = request.ParameterSpec
            })
            .ToList();

        OperationResponse<GetGeometryFramesInLiveResponse> analysis = _metricsService.GetGeometryFramesInLive(new GetGeometryFramesInLiveRequest
        {
            FilePath = request.FilePath,
            Entries = entries
        });

        return WithSelectionMessage(analysis, selection.Data, "Selection-scoped geometry frames completed.");
    }

    public OperationResponse<GetCurvatureSamplesInLiveResponse> GetCurvatureSamplesByFilter(GetCurvatureSamplesByFilterRequest request)
    {
        OperationResponse<RhinoObjectFilterResult> selection = ResolveSelection(request);
        if (!selection.Success || selection.Data is null)
        {
            return OperationResponse<GetCurvatureSamplesInLiveResponse>.Fail(selection.Message);
        }

        List<CurvatureSampleEntryRequest> entries = GetSelectedObjectIds(selection.Data)
            .Select((objectId, index) => new CurvatureSampleEntryRequest
            {
                EntryId = CreateEntryId(request.EntryIdPrefix, "curvature", index),
                ObjectId = objectId,
                Mode = request.Mode,
                SampleCount = request.SampleCount,
                StepLength = request.StepLength,
                Parameters = new List<double>(request.Parameters),
                UvSamples = new List<GeometryAnalysisUvSampleRequest>(request.UvSamples)
            })
            .ToList();

        OperationResponse<GetCurvatureSamplesInLiveResponse> analysis = _curvatureService.GetCurvatureSamplesInLive(new GetCurvatureSamplesInLiveRequest
        {
            FilePath = request.FilePath,
            Entries = entries
        });

        return WithSelectionMessage(analysis, selection.Data, "Selection-scoped curvature sampling completed.");
    }

    public OperationResponse<GetContourCurvesInLiveResponse> GetContourCurvesByFilter(GetContourCurvesByFilterRequest request)
    {
        OperationResponse<RhinoObjectFilterResult> selection = ResolveSelection(request);
        if (!selection.Success || selection.Data is null)
        {
            return OperationResponse<GetContourCurvesInLiveResponse>.Fail(selection.Message);
        }

        List<GeometryContourEntryRequest> entries = GetSelectedObjectIds(selection.Data)
            .Select((objectId, index) => new GeometryContourEntryRequest
            {
                EntryId = CreateEntryId(request.EntryIdPrefix, "contour", index),
                ObjectId = objectId,
                StartX = request.StartX,
                StartY = request.StartY,
                StartZ = request.StartZ,
                EndX = request.EndX,
                EndY = request.EndY,
                EndZ = request.EndZ,
                Interval = request.Interval
            })
            .ToList();

        OperationResponse<GetContourCurvesInLiveResponse> analysis = _intersectionService.GetContourCurvesInLive(new GetContourCurvesInLiveRequest
        {
            FilePath = request.FilePath,
            Entries = entries
        });

        return WithSelectionMessage(analysis, selection.Data, "Selection-scoped contour preview completed.");
    }

    private OperationResponse<RhinoObjectFilterResult> ResolveSelection(SelectionScopedAnalysisRequestBase request)
    {
        OperationResponse<RhinoObjectFilterResult> selection = _objectSelectionSkill.Select(CreateSelectionRequest(request));
        if (!selection.Success || selection.Data is null)
        {
            return selection;
        }

        if (selection.Data.MatchedCount == 0)
        {
            return OperationResponse<RhinoObjectFilterResult>.Fail("Filter matched no objects.");
        }

        if (selection.Data.MatchedCount > LiveGeometryAnalysisHelpers.MaxObjectIdsPerRequest)
        {
            return OperationResponse<RhinoObjectFilterResult>.Fail(
                $"Filter matched {selection.Data.MatchedCount} objects, exceeding the 5000-object analysis limit. Refine the filter or split by layer.");
        }

        return selection;
    }

    private static FilterObjectsRequest CreateSelectionRequest(SelectionScopedAnalysisRequestBase request)
    {
        return new FilterObjectsRequest
        {
            FilePath = request.FilePath,
            LayerQueries = new List<string>(request.LayerQueries),
            ConfirmedLayerFullPaths = new List<string>(request.ConfirmedLayerFullPaths),
            ObjectTypes = new List<string>(request.ObjectTypes),
            UserAttributeConditions = new List<UserAttributeConditionRequest>(request.UserAttributeConditions),
            MatchMode = request.MatchMode,
            UserAttributeMatchMode = request.UserAttributeMatchMode
        };
    }

    private static List<Guid> GetSelectedObjectIds(RhinoObjectFilterResult selection)
    {
        return selection.Objects.Select(item => item.ObjectId).ToList();
    }

    private static OperationResponse<T> WithSelectionMessage<T>(
        OperationResponse<T> analysis,
        RhinoObjectFilterResult selection,
        string message)
        where T : class
    {
        if (!analysis.Success || analysis.Data is null)
        {
            return OperationResponse<T>.Fail(analysis.Message);
        }

        return OperationResponse<T>.Ok(
            analysis.Data,
            $"{message} Resolved {selection.MatchedCount} objects server-side from filter: {selection.CriteriaSummary}");
    }

    private static string CreateEntryId(string? prefix, string fallbackPrefix, int index)
    {
        string normalizedPrefix = string.IsNullOrWhiteSpace(prefix)
            ? fallbackPrefix
            : prefix.Trim();

        return $"{normalizedPrefix}-{index + 1}";
    }
}
