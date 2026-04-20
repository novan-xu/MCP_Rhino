using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using MCP_Rhino.Server.Skills.Editing;

namespace MCP_Rhino.Server.Skills.Modeling;

public sealed class GeometryModificationSkill
{
    private readonly ObjectSelectionSkill _objectSelectionSkill;
    private readonly RhinoObjectFilterService _filterService;
    private readonly RhinoGeometryModificationService _modificationService;

    public GeometryModificationSkill(
        ObjectSelectionSkill objectSelectionSkill,
        RhinoObjectFilterService filterService,
        RhinoGeometryModificationService modificationService)
    {
        _objectSelectionSkill = objectSelectionSkill;
        _filterService = filterService;
        _modificationService = modificationService;
    }

    public OperationResponse<GeometryModificationPreviewResponse> Preview(PreviewTransformObjectsRequest request)
    {
        OperationResponse<RhinoObjectFilterResult> selection = ResolveSelection(
            request.FilePath,
            request.ConfirmedObjectIds,
            request.LayerQueries,
            request.ConfirmedLayerFullPaths,
            request.ObjectTypes,
            request.UserAttributeConditions,
            request.MatchMode,
            request.UserAttributeMatchMode);

        if (!selection.Success || selection.Data is null)
        {
            return OperationResponse<GeometryModificationPreviewResponse>.Fail(selection.Message);
        }

        return _modificationService.Preview(request, selection.Data);
    }

    public OperationResponse<GeometryModificationResponse> Apply(TransformObjectsRequest request)
    {
        OperationResponse<RhinoObjectFilterResult> selection = ResolveSelection(
            request.FilePath,
            request.ConfirmedObjectIds,
            request.LayerQueries,
            request.ConfirmedLayerFullPaths,
            request.ObjectTypes,
            request.UserAttributeConditions,
            request.MatchMode,
            request.UserAttributeMatchMode);

        if (!selection.Success || selection.Data is null)
        {
            return OperationResponse<GeometryModificationResponse>.Fail(selection.Message);
        }

        return _modificationService.Apply(request, selection.Data);
    }

    public OperationResponse<GeometryModificationPreviewResponse> Preview(PreviewDeleteObjectsRequest request)
    {
        OperationResponse<RhinoObjectFilterResult> selection = ResolveSelection(
            request.FilePath,
            request.ConfirmedObjectIds,
            request.LayerQueries,
            request.ConfirmedLayerFullPaths,
            request.ObjectTypes,
            request.UserAttributeConditions,
            request.MatchMode,
            request.UserAttributeMatchMode);

        if (!selection.Success || selection.Data is null)
        {
            return OperationResponse<GeometryModificationPreviewResponse>.Fail(selection.Message);
        }

        return _modificationService.Preview(request, selection.Data);
    }

    public OperationResponse<GeometryModificationResponse> Apply(DeleteObjectsRequest request)
    {
        OperationResponse<RhinoObjectFilterResult> selection = ResolveSelection(
            request.FilePath,
            request.ConfirmedObjectIds,
            request.LayerQueries,
            request.ConfirmedLayerFullPaths,
            request.ObjectTypes,
            request.UserAttributeConditions,
            request.MatchMode,
            request.UserAttributeMatchMode);

        if (!selection.Success || selection.Data is null)
        {
            return OperationResponse<GeometryModificationResponse>.Fail(selection.Message);
        }

        return _modificationService.Apply(request, selection.Data);
    }

    public OperationResponse<GeometryModificationPreviewResponse> Preview(PreviewReplaceGeometryRequest request)
    {
        List<GeometryReplacementSpec> specs = request.Entries.Select(MapReplacement).ToList();
        OperationResponse<RhinoObjectFilterResult> targets = ResolveExplicitObjectIds(
            request.FilePath,
            specs.Select(spec => spec.ObjectId));

        if (!targets.Success || targets.Data is null)
        {
            return OperationResponse<GeometryModificationPreviewResponse>.Fail(targets.Message);
        }

        return _modificationService.PreviewReplace(request.FilePath, specs, targets.Data.Objects);
    }

