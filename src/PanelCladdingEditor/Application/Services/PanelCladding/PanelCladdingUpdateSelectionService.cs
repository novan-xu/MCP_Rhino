using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public static class PanelCladdingUpdateSelectionService
{
    // Sources must carry the resolved CID, including any parent/child role correction.
    public static PanelCladdingUpdateSelection CreatePlan(IEnumerable<PanelCladdingUpdateSource> sources)
    {
        var groups = sources.DistinctBy(source => source.ObjectId)
            .GroupBy(source => source.PanelCid.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new PanelCladdingUpdateSelection
        {
            ProcessableSources = groups.Where(group => group.Count() == 1)
                .Select(group => group.Single()).ToArray(),
            DuplicateCidGroups = groups.Where(group => group.Count() > 1)
                .Select(group => new PanelCladdingDuplicateCidGroup(
                    group.Key, group.Select(source => source.ObjectId).ToArray())).ToArray()
        };
    }

    public static IReadOnlyList<PanelCladdingUpdateSource> FindDependencyOwners(
        IEnumerable<PanelCladdingUpdateSource> sources,
        string panelId,
        string cid) => sources.Where(source =>
            string.Equals(source.PanelId.Trim(), panelId.Trim(), StringComparison.OrdinalIgnoreCase) &&
            PanelCladdingCidService.IncludesDependency(source.PanelId, source.PanelCid, cid.Trim()))
        .ToArray();
}
