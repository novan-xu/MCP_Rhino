using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

const string pid = "PID_BKT_S1_06_14";
const string cid = "CID_BKT_S1_06_14";
const string shortName = "S1_06_14";
var keys = new PanelCladdingKeyService();
foreach (var (input, expected) in new[]
{
    (cid, shortName), (cid + "-P", shortName + "-P"),
    (cid + "-C-0A", shortName + "-C-0A"), (cid + "-P-INT_B1", shortName + "-P-INT_B1"),
    (cid + "-FRM_0", shortName + "-FRM_0"), (" cid_bkt_S1_06_14 ", shortName),
    ("CID_OTHER_S1_06_14", "OTHER_S1_06_14"), ("CUSTOM-CID", "CUSTOM-CID"),
    ("CID_BKT_", "CID_BKT_"), ("", "")
})
    Check(PanelCladdingCidService.ShortName(input) == expected, "Short name: " + input);
foreach (var (type, suffix) in new[] { ("flat", ""), ("corner_parent", "-P"), ("corner_child", "-C") })
{
    var text = Text(type);
    text[PanelCladdingSpawnPlanningService.CidUserTextKey] = "CID_STALE";
    Check(PanelCladdingCidService.PanelNameWrite(text) == shortName + suffix, "Name follows corrected unit-type CID");
    Check(text[PanelCladdingSpawnPlanningService.CidUserTextKey] == "CID_STALE", "Name planning never mutates metadata");
}
var untyped = Text("");
Check(PanelCladdingCidService.PanelNameWrite(untyped) == shortName, "Untyped panel names use existing CID");
untyped.Remove(PanelCladdingSpawnPlanningService.CidUserTextKey);
Check(PanelCladdingCidService.PanelNameWrite(untyped) is null, "Missing CID leaves panel name alone");
Console.WriteLine("[OK] short panel/surface/curve names, complete suffixes, casing, custom identifiers, and corrected unit-type CID");

foreach (PanelCladdingObjectScope scope in Enum.GetValues<PanelCladdingObjectScope>())
foreach (var (stalePanel, staleSurface, staleCurve) in new[]
{
    (false, false, false), (true, false, false), (false, true, false),
    (false, false, true), (true, true, true)
})
{
    var snapshot = Snapshot(scope, stalePanel, staleSurface, staleCurve);
    var planner = new PanelCladdingSurfaceSyncPlanningService(keys);
    var plan = Required(planner.CreatePlan(snapshot));
    Check(plan.Issues.Count == 0, string.Join(';', plan.Issues.Select(issue => issue.Message)));
    Check(!plan.Panels.Single().CladdingChanged, "Name-only changes must not be classified as cladding edits");
    Check(plan.Panels.Single().NameChanged == stalePanel, "Panel name-only detection");
    Check(plan.Curves.Single().MetadataChanged == staleCurve, "Curve name-only detection and idempotence");
    if (scope == PanelCladdingObjectScope.Surfaces)
    {
        var surface = plan.Surfaces.Single();
        Check(surface.NameChanged == staleSurface && surface.MetadataChanged == staleSurface,
            "Surface name-only detection and idempotence");
        Check(!surface.CidChanged && !surface.PidChanged && !surface.CoverageChanged &&
            !surface.CladdingKeyChanged && !surface.ReleaseChanged, "Full surface identity and material stay intact");
    }
    var repository = new CapturingRepository(snapshot);
    var service = new PanelCladdingSurfaceSyncService(repository, new PanelCladdingTypeSignatureService(keys), planner);
    Required(service.Sync(snapshot.DocumentPath, snapshot.SelectedPanelIds, "", false, scope));
    var commit = repository.LastCommit ?? throw new InvalidOperationException("Commit was not called");
    Check(commit.PanelWrites.Count == (stalePanel ? 1 : 0), "Panel name-only changes must reach live commit");
    Check(commit.SurfaceWrites.Count == (scope == PanelCladdingObjectScope.Surfaces && staleSurface ? 1 : 0),
        "Surface name-only changes must reach live commit in surface scope");
    Check(commit.CurveWrites.Count == (staleCurve ? 1 : 0), "Curve name-only changes must reach live commit");
    Check(plan.Curves.Single().DesiredCid == cid + "-FRM_0", "Full curve CID remains unchanged");
}
Console.WriteLine("[OK] both sync scopes commit isolated/all name-only changes and make no writes when names already match");

// Rhino mutation adapters cannot execute without a native document. Verify the compiled
// production source routes all existing write paths through the shared naming policy.
var root = new DirectoryInfo(AppContext.BaseDirectory);
while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
Check(root is not null, "Repository root");
string Source(string relative) => File.ReadAllText(Path.Combine(root!.FullName, "src/PanelCladdingEditor", relative));
const string live = "Infrastructure/Rhino/Live/PanelCladding/";
var spawnSource = Source(live + "LivePanelCladdingSpawnService.cs");
Check(spawnSource.Contains("ShortName(item.Region.Cid)") && spawnSource.Contains("ShortName(item.CurvePlan.Cid)"),
    "Both spawn families must name from full CID");
