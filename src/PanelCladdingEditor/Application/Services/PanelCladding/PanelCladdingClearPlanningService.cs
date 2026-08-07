using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed class PanelCladdingClearPlanningService
{
    private readonly PanelCladdingKeyService _keys;

    public PanelCladdingClearPlanningService(PanelCladdingKeyService keys)
    {
        _keys = keys;
    }

    public OperationResponse<PanelCladdingClearPlan> CreatePlan(
        IReadOnlyList<PanelCladdingClearPanelSnapshot> panels)
    {
        PanelCladdingClearPanelSnapshot[] distinctPanels = (panels ??
                Array.Empty<PanelCladdingClearPanelSnapshot>())
            .Where(panel => panel.ObjectId != Guid.Empty)
            .GroupBy(panel => panel.ObjectId)
            .Select(group => group.First())
            .ToArray();
        if (distinctPanels.Length == 0)
        {
            return OperationResponse<PanelCladdingClearPlan>.Fail(
                "PANEL_CLADDING_CLEAR_SELECTION_REQUIRED");
        }

        var panelPlans = new List<PanelCladdingClearPanelPlan>(distinctPanels.Length);
        foreach (PanelCladdingClearPanelSnapshot panel in distinctPanels)
        {
            string[] deletes = panel.UserText.Keys
                .Where(_keys.IsClearableCladdingAssignmentKey)
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            panelPlans.Add(new PanelCladdingClearPanelPlan
            {
                ObjectId = panel.ObjectId,
                UserTextDeletes = deletes
            });
        }

        return OperationResponse<PanelCladdingClearPlan>.Ok(new PanelCladdingClearPlan
        {
            Panels = panelPlans
        });
    }
}
