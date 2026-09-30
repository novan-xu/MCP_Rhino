using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

const string pid = "PID_RELEASE_01";
const string cid = "CID_RELEASE_01-P";
const string releaseKey = PanelCladdingSpawnPlanningService.ReleaseUserTextKey;
Check(releaseKey == "CW_1.05_LOT", "Curve metadata must use the canonical lot key");
const string layer = "01_CW Panels::Surfaces-PNL::WT01";
var keys = new PanelCladdingKeyService();
var baseGrid = Required(keys.CreateKeySet([25d], [40d], new Dictionary<string, string>(), 100, 80, 0.001));
var mergedGrid = new PanelCladdingKeySet
{
    HorizontalOffsets = baseGrid.HorizontalOffsets,
    VerticalOffsets = baseGrid.VerticalOffsets,
    Cells = baseGrid.Cells,
    Topology = new PanelCladdingTopologyState
    {
        MergeRuns = [new(PanelCladdingTopologyAxis.Horizontal, 0, 0, 1)]
    }
};
var extrusion = new PanelCladdingExtrusionPlanningService();
var curves = Required(extrusion.CreatePlan(pid, cid, 100, 80, mergedGrid, layer, " 007 "));
Check(curves.Select(curve => curve.Kind).Distinct().Count() == 3, "Fixture must include frame, atomic, and merged curves");
Check(curves.All(curve => curve.UserTextWrites[releaseKey] == "007"), "Every curve kind inherits text release, retaining zeros");
Check(curves.All(curve => !curve.UserTextWrites.ContainsKey("CW_1.05_RELEASE")), "Curves must not write the former release key");
Check(curves.All(curve => curve.Cid.StartsWith(cid + "-", StringComparison.Ordinal)), "Role CID must remain unchanged");
foreach (string? absent in new string?[] { null, "", "  " })
{
    var withoutRelease = Required(extrusion.CreatePlan(pid, cid, 100, 80, mergedGrid, layer, absent));
    Check(withoutRelease.All(curve => !curve.UserTextWrites.ContainsKey(releaseKey)), "Missing panel release must not fabricate curve metadata");
}
Console.WriteLine("[OK] frame, intermediate, and merged curve plans inherit release text; missing release remains absent");

var panelText = new Dictionary<string, string>(StringComparer.Ordinal)
{
    [PanelCladdingSpawnPlanningService.PanelIdUserTextKey] = pid,
    [PanelCladdingSpawnPlanningService.CidUserTextKey] = cid,
    [PanelCladdingSpawnPlanningService.WallTypeUserTextKey] = "WT01",
    [releaseKey.ToLowerInvariant()] = " 007 ",
    ["CW_1.05_RELEASE"] = "OLD-IGNORED",
    ["parent"] = "1",
    ["CW_4.00_CLADDING_0A"] = "GL01"
};
var grid = Required(keys.CreateKeySet([], [], panelText, 100, 80, 0.001));
var combined = Required(new PanelCladdingSpawnPlanningService(keys)
    .CreatePlan(panelText, grid, 100, 80, layer));
Check(combined.Curves.All(curve => curve.UserTextWrites[releaseKey] == "007"), "Combined spawn passes panel release to curves");
Check(combined.Regions.Single().UserTextWrites[releaseKey.ToLowerInvariant()] == "007", "Existing surface release behavior preserved");
Console.WriteLine("[OK] combined spawn reads canonical release key case-insensitively and preserves surface behavior");

foreach (PanelCladdingObjectScope scope in Enum.GetValues<PanelCladdingObjectScope>())
{
    foreach ((string? sourceRelease, string existingRelease, bool expectedChange) in new[]
    {
        (" 007 ", "", true), ("007", "006", true), ("007", "007", false),
        ("007", " 007 ", true), ("", "006", true), ((string?)null, "006", true), ((string?)null, "", false)
    })
    {
        var text = new Dictionary<string, string>(panelText, StringComparer.Ordinal);
        text.Remove(releaseKey.ToLowerInvariant());
        if (sourceRelease is not null) text[releaseKey.ToLowerInvariant()] = sourceRelease;
        Guid panelObjectId = Guid.NewGuid();
        var layout = new PanelCladdingLayout
        {
            ObjectId = panelObjectId, Width = 100, Height = 80, ModelTolerance = 0.001,
            Cells = grid.Cells, SourceUserText = text
        };
        string desiredCode = "FRM_0";
        var sync = Required(new PanelCladdingSurfaceSyncPlanningService(keys).CreatePlan(
            new PanelCladdingSurfaceSyncSnapshot
            {
                Scope = scope, SelectedPanelIds = [panelObjectId],
                Panels = [new PanelCladdingSurfaceSyncPanelSnapshot
                {
                    ObjectId = panelObjectId, PanelId = pid, PanelCid = cid, Layout = layout
                }],
                Surfaces = [new PanelCladdingSurfaceSyncSurfaceSnapshot
                {
                    ObjectId = Guid.NewGuid(), PanelObjectId = panelObjectId, PanelId = pid,
                    Cid = cid + "-0A", LayerPath = "04_STEP Surfaces::Surfaces-Glass::GL01",
                    CladdingValue = "GL01", CoveredCellLabels = ["0A"]
                }],
                Curves = [new PanelCladdingSurfaceSyncCurveSnapshot
                {
                    ObjectId = Guid.NewGuid(), PanelObjectId = panelObjectId, PanelId = pid,
                    Cid = cid + "-" + desiredCode, CurveCode = desiredCode, DesiredCode = desiredCode,
                    ReleaseNumber = existingRelease, UsesObjectColor = true
                }]
            }));
        Check(sync.Issues.Count == 0, string.Join(";", sync.Issues.Select(issue => issue.Message)));
        var planned = sync.Curves.Single();
        Check(planned.DesiredReleaseNumber == (sourceRelease?.Trim() ?? ""), "Sync release must come from panel");
        Check(planned.MetadataChanged == expectedChange, $"Release-only change detection failed: {scope}/{sourceRelease}/{existingRelease}");
        Check(planned.DesiredCid == cid + "-FRM_0", "Sync preserves role CID");
    }
}
Console.WriteLine("[OK] both sync scopes repair missing/stale release, preserve current values, and clear removed source release");

static T Required<T>(OperationResponse<T> response)
{
    if (!response.Success || response.Data is null) throw new InvalidOperationException(response.Message);
    return response.Data;
}
static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
