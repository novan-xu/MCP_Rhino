using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoLotVisualizationService
{
    private const string GroupPrefix = "MCP_Lot_";
    private static readonly string[] DefaultIneffectiveValues =
    {
        "TBD", "TBC", "N/A", "NA", "NONE", "UNASSIGNED", "NOT ASSIGNED", "-", "?"
    };

    private readonly ILiveRhinoDocumentAccessor _documentAccessor;
    private readonly ILiveLotVisualizationOperator _operator;

    public RhinoLotVisualizationService(
        ILiveRhinoDocumentAccessor documentAccessor,
        ILiveLotVisualizationOperator @operator)
    {
        _documentAccessor = documentAccessor;
        _operator = @operator;
    }

    public OperationResponse<LotVisualizationPreviewResponse> Preview(LotVisualizationScopeRequest request)
    {
        OperationResponse<LotVisualizationSpec> validation = Validate(request);
        if (!validation.Success || validation.Data is null)
        {
            return OperationResponse<LotVisualizationPreviewResponse>.Fail(validation.Message);
        }

        OperationResponse<LotVisualizationPreviewResponse> response = _documentAccessor.Execute(request.FilePath, document =>
        {
            OperationResponse<LotVisualizationPlan> preview = _operator.Preview(document, validation.Data);
            return !preview.Success || preview.Data is null
                ? OperationResponse<LotVisualizationPreviewResponse>.Fail(preview.Message)
                : OperationResponse<LotVisualizationPreviewResponse>.Ok(ToResponse(request.FilePath, preview.Data), preview.Message);
        });

        return response;
    }

    public OperationResponse<LotVisualizationApplyResult> Apply(LotVisualizationScopeRequest request)
    {
        OperationResponse<LotVisualizationSpec> validation = Validate(request);
        if (!validation.Success || validation.Data is null)
        {
            return OperationResponse<LotVisualizationApplyResult>.Fail(validation.Message);
        }

        return _documentAccessor.ExecuteWithUndo(request.FilePath, "MCP: ApplyLotVisualization", document =>
        {
            OperationResponse<LotVisualizationPlan> preview = _operator.Preview(document, validation.Data);
            if (!preview.Success || preview.Data is null)
            {
                return OperationResponse<(bool Mutated, LotVisualizationApplyResult Result)>.Fail(preview.Message);
            }

            OperationResponse<LotVisualizationApplyResult> applied = _operator.Apply(document, preview.Data, GroupPrefix);
            return !applied.Success || applied.Data is null
                ? OperationResponse<(bool Mutated, LotVisualizationApplyResult Result)>.Fail(applied.Message)
                : OperationResponse<(bool Mutated, LotVisualizationApplyResult Result)>.Ok(
                    (applied.Data.ModifiedObjectCount > 0, applied.Data),
                    applied.Message);
        });
    }

    public static LotVisualizationPreviewResponse ToResponse(string filePath, LotVisualizationPlan plan)
    {
        return new LotVisualizationPreviewResponse
        {
            FilePath = filePath,
            TargetObjectCount = plan.TargetObjectIds.Count,
            AssignedObjectCount = plan.Lots.Sum(lot => lot.ObjectIds.Count),
            UnassignedObjectCount = plan.UnassignedObjectIds.Count,
            ResolvedLotNumberKeys = plan.ResolvedLotNumberKeys,
            LayerFullPaths = plan.LayerFullPaths,
            Lots = plan.Lots.Select(lot => new LotVisualizationLotResponse
            {
                LotNumber = lot.LotNumber,
                GroupName = lot.GroupName,
                ObjectCount = lot.ObjectIds.Count,
                R = lot.Color.R,
                G = lot.Color.G,
                B = lot.Color.B
            }).ToList(),
            Warnings = plan.Warnings
        };
    }

    private static OperationResponse<LotVisualizationSpec> Validate(LotVisualizationScopeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return OperationResponse<LotVisualizationSpec>.Fail("FilePath is required.");
        }

        IReadOnlyList<string> keys = (request.LotNumberKeys ?? Array.Empty<string>())
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var ineffective = new HashSet<string>(DefaultIneffectiveValues, StringComparer.OrdinalIgnoreCase);
        foreach (string value in request.IneffectiveLotValues ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                ineffective.Add(value.Trim());
            }
        }

        return OperationResponse<LotVisualizationSpec>.Ok(new LotVisualizationSpec
        {
            ObjectIds = (request.ObjectIds ?? Array.Empty<Guid>()).Where(id => id != Guid.Empty).Distinct().ToList(),
            LotNumberKeys = keys,
            IneffectiveLotValues = ineffective,
            GroupPrefix = GroupPrefix
        });
    }
}
