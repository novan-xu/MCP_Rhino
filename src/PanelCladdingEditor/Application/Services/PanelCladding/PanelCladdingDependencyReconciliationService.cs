using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed class PanelCladdingDependencyReconciliationService
{
    public OperationResponse<PanelCladdingDependencyReconciliationPlan> CreatePlan(
        IReadOnlyList<PanelCladdingExpectedDependency> expected,
        IReadOnlyList<PanelCladdingExistingDependency> existing)
    {
        PanelCladdingExpectedDependency[] expectedItems = (expected ??
                Array.Empty<PanelCladdingExpectedDependency>())
            .OrderBy(item => item.PanelId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Kind)
            .ThenBy(item => item.Cid, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        PanelCladdingExistingDependency[] existingItems = (existing ??
                Array.Empty<PanelCladdingExistingDependency>())
            .Where(item => item.ObjectId != Guid.Empty)
            .GroupBy(item => item.ObjectId)
            .Select(group => group.First())
            .ToArray();

        PanelCladdingExpectedDependency? invalidExpected = expectedItems.FirstOrDefault(item =>
            item.SourcePanelObjectId == Guid.Empty ||
            string.IsNullOrWhiteSpace(item.PanelId) ||
            string.IsNullOrWhiteSpace(item.Cid));
        if (invalidExpected is not null)
        {
            return OperationResponse<PanelCladdingDependencyReconciliationPlan>.Fail(
                "PANEL_CLADDING_UPDATE_EXPECTED_IDENTITY_INVALID");
        }

        IGrouping<string, PanelCladdingExpectedDependency>? duplicateExpected = expectedItems
            .GroupBy(IdentityKey, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateExpected is not null)
        {
            return OperationResponse<PanelCladdingDependencyReconciliationPlan>.Fail(
                $"PANEL_CLADDING_UPDATE_DUPLICATE_EXPECTED_CID: {duplicateExpected.First().Cid}");
        }

        var existingByIdentity = existingItems
            .Where(item => !string.IsNullOrWhiteSpace(item.PanelId) &&
                !string.IsNullOrWhiteSpace(item.Cid))
            .GroupBy(IdentityKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(item => item.ObjectId.ToString("D"), StringComparer.Ordinal).ToArray(),
                StringComparer.OrdinalIgnoreCase);
        var retained = new HashSet<Guid>();
        var creates = new List<PanelCladdingExpectedDependency>();
        var updates = new List<PanelCladdingDependencyUpdateAction>();

        foreach (PanelCladdingExpectedDependency item in expectedItems)
        {
            if (!existingByIdentity.TryGetValue(IdentityKey(item), out PanelCladdingExistingDependency[]? matches) ||
                matches.Length == 0)
            {
                creates.Add(item);
                continue;
            }

            PanelCladdingExistingDependency retainedMatch = matches[0];
            retained.Add(retainedMatch.ObjectId);
            updates.Add(new PanelCladdingDependencyUpdateAction
            {
                ObjectId = retainedMatch.ObjectId,
                Expected = item
            });
        }

        Guid[] deletes = existingItems
            .Where(item => !retained.Contains(item.ObjectId))
            .Select(item => item.ObjectId)
            .OrderBy(id => id.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        return OperationResponse<PanelCladdingDependencyReconciliationPlan>.Ok(
            new PanelCladdingDependencyReconciliationPlan
            {
                Creates = creates,
                Updates = updates,
                Deletes = deletes
            });
    }

    private static string IdentityKey(PanelCladdingExpectedDependency item) =>
        $"{item.Kind}|{item.PanelId.Trim()}|{item.Cid.Trim()}";

    private static string IdentityKey(PanelCladdingExistingDependency item) =>
        $"{item.Kind}|{item.PanelId.Trim()}|{item.Cid.Trim()}";
}
