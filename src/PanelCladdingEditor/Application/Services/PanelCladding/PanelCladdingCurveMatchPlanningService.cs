using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed class PanelCladdingCurveMatchPlanningService : IPanelCladdingMatchPlanningService
{
    private readonly PanelCladdingKeyService _keys;

    public PanelCladdingCurveMatchPlanningService(PanelCladdingKeyService keys)
    {
        _keys = keys;
    }

    public OperationResponse<PanelCladdingMatchPlan> CreatePlan(
        PanelCladdingMatchPanelSnapshot source,
        IReadOnlyList<PanelCladdingMatchPanelSnapshot> targets)
    {
        if (source.ObjectId == Guid.Empty)
        {
            return OperationResponse<PanelCladdingMatchPlan>.Fail(
                "PANEL_CLADDING_CURVE_MATCH_SOURCE_REQUIRED");
        }
        PanelCladdingMatchPanelSnapshot[] distinctTargets = (targets ??
                Array.Empty<PanelCladdingMatchPanelSnapshot>())
            .Where(target => target.ObjectId != Guid.Empty)
            .GroupBy(target => target.ObjectId)
            .Select(group => group.First())
            .ToArray();
        if (distinctTargets.Length == 0)
        {
            return OperationResponse<PanelCladdingMatchPlan>.Fail(
                "PANEL_CLADDING_CURVE_MATCH_TARGET_REQUIRED");
        }
        if (distinctTargets.Any(target => target.ObjectId == source.ObjectId))
        {
            return OperationResponse<PanelCladdingMatchPlan>.Fail(
                "PANEL_CLADDING_CURVE_MATCH_SOURCE_IS_TARGET");
        }

        OperationResponse<PanelCladdingKeySet> sourceLayout = Parse(source, "SOURCE");
        if (!sourceLayout.Success || sourceLayout.Data is null)
        {
            return OperationResponse<PanelCladdingMatchPlan>.Fail(sourceLayout.Message);
        }
        OperationResponse<IReadOnlyDictionary<string, string>> sourceMasks =
            _keys.EncodeNonDefaultTopology(
                sourceLayout.Data.Topology,
                sourceLayout.Data.HorizontalOffsets.Count,
                sourceLayout.Data.VerticalOffsets.Count);
        if (!sourceMasks.Success || sourceMasks.Data is null)
        {
            return OperationResponse<PanelCladdingMatchPlan>.Fail(
                $"PANEL_CLADDING_CURVE_MATCH_SOURCE_INVALID: {sourceMasks.Message}");
        }

        var targetPlans = new List<PanelCladdingMatchTargetPlan>(distinctTargets.Length);
        foreach (PanelCladdingMatchPanelSnapshot target in distinctTargets)
        {
            OperationResponse<PanelCladdingKeySet> targetLayout = Parse(target, "TARGET");
            if (!targetLayout.Success || targetLayout.Data is null)
            {
                return OperationResponse<PanelCladdingMatchPlan>.Fail(
                    $"{targetLayout.Message}: {target.ObjectId:D}");
            }
            if (sourceLayout.Data.HorizontalOffsets.Count != targetLayout.Data.HorizontalOffsets.Count ||
                sourceLayout.Data.VerticalOffsets.Count != targetLayout.Data.VerticalOffsets.Count)
            {
                return OperationResponse<PanelCladdingMatchPlan>.Fail(
                    $"PANEL_CLADDING_CURVE_MATCH_LAYOUT_MISMATCH: {target.ObjectId:D}: " +
                    $"source={sourceLayout.Data.HorizontalOffsets.Count}H/{sourceLayout.Data.VerticalOffsets.Count}V, " +
                    $"target={targetLayout.Data.HorizontalOffsets.Count}H/{targetLayout.Data.VerticalOffsets.Count}V");
            }

            string[] deletes = target.UserText.Keys
                .Where(key => PanelCladdingKeyService.IsTopologyKey(key) ||
                    string.Equals(
                        key,
                        PanelCladdingKeyService.SignatureKey,
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        key,
                        PanelCladdingKeyService.LegacySignatureKey,
                        StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            targetPlans.Add(new PanelCladdingMatchTargetPlan
            {
                ObjectId = target.ObjectId,
                UserTextDeletes = deletes,
                UserTextWrites = new Dictionary<string, string>(
                    sourceMasks.Data,
                    StringComparer.OrdinalIgnoreCase)
            });
        }

        return OperationResponse<PanelCladdingMatchPlan>.Ok(new PanelCladdingMatchPlan
        {
            SourceObjectId = source.ObjectId,
            Targets = targetPlans
        });
    }

    private OperationResponse<PanelCladdingKeySet> Parse(
        PanelCladdingMatchPanelSnapshot panel,
        string role)
    {
        if (panel.Geometry.GeometryClass == PanelGeometryClass.UnsupportedProjection)
        {
            return OperationResponse<PanelCladdingKeySet>.Fail(
                $"PANEL_CLADDING_CURVE_MATCH_{role}_INVALID: unsupported panel projection");
        }
        OperationResponse<PanelCladdingKeySet> parsed = _keys.Parse(
            panel.UserText,
            panel.Geometry.Width,
            panel.Geometry.Height,
            panel.Geometry.ModelTolerance);
        return !parsed.Success || parsed.Data is null
            ? OperationResponse<PanelCladdingKeySet>.Fail(
                $"PANEL_CLADDING_CURVE_MATCH_{role}_INVALID: {parsed.Message}")
            : parsed;
    }
}