Check(Source(live + "LivePanelCladdingUpdateService.cs").Contains("ShortName(dependency.Expected.Cid)"),
    "Update replacements and creations use dependency CID");
var syncSource = Source(live + "LivePanelCladdingSurfaceSyncRepository.cs");
Check(syncSource.Contains("ShortName(surfaceWrite.DesiredCid)") && syncSource.Contains("ShortName(curveWrite.DesiredCid)"),
    "Both sync families must name from full CID");
var createSource = Source(live + "LivePanelCladdingCreateService.cs");
Check(createSource.Contains("identityChanged || !UserTextEquals(original, proposed)"), "PCCreate must commit name-only correction");
foreach (string path in new[] {live + "LivePanelCladdingCreateService.cs", live + "LivePanelCladdingMatchService.cs",
    live + "LivePanelCladdingSurfaceSyncRepository.cs", "Infrastructure/Rhino/LivePanelCladdingRepository.cs"})
    Check(Source(path).Contains("LivePanelCladdingCidService.Normalize("), "Panel identity route: " + path);
var identitySource = Source(live + "LivePanelCladdingCidService.cs");
Check(identitySource.Contains("PanelCladdingCidService.PanelNameWrite(text)") && identitySource.Contains("attributes.Name = name;"),
    "Panel normalization applies name-only corrections");
Console.WriteLine("[OK] live create/save/match/spawn/update/sync naming routes and name-only create commit guard");

Dictionary<string, string> Text(string type) => new()
{
    [PanelCladdingSpawnPlanningService.PanelIdUserTextKey] = pid,
    [PanelCladdingSpawnPlanningService.CidUserTextKey] = cid,
    [PanelCladdingCidService.UnitTypeUserTextKey] = type,
    ["CW_4.00_CLADDING_0A"] = "GL01"
};

PanelCladdingSurfaceSyncSnapshot Snapshot(PanelCladdingObjectScope scope, bool stalePanel, bool staleSurface, bool staleCurve)
{
    var text = Text("flat");
    var grid = Required(keys.CreateKeySet([], [], text, 100d, 80d, 0.001d));
    text[PanelCladdingKeyService.CladdingLogicKey] = Required(new PanelCladdingLogicService().Encode(grid.Cells, text));
    var layout = new PanelCladdingLayout
    {
        ObjectId = Guid.NewGuid(), ObjectName = stalePanel ? "Old panel" : shortName,
        DocumentPath = "object-names-fixture.3dm", GeometryFingerprint = "name-fixture",
        SystemCode = "WT01", Width = 100, Height = 80, ModelTolerance = 0.001,
        Cells = grid.Cells, Topology = grid.Topology, SourceUserText = text
    };
    return new PanelCladdingSurfaceSyncSnapshot
    {
        Scope = scope, DocumentPath = layout.DocumentPath, SelectedPanelIds = [layout.ObjectId],
        Panels = [new PanelCladdingSurfaceSyncPanelSnapshot
        {
            ObjectId = layout.ObjectId, PanelId = pid, PanelCid = cid, Layout = layout
        }],
        Surfaces = [new PanelCladdingSurfaceSyncSurfaceSnapshot
        {
            ObjectId = Guid.NewGuid(), PanelObjectId = layout.ObjectId, PanelId = pid, Cid = cid + "-0A",
            ObjectName = staleSurface ? cid + "-0A" : shortName + "-0A", CladdingValue = "GL01",
            LayerPath = "04_STEP Surfaces::Surfaces-Glass::GL01", CoverageValue = "0A", CoveredCellLabels = ["0A"]
        }],
        Curves = [new PanelCladdingSurfaceSyncCurveSnapshot
        {
            ObjectId = Guid.NewGuid(), PanelObjectId = layout.ObjectId, PanelId = pid, Cid = cid + "-FRM_0",
            ObjectName = staleCurve ? "FRM_0" : shortName + "-FRM_0", CurveCode = "FRM_0", DesiredCode = "FRM_0",
            UsesObjectColor = true, ObjectColor = new(0, 0, 255), DesiredObjectColor = new(0, 0, 255)
        }]
    };
}

static T Required<T>(OperationResponse<T> response) => response.Success && response.Data is not null
    ? response.Data : throw new InvalidOperationException(response.Message);
static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class CapturingRepository(PanelCladdingSurfaceSyncSnapshot snapshot) : ILivePanelCladdingSurfaceSyncRepository
{
    public PanelCladdingSurfaceSyncCommitRequest? LastCommit { get; private set; }
    public OperationResponse<PanelCladdingSurfaceSyncSnapshot> Read(string filePath, IReadOnlyList<Guid> panelObjectIds,
        PanelCladdingObjectScope scope) => OperationResponse<PanelCladdingSurfaceSyncSnapshot>.Ok(snapshot);
    public OperationResponse<PanelCladdingSurfaceSyncResult> Commit(PanelCladdingSurfaceSyncCommitRequest request,
        Func<OperationResponse> finalizeWorkbook, IReadOnlyList<PanelCladdingSurfaceSyncTypeResult> types,
        int matchedSurfaceCount, int matchedCurveCount)
    {
        LastCommit = request;
        return OperationResponse<PanelCladdingSurfaceSyncResult>.Ok(new());
    }
}
