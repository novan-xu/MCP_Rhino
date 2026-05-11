using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Viewport;

[McpServerToolType]
public sealed class CaptureReferenceImageModelingQaViewsTool
{
    private readonly ReferenceImageVisualQaCaptureService _service;

    public CaptureReferenceImageModelingQaViewsTool(ReferenceImageVisualQaCaptureService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Capture bounded live Rhino viewport images, target object summaries, and a checkpoint checklist for reference-image object modeling QA. Use after initial massing and later refinement passes; this does not perform semantic comparison itself.")]
    public OperationResponse<ReferenceImageVisualQaCaptureResponse> CaptureReferenceImageModelingQaViews(
        string filePath,
        ReferenceImageVisualQaCheckpointKind checkpointKind = ReferenceImageVisualQaCheckpointKind.InitialMassing,
        string? referenceImagePath = null,
        string? referenceImageLabel = null,
        List<Guid>? objectIds = null,
        List<string>? viewNames = null,
        ImageSizePxRequest? imageSizePx = null,
        bool backgroundTransparent = false,
        int maxObjectSummaries = 20,
        List<string>? layerQueries = null,
        List<string>? confirmedLayerFullPaths = null,
        List<string>? objectTypes = null,
        List<UserAttributeConditionRequest>? userAttributeConditions = null,
        FilterMatchMode matchMode = FilterMatchMode.All,
        FilterMatchMode userAttributeMatchMode = FilterMatchMode.All)
    {
        return _service.Capture(new CaptureReferenceImageModelingQaViewsRequest
        {
            FilePath = filePath,
            CheckpointKind = checkpointKind,
            ReferenceImagePath = referenceImagePath,
            ReferenceImageLabel = referenceImageLabel,
            ObjectIds = objectIds ?? new List<Guid>(),
            ViewNames = viewNames ?? new List<string>(),
            ImageSizePx = imageSizePx,
            BackgroundTransparent = backgroundTransparent,
            MaxObjectSummaries = maxObjectSummaries,
            LayerQueries = layerQueries ?? new List<string>(),
            ConfirmedLayerFullPaths = confirmedLayerFullPaths ?? new List<string>(),
            ObjectTypes = objectTypes ?? new List<string>(),
            UserAttributeConditions = userAttributeConditions ?? new List<UserAttributeConditionRequest>(),
            MatchMode = matchMode,
            UserAttributeMatchMode = userAttributeMatchMode
        });
    }
}
