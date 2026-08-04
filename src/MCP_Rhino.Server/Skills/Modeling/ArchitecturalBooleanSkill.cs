using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Skills.Modeling;

public sealed class ArchitecturalBooleanSkill
{
    private readonly RhinoArchitecturalBooleanService _service;

    public ArchitecturalBooleanSkill(RhinoArchitecturalBooleanService service)
    {
        _service = service;
    }

    public OperationResponse<ArchitecturalBooleanPreviewResponse> Preview(PreviewBooleanObjectsRequest request)
    {
        return _service.Preview(request.FilePath, request.Entries.Select(entry => MapBoolean(entry, request.Tolerance)).ToList());
    }

    public OperationResponse<ArchitecturalBooleanApplyResponse> Apply(ApplyBooleanObjectsRequest request)
    {
        return _service.Apply(request.FilePath, request.Entries.Select(entry => MapBoolean(entry, request.Tolerance)).ToList());
    }

    public OperationResponse<ArchitecturalBooleanPreviewResponse> PreviewOpenings(PreviewOpeningsRequest request)
    {
        return _service.PreviewOpenings(request.FilePath, request.Items.Select(item => MapOpening(item, request.Tolerance)).ToList());
    }

    public OperationResponse<ArchitecturalBooleanApplyResponse> ApplyOpenings(ApplyOpeningsRequest request)
    {
        return _service.ApplyOpenings(
            request.FilePath,
            request.Items.Select(item => MapOpening(item, request.Tolerance)).ToList(),
            ArchitecturalPrimitiveCreationSkill.MapAttributes(request.Common, request.Metadata, request.AutoCreateLayers));
    }

    internal static ArchitecturalBooleanOperationSpec MapBoolean(BooleanOperationEntryRequest request, double tolerance)
    {
        return new ArchitecturalBooleanOperationSpec
        {
            Operation = request.Operation,
            TargetObjectIds = request.TargetObjectIds,
            CutterObjectIds = request.CutterObjectIds,
            DeleteTargets = request.DeleteTargets,
            DeleteCutters = request.DeleteCutters,
            Tolerance = tolerance
        };
    }

    internal static ArchitecturalOpeningOperationSpec MapOpening(OpeningItemRequest request, double tolerance)
    {
        return new ArchitecturalOpeningOperationSpec
        {
            TargetObjectId = request.TargetObjectId,
            OpeningKind = request.Kind,
            CenterX = request.CenterX,
            CenterY = request.CenterY,
            CenterZ = request.CenterZ,
            Width = request.Width,
            Height = request.Height,
            Depth = request.Depth,
            Radius = request.Radius,
            RetainCutter = request.RetainCutter,
            Tolerance = tolerance
        };
    }
}

