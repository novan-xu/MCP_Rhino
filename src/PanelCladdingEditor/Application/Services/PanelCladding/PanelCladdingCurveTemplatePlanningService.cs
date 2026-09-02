using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed class PanelCladdingCurveTemplatePlanningService
{
    private readonly PanelCladdingKeyService _keys;

    public PanelCladdingCurveTemplatePlanningService(PanelCladdingKeyService keys)
    {
        _keys = keys;
    }

    public OperationResponse<PanelCladdingCurveTemplatePlan> CreatePlan(
        IReadOnlyList<PanelCladdingCurveTemplatePanelSnapshot> panels,
        PanelCladdingCurveTemplatePriority priority)
    {
        PanelCladdingCurveTemplatePanelSnapshot[] distinctPanels = (panels ??
                Array.Empty<PanelCladdingCurveTemplatePanelSnapshot>())
            .Where(panel => panel.ObjectId != Guid.Empty)
            .GroupBy(panel => panel.ObjectId)
            .Select(group => group.First())
            .ToArray();
        if (distinctPanels.Length == 0)
        {
            return OperationResponse<PanelCladdingCurveTemplatePlan>.Fail(
                "PANEL_CLADDING_CURVE_TEMPLATE_SELECTION_REQUIRED");
        }
        if (!Enum.IsDefined(priority))
        {
            return OperationResponse<PanelCladdingCurveTemplatePlan>.Fail(
                "PANEL_CLADDING_CURVE_TEMPLATE_PRIORITY_INVALID");
        }

        var panelPlans = new List<PanelCladdingCurveTemplatePanelPlan>(distinctPanels.Length);
        foreach (PanelCladdingCurveTemplatePanelSnapshot panel in distinctPanels)
        {
            OperationResponse<PanelCladdingCurveTemplatePanelPlan> planned = CreatePanelPlan(panel, priority);
            if (!planned.Success || planned.Data is null)
            {
                return OperationResponse<PanelCladdingCurveTemplatePlan>.Fail(
                    $"PANEL_CLADDING_CURVE_TEMPLATE_PANEL_INVALID: {panel.ObjectId:D}: {planned.Message}");
            }
            panelPlans.Add(planned.Data);
        }

        return OperationResponse<PanelCladdingCurveTemplatePlan>.Ok(new PanelCladdingCurveTemplatePlan
        {
            Priority = priority,
            Panels = panelPlans
        });
    }

    private OperationResponse<PanelCladdingCurveTemplatePanelPlan> CreatePanelPlan(
        PanelCladdingCurveTemplatePanelSnapshot panel,
        PanelCladdingCurveTemplatePriority priority)
    {
        if (panel.HorizontalTrackCount < 0 || panel.VerticalTrackCount < 0)
        {
            return OperationResponse<PanelCladdingCurveTemplatePanelPlan>.Fail(
                "PANEL_CLADDING_CURVE_TEMPLATE_TRACK_COUNT_INVALID");
        }
        if (panel.HasMergeMask)
        {
            return OperationResponse<PanelCladdingCurveTemplatePanelPlan>.Fail(
                "PANEL_CLADDING_CURVE_TEMPLATE_MERGE_MASK_ALREADY_CONFIGURED");
        }

        PanelCladdingTopologyAxis axis = priority == PanelCladdingCurveTemplatePriority.Horizontal
            ? PanelCladdingTopologyAxis.Horizontal
            : PanelCladdingTopologyAxis.Vertical;
        int trackCount = axis == PanelCladdingTopologyAxis.Horizontal
            ? panel.HorizontalTrackCount
            : panel.VerticalTrackCount;
        int bayCount = axis == PanelCladdingTopologyAxis.Horizontal
            ? panel.VerticalTrackCount + 1
            : panel.HorizontalTrackCount + 1;
        IReadOnlyList<PanelCladdingMergeRun> runs = BuildRuns(
            axis,
            trackCount,
            bayCount,
            panel.Topology ?? new PanelCladdingTopologyState());
        var topology = new PanelCladdingTopologyState
        {
            MissingSegments = panel.Topology?.MissingSegments ??
                Array.Empty<PanelCladdingSegmentCoordinate>(),
            HiddenSegments = panel.Topology?.HiddenSegments ??
                Array.Empty<PanelCladdingSegmentCoordinate>(),
            MergeRuns = runs
        };
        OperationResponse<IReadOnlyDictionary<string, string>> encoded =
            _keys.EncodeNonDefaultTopology(
                topology,
                panel.HorizontalTrackCount,
                panel.VerticalTrackCount);
        if (!encoded.Success || encoded.Data is null)
        {
            return OperationResponse<PanelCladdingCurveTemplatePanelPlan>.Fail(encoded.Message);
        }

        return OperationResponse<PanelCladdingCurveTemplatePanelPlan>.Ok(
            new PanelCladdingCurveTemplatePanelPlan
            {
                ObjectId = panel.ObjectId,
                MergeMask = encoded.Data.TryGetValue(
                    PanelCladdingKeyService.MergeMaskKey,
                    out string? mergeMask)
                    ? mergeMask
                    : string.Empty,
                UserTextDeletes = runs.Count > 0
                    ? new[]
                    {
                        PanelCladdingKeyService.SignatureKey,
                        PanelCladdingKeyService.LegacySignatureKey
                    }
                    : Array.Empty<string>(),
                MergeRuns = runs
            });
    }

    private static IReadOnlyList<PanelCladdingMergeRun> BuildRuns(
        PanelCladdingTopologyAxis axis,
        int trackCount,
        int bayCount,
        PanelCladdingTopologyState topology)
    {
        HashSet<PanelCladdingSegmentCoordinate> missing = topology.MissingSegments.ToHashSet();
        HashSet<PanelCladdingSegmentCoordinate> hidden = topology.HiddenSegments.ToHashSet();
        var runs = new List<PanelCladdingMergeRun>();

        for (int track = 0; track < trackCount; track++)
        {
            int start = -1;
            bool runHidden = false;
            for (int bay = 0; bay <= bayCount; bay++)
            {
                var coordinate = new PanelCladdingSegmentCoordinate(axis, track, bay);
                bool present = bay < bayCount && !missing.Contains(coordinate);
                bool isHidden = present && hidden.Contains(coordinate);
                if (start >= 0 && (!present || isHidden != runHidden))
                {
                    AddRunIfMergeable(runs, axis, track, start, bay - 1);
                    start = -1;
                }
                if (present && start < 0)
                {
                    start = bay;
                    runHidden = isHidden;
                }
            }
        }

        return runs;
    }

    private static void AddRunIfMergeable(
        ICollection<PanelCladdingMergeRun> runs,
        PanelCladdingTopologyAxis axis,
        int track,
        int startBay,
        int endBay)
    {
        if (endBay > startBay)
        {
            runs.Add(new PanelCladdingMergeRun(axis, track, startBay, endBay));
        }
    }
}
