using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

const string releaseKey = PanelCladdingSpawnPlanningService.ReleaseUserTextKey;
Check(releaseKey == "CW_1.05_LOT", "Surface metadata must use the canonical lot key");
var keys = new PanelCladdingKeyService();
var planner = new PanelCladdingSurfaceSyncPlanningService(keys);
var spawnPlanner = new PanelCladdingSpawnPlanningService(keys);

foreach (string release in new[] { " 007 ", "R-03A" })
{
    var text = PanelText("PID_RELEASE_01", release);
    var grid = Required(keys.CreateKeySet([], [30d, 70d], text, 100, 80, 0.001));
    foreach (var spawn in new[]
    {
        Required(spawnPlanner.CreatePlan(text, grid)),
        Required(spawnPlanner.CreateUpdatePlan(text, grid))
    })
    {
        Check(spawn.Regions.Count == 2 && spawn.Regions.Any(region => region.Cells.Count == 2),
            "Fixture must contain both merged and single-cell material surfaces");
        Check(spawn.Regions.All(region => region.UserTextWrites[releaseKey.ToLowerInvariant()] == release.Trim()),
            "Every spawned/updated surface must inherit the panel release as text");
        Check(spawn.Regions.All(region => !region.UserTextWrites.ContainsKey("CW_1.05_RELEASE")),
            "Surfaces must not inherit the former release key");
    }
}
Console.WriteLine("[OK] spawn/update plans inherit panel release on merged and single-cell surfaces");

foreach ((string? source, string existing, bool changed) in new[]
{
    (" 007 ", "", true), ("007", "006", true), ("007", "007", false),
    ("007", " 007 ", true), ("R-03A", "R-03", true), ("", "007", true),
    ("  ", "007", true), ((string?)null, "007", true), ((string?)null, "", false)
})
{
    var snapshot = Fixture("PID_RELEASE_01", source, existing);
    var plan = Required(planner.CreatePlan(snapshot));
    Check(plan.Issues.Count == 0, string.Join(";", plan.Issues.Select(issue => issue.Message)));
    Check(plan.Surfaces.Count == 2, "Both material surfaces must be matched");
    Check(plan.Surfaces.All(surface => surface.DesiredReleaseNumber == (source?.Trim() ?? "")),
        "Desired release must come from the owning panel");
    Check(plan.Surfaces.All(surface => surface.ReleaseChanged == changed && surface.MetadataChanged == changed),
        $"Release-only change detection failed for '{source}'/'{existing}'");
    Check(plan.Surfaces.All(surface => !surface.CidChanged && !surface.PidChanged &&
        !surface.CladdingKeyChanged && !surface.CoverageChanged), "Other surface metadata must remain stable");
    Check(plan.Panels.All(panel => !panel.CladdingChanged), "Release-only drift must not change panel types");

    var commit = Commit(snapshot);
    Check(commit.SurfaceWrites.Count == (changed ? 2 : 0), "Release-only changes must reach the commit request");
    Check(commit.PanelWrites.Count == 0 && commit.CurveWrites.Count == 0,
        "Surface release-only changes must not trigger other writes");
    Check(commit.SurfaceWrites.All(surface => surface.DesiredReleaseNumber == (source?.Trim() ?? "")),
        "Committed surface values must use normalized panel release");
}
Console.WriteLine("[OK] sync commits missing/stale/removed release; current values and panel types remain stable");

var first = Fixture("PID_RELEASE_01", "007", "006");
var second = Fixture("PID_RELEASE_02", "012", "012");
var batch = new PanelCladdingSurfaceSyncSnapshot
{
    SelectedPanelIds = [.. first.SelectedPanelIds, .. second.SelectedPanelIds],
    Panels = [.. first.Panels, .. second.Panels],
    Surfaces = [.. first.Surfaces, .. second.Surfaces]
};
var batchPlan = Required(planner.CreatePlan(batch));
Check(batchPlan.Issues.Count == 0 && batchPlan.Surfaces.Count == 4, "Both panels must be planned");
Check(batchPlan.Surfaces.Where(surface => surface.PanelId == "PID_RELEASE_02")
    .All(surface => surface.DesiredReleaseNumber == "012" && !surface.MetadataChanged),
    "Each panel retains authority over its own surfaces");
var batchCommit = Commit(batch);
Check(batchCommit.SurfaceWrites.Count == 2 &&
    batchCommit.SurfaceWrites.All(surface => surface.PanelObjectId == first.Panels[0].ObjectId),
    "Only surfaces owned by the changed panel should be committed");

var curveScope = new PanelCladdingSurfaceSyncSnapshot
{
    Scope = PanelCladdingObjectScope.Curves,
    SelectedPanelIds = first.SelectedPanelIds, Panels = first.Panels, Surfaces = first.Surfaces,
    Curves = [new PanelCladdingSurfaceSyncCurveSnapshot
    {
        ObjectId = Guid.NewGuid(), PanelObjectId = first.Panels[0].ObjectId,
        PanelId = "PID_RELEASE_01", Cid = "CID_RELEASE_01-P-FRM_0",
        CurveCode = "FRM_0", DesiredCode = "FRM_0", ReleaseNumber = "007", UsesObjectColor = true
    }]
};
var scopedCommit = Commit(curveScope);
Check(scopedCommit.Issues.Count == 0 && scopedCommit.SurfaceWrites.Count == 0,
    "Curve-only sync must not update stale surfaces");