    public OperationResponse<GeometryModificationResponse> Apply(ReplaceGeometryRequest request)
    {
        List<GeometryReplacementSpec> specs = request.Entries.Select(MapReplacement).ToList();
        OperationResponse<RhinoObjectFilterResult> targets = ResolveExplicitObjectIds(
            request.FilePath,
            specs.Select(spec => spec.ObjectId));

        if (!targets.Success || targets.Data is null)
        {
            return OperationResponse<GeometryModificationResponse>.Fail(targets.Message);
        }

        return _modificationService.ApplyReplace(request.FilePath, specs, targets.Data.Objects);
    }

    public OperationResponse<GeometryModificationPreviewResponse> Preview(PreviewEditControlPointsRequest request)
    {
        List<ControlPointEditSpec> specs = request.Entries.Select(MapControlPointEdit).ToList();
        OperationResponse<RhinoObjectFilterResult> targets = ResolveExplicitObjectIds(
            request.FilePath,
            specs.Select(spec => spec.ObjectId));

        if (!targets.Success || targets.Data is null)
        {
            return OperationResponse<GeometryModificationPreviewResponse>.Fail(targets.Message);
        }

        return _modificationService.PreviewEditControlPoints(request.FilePath, specs, targets.Data.Objects);
    }

    public OperationResponse<GeometryModificationResponse> Apply(EditControlPointsRequest request)
    {
        List<ControlPointEditSpec> specs = request.Entries.Select(MapControlPointEdit).ToList();
        OperationResponse<RhinoObjectFilterResult> targets = ResolveExplicitObjectIds(
            request.FilePath,
            specs.Select(spec => spec.ObjectId));

        if (!targets.Success || targets.Data is null)
        {
            return OperationResponse<GeometryModificationResponse>.Fail(targets.Message);
        }

        return _modificationService.ApplyEditControlPoints(request.FilePath, specs, targets.Data.Objects);
    }

    private OperationResponse<RhinoObjectFilterResult> ResolveSelection(
        string filePath,
        IReadOnlyList<Guid> confirmedObjectIds,
        IReadOnlyList<string> layerQueries,
        IReadOnlyList<string> confirmedLayerFullPaths,
        IReadOnlyList<string> objectTypes,
        IReadOnlyList<UserAttributeConditionRequest> userAttributeConditions,
        Domain.Enums.FilterMatchMode matchMode,
        Domain.Enums.FilterMatchMode userAttributeMatchMode)
    {
        if (confirmedObjectIds.Count > 0)
        {
            return _filterService.ResolveByObjectIds(filePath, confirmedObjectIds);
        }

        if (layerQueries.Count == 0
            && confirmedLayerFullPaths.Count == 0
            && objectTypes.Count == 0
            && userAttributeConditions.Count == 0)
        {
            return OperationResponse<RhinoObjectFilterResult>.Fail("错误：至少需要提供 ConfirmedObjectIds 或筛查条件。");
        }

        return _objectSelectionSkill.Select(new FilterObjectsRequest
        {
            FilePath = filePath,
            LayerQueries = new List<string>(layerQueries),
            ConfirmedLayerFullPaths = new List<string>(confirmedLayerFullPaths),
            ObjectTypes = new List<string>(objectTypes),
            UserAttributeConditions = new List<UserAttributeConditionRequest>(userAttributeConditions),
            MatchMode = matchMode,
            UserAttributeMatchMode = userAttributeMatchMode
        });
    }

    private OperationResponse<RhinoObjectFilterResult> ResolveExplicitObjectIds(string filePath, IEnumerable<Guid> objectIds)
    {
        List<Guid> ids = objectIds.ToList();
        if (ids.Count == 0)
        {
            return OperationResponse<RhinoObjectFilterResult>.Fail("错误：至少需要提供一个 ObjectId。");
        }

        return _filterService.ResolveByObjectIds(filePath, ids);
    }

    private static GeometryReplacementSpec MapReplacement(GeometryReplacementEntryRequest request)
    {
        return new GeometryReplacementSpec
        {
            ObjectId = request.ObjectId,
            Geometry = request.Geometry
        };
    }

    private static ControlPointEditSpec MapControlPointEdit(ControlPointEditEntryRequest request)
    {
        return new ControlPointEditSpec
        {
            ObjectId = request.ObjectId,
            TargetMode = request.TargetMode,
            PointIndex = request.PointIndex,
            UIndex = request.UIndex,
            VIndex = request.VIndex,
            X = request.X,
            Y = request.Y,
            Z = request.Z,
            Weight = request.Weight
        };
    }
}