Console.WriteLine("[OK] per-panel inheritance and curve-only scope stay isolated");

Dictionary<string, string> PanelText(string pid, string? release)
{
    var text = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [PanelCladdingSpawnPlanningService.PanelIdUserTextKey] = pid,
        [PanelCladdingSpawnPlanningService.CidUserTextKey] = pid.Replace("PID_", "CID_") + "-P",
        [PanelCladdingSpawnPlanningService.WallTypeUserTextKey] = "WT01",
        ["CW_1.05_RELEASE"] = "OLD-IGNORED",
        ["parent"] = "1",
        [PanelCladdingKeyService.GetCellKey(0, "A")] = "GL01",
        [PanelCladdingKeyService.GetCellKey(1, "A")] = "0A",
        [PanelCladdingKeyService.GetCellKey(2, "A")] = "MT01"
    };
    if (release is not null) text[releaseKey.ToLowerInvariant()] = release;
    return text;
}

PanelCladdingSurfaceSyncSnapshot Fixture(string pid, string? release, string existing)
{
    var text = PanelText(pid, "007");
    var grid = Required(keys.CreateKeySet([], [30d, 70d], text, 100, 80, 0.001));
    var spawn = Required(spawnPlanner.CreatePlan(text, grid));
    text.Remove(releaseKey.ToLowerInvariant());
    if (release is not null) text[releaseKey.ToLowerInvariant()] = release;
    text[PanelCladdingKeyService.CladdingLogicKey] = Required(new PanelCladdingLogicService().Encode(
        grid.Cells, grid.Cells.ToDictionary(cell => cell.UserTextKey, cell => cell.Value)));
    Guid panelId = Guid.NewGuid();
    var layout = new PanelCladdingLayout
    {
        ObjectId = panelId, Width = 100, Height = 80, ModelTolerance = 0.001,
        HorizontalOffsets = grid.HorizontalOffsets, VerticalOffsets = grid.VerticalOffsets,
        Cells = grid.Cells, SourceUserText = text
    };
    return new PanelCladdingSurfaceSyncSnapshot
    {
        SelectedPanelIds = [panelId],
        Panels = [new PanelCladdingSurfaceSyncPanelSnapshot
        {
            ObjectId = panelId, PanelId = pid, PanelCid = text[PanelCladdingSpawnPlanningService.CidUserTextKey],
            Layout = layout
        }],
        Surfaces = spawn.Regions.Select(region => new PanelCladdingSurfaceSyncSurfaceSnapshot
        {
            ObjectId = Guid.NewGuid(), PanelObjectId = panelId, PanelId = pid, Cid = region.Cid,
            ReleaseNumber = existing, LayerPath = region.LayerPath, CladdingValue = region.CladdingCode,
            CoverageValue = region.UserTextWrites[PanelCladdingSurfaceCoverageService.UserTextKey],
            CoveredCellLabels = region.Cells.Select(cell => cell.ShortLabel).ToArray()
        }).ToArray()
    };
}

PanelCladdingSurfaceSyncCommitRequest Commit(PanelCladdingSurfaceSyncSnapshot snapshot)
{
    var repository = new CapturingRepository(snapshot);
    var service = new PanelCladdingSurfaceSyncService(repository, new PanelCladdingTypeSignatureService(keys), planner);
    Required(service.Sync("surface-release.3dm", snapshot.SelectedPanelIds, "", false, snapshot.Scope));
    return repository.LastCommit ?? throw new InvalidOperationException("Commit was not called");
}

static T Required<T>(OperationResponse<T> response)
{
    if (!response.Success || response.Data is null) throw new InvalidOperationException(response.Message);
    return response.Data;
}

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class CapturingRepository(PanelCladdingSurfaceSyncSnapshot snapshot) : ILivePanelCladdingSurfaceSyncRepository
{
    public PanelCladdingSurfaceSyncCommitRequest? LastCommit { get; private set; }

    public OperationResponse<PanelCladdingSurfaceSyncSnapshot> Read(string filePath,
        IReadOnlyList<Guid> panelObjectIds, PanelCladdingObjectScope scope) =>
        OperationResponse<PanelCladdingSurfaceSyncSnapshot>.Ok(snapshot);

    public OperationResponse<PanelCladdingSurfaceSyncResult> Commit(PanelCladdingSurfaceSyncCommitRequest request,
        Func<OperationResponse> finalizeWorkbook, IReadOnlyList<PanelCladdingSurfaceSyncTypeResult> types,
        int matchedSurfaceCount, int matchedCurveCount)
    {
        LastCommit = request;
        return OperationResponse<PanelCladdingSurfaceSyncResult>.Ok(new PanelCladdingSurfaceSyncResult());
    }
}
