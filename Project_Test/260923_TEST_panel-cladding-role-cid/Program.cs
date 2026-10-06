using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

const string pid = "PID_BKT_W1_03_01";
const string cid = "CID_BKT_W1_03_01";
var keys = new PanelCladdingKeyService();
foreach ((string role, string suffix) in new[] { ("corner_parent", "-P"), ("corner_child", "-C"), ("flat", ""), ("", "") })
{
    var text = Text(role);
    if (role.Length > 0) text[PanelCladdingSpawnPlanningService.CidUserTextKey] = "CID_STALE-P";
    // Conflicting legacy flags must not affect any command path, even when type is absent.
    text["parent"] = "1";
    text["child"] = "1";
    var keySet = Required(keys.CreateKeySet([20d, 40d], [50d], text, 100d, 80d, 0.001d));
    var planner = new PanelCladdingSpawnPlanningService(keys);
    var spawn = Required(planner.CreatePlan(text, keySet, 100d, 80d,
        PanelCladdingExtrusionPlanningService.PanelSurfaceLayerRootPath + "::WT01"));
    Check(spawn.Regions.Single(region => region.OwnerCellLabel == "0A").Cid == cid + suffix + "-0A",
        "Exact surface example: " + role);
    Check(spawn.Curves.Any(curve => curve.Cid == cid + suffix + "-INT_B1"), "Exact curve example: " + role);
    Check(spawn.Regions.All(region => region.UserTextWrites[PanelCladdingSpawnPlanningService.PanelIdUserTextKey] == pid),
        "Surface PID must remain unchanged");
    Check(spawn.Curves.All(curve => curve.UserTextWrites[PanelCladdingSpawnPlanningService.PanelIdUserTextKey] == pid),
        "Curve PID must remain unchanged");
    Check(Required(planner.CreateUpdatePlan(text, keySet)).Regions.Select(region => region.Cid)
        .SequenceEqual(spawn.Regions.Select(region => region.Cid)), "Update surface naming");

    foreach (PanelCladdingSaveScope scope in Enum.GetValues<PanelCladdingSaveScope>())
    {
        var repository = new CapturingRepository(Layout(text, keySet));
        var save = new PanelCladdingSaveService(repository, new PanelCladdingTypeSignatureService(keys));
        Required(save.Save(new PanelCladdingSaveRequest
        {
            FilePath = repository.Layout.DocumentPath,
            ObjectId = repository.Layout.ObjectId,
            ExpectedGeometryFingerprint = repository.Layout.GeometryFingerprint,
            CellValues = keySet.Cells.ToDictionary(cell => cell.UserTextKey, cell => cell.Value),
            SystemCode = "WT01",
            Scope = scope
        }));
        var writes = repository.LastCommit!.UserTextWrites;
        Check(role.Length == 0
                ? !writes.ContainsKey(PanelCladdingSpawnPlanningService.CidUserTextKey)
                : writes[PanelCladdingSpawnPlanningService.CidUserTextKey] == cid + suffix,
            "Panel CID persisted by " + scope);
        Check(!writes.ContainsKey(PanelCladdingSpawnPlanningService.PanelIdUserTextKey), "Save preserves PID");
    }

    var create = Required(new PanelCladdingCreatePlanningService(keys).CreatePlan([
        new PanelCladdingCreatePanelSnapshot
        {
            ObjectId = Guid.NewGuid(), XMaximum = 100d, YMaximum = 80d, Tolerance = 0.001d,
            UserText = text,
            Guides = [new PanelCladdingCreateGuideSnapshot
            {
                ObjectId = Guid.NewGuid(), Samples = [new(0, 40, 0), new(100, 40, 0)]
            }]
        }
    ]));
    Check(role.Length == 0
            ? !create.Panels[0].UserTextWrites.ContainsKey(PanelCladdingSpawnPlanningService.CidUserTextKey)
            : create.Panels[0].UserTextWrites[PanelCladdingSpawnPlanningService.CidUserTextKey] == cid + suffix,
        "PCCreate panel CID");

    // Legacy unsuffixed outputs and already migrated outputs must both sync to the same CIDs.
    foreach (string outputSuffix in new[] { "", suffix }.Distinct())
    {
        var layout = Layout(text, keySet);
        var surfaces = keySet.Cells.Select(cell => new PanelCladdingSurfaceSyncSurfaceSnapshot
        {
            ObjectId = Guid.NewGuid(), PanelObjectId = layout.ObjectId, PanelId = pid,
            Cid = cid + outputSuffix + "-" + cell.ShortLabel,
            LayerPath = "04_STEP Surfaces::Surfaces-Glass::GL01", CladdingValue = "GL01"
            // No coverage payload: exercise legacy CID-to-cell resolution as well.
        }).ToArray();
        var curve = new PanelCladdingSurfaceSyncCurveSnapshot
        {
            ObjectId = Guid.NewGuid(), PanelObjectId = layout.ObjectId, PanelId = pid,
            Cid = cid + outputSuffix + "-INT_B1", CurveCode = "INT_B1", DesiredCode = "INT_B1"
        };
        foreach (PanelCladdingObjectScope scope in Enum.GetValues<PanelCladdingObjectScope>())
        {
            var sync = Required(new PanelCladdingSurfaceSyncPlanningService(keys).CreatePlan(
                new PanelCladdingSurfaceSyncSnapshot
                {
                    Scope = scope, SelectedPanelIds = [layout.ObjectId],
                    Panels = [new PanelCladdingSurfaceSyncPanelSnapshot
                    {
                        ObjectId = layout.ObjectId, PanelId = pid,
                        PanelCid = text[PanelCladdingSpawnPlanningService.CidUserTextKey], Layout = layout
                    }],
                    Surfaces = surfaces, Curves = [curve]
                }));
            Check(sync.Issues.Count == 0, string.Join(";", sync.Issues.Select(issue => issue.Message)));
            Check(sync.Curves.Single().DesiredCid == cid + suffix + "-INT_B1", "Sync curve CID");
            Check(sync.Surfaces.All(surface => surface.DesiredCid.StartsWith(cid + suffix + "-")), "Sync surface CID");
            if (role.Length > 0)
            {
                Check(sync.Panels.Single().CladdingChanged, "CID-only panel change must be committed during sync");
            }
        }
    }
    if (role.Length > 0)
    {
        text[PanelCladdingSpawnPlanningService.CidUserTextKey] = cid + suffix;
        Check(PanelCladdingCidService.ResolvePanelCid(pid, text) == cid + suffix, "Repeated execution is idempotent");
        text.Remove(PanelCladdingSpawnPlanningService.CidUserTextKey);
        var missingCid = Required(planner.CreatePlan(text, keySet, 100d, 80d,
            PanelCladdingExtrusionPlanningService.PanelSurfaceLayerRootPath + "::WT01"));
        Check(missingCid.Curves.All(curve => curve.Cid.StartsWith(cid + suffix + "-")), "Typed panel missing CID");
    }
    Console.WriteLine($"[OK] {(role.Length > 0 ? role : "ordinary")}: spawn, update planning, all save scopes, create, and both sync scopes");
}

var mixed = Text("");
mixed["PaReNt"] = " 1 ";
mixed["CHILD"] = "1";
Check(PanelCladdingCidService.RoleSuffix(mixed) == "" && PanelCladdingCidService.PanelCidWrite(mixed) is null &&
    PanelCladdingCidService.ResolvePanelCid(pid, mixed) == cid, "Legacy flags alone cannot activate a role");
mixed["cw_1.06_unit_type"] = "  CORNER_PARENT  ";
Check(PanelCladdingCidService.ResolvePanelCid(pid, mixed) == cid + "-P", "Case-insensitive key/value and whitespace");
mixed[PanelCladdingSpawnPlanningService.CidUserTextKey] = cid + "-P";
mixed["cw_1.06_unit_type"] = "corner_child";
Check(PanelCladdingCidService.ResolvePanelCid(pid, mixed) == cid + "-C", "Role change replaces suffix");
mixed[PanelCladdingSpawnPlanningService.CidUserTextKey] = cid + "-C";
mixed["cw_1.06_unit_type"] = " flat ";
Check(PanelCladdingCidService.PanelCidWrite(mixed) == cid &&
    PanelCladdingCidService.ResolvePanelCid(pid, mixed) == cid, "Flat removes a previous corner suffix despite old flags");
foreach (string unsupported in new[] { "parent", "child", "1", "corner", "corner-parent", "", "   " })
{
    mixed["cw_1.06_unit_type"] = unsupported;
    foreach (string stored in new[] { "CUSTOM-CID", cid + "-P", cid + "-C" })
    {
        mixed[PanelCladdingSpawnPlanningService.CidUserTextKey] = stored;
        Check(PanelCladdingCidService.RoleSuffix(mixed) == "" &&
            PanelCladdingCidService.PanelCidWrite(mixed) is null &&
            PanelCladdingCidService.ResolvePanelCid(pid, mixed) == stored,
            "Unsupported types preserve stored CIDs and never fall back to old flags");
    }
    mixed.Remove(PanelCladdingSpawnPlanningService.CidUserTextKey);
    Check(PanelCladdingCidService.ResolvePanelCid(pid, mixed) == cid, "Missing CID retains base identity fallback");
}
foreach (string role in new[] { "flat", "corner_parent", "corner_child" })
{
    var noPid = Text(role);
    noPid.Remove(PanelCladdingSpawnPlanningService.PanelIdUserTextKey);
    Check(PanelCladdingCidService.PanelCidWrite(noPid) is null, "No panel CID write without PID");
}
Check(PanelCladdingCidService.IncludesDependency(pid, cid + "-P", cid + "-P-0A"), "Own role is in update scope");
Check(!PanelCladdingCidService.IncludesDependency(pid, cid + "-P", cid + "-C-0A"), "Sibling role excluded from update");
Check(!PanelCladdingCidService.IncludesDependency(pid, cid + "-C", cid + "-P-INT_B1"), "Parent curves excluded from child update");
Check(PanelCladdingCidService.IncludesDependency(pid, cid + "-P", cid + "-0A"), "Legacy output can migrate");
Console.WriteLine("[OK] unit types, ignored legacy flags, casing, role transitions, missing/unknown types, custom IDs, and sibling update isolation");

Dictionary<string, string> Text(string role)
{
    var text = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [PanelCladdingSpawnPlanningService.PanelIdUserTextKey] = pid,
        [PanelCladdingSpawnPlanningService.CidUserTextKey] = cid,
        [PanelCladdingSpawnPlanningService.ReleaseUserTextKey] = "01",
        [PanelCladdingSpawnPlanningService.WallTypeUserTextKey] = "WT01",
        ["CW_4.00_CLADDING_0A"] = "GL01", ["CW_4.01_CLADDING_0B"] = "GL01",
        ["CW_4.02_CLADDING_0C"] = "GL01", ["CW_4.03_CLADDING_1A"] = "GL01",
        ["CW_4.04_CLADDING_1B"] = "GL01", ["CW_4.05_CLADDING_1C"] = "GL01"
    };
    if (role.Length > 0) text["CW_1.06_UNIT_TYPE"] = role;
    return text;
}

PanelCladdingLayout Layout(IReadOnlyDictionary<string, string> text, PanelCladdingKeySet grid) => new()
{
    ObjectId = Guid.NewGuid(), DocumentPath = "role-cid-test.3dm", GeometryFingerprint = "role-cid-fixture",
    SystemCode = "WT01", Width = 100d, Height = 80d, ModelTolerance = 0.001d,
    HorizontalOffsets = grid.HorizontalOffsets, VerticalOffsets = grid.VerticalOffsets,
    Cells = grid.Cells, Topology = grid.Topology, SourceUserText = text
};

static T Required<T>(OperationResponse<T> response)
{
    if (!response.Success || response.Data is null) throw new InvalidOperationException(response.Message);
    return response.Data;
}
static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

internal sealed class CapturingRepository(PanelCladdingLayout layout) : ILivePanelCladdingRepository
{
    internal PanelCladdingLayout Layout { get; } = layout;
    internal PanelAttributeCommitRequest? LastCommit { get; private set; }
    public OperationResponse<PanelCladdingLayout> ReadLayout(string filePath, Guid objectId) =>
        OperationResponse<PanelCladdingLayout>.Ok(Layout);
    public OperationResponse<PanelCladdingMatchPanelSnapshot> ReadMatchPanel(string filePath, Guid objectId) =>
        throw new NotSupportedException();
    public OperationResponse<string> SetWorkbookPath(string filePath, string workbookPath) =>
        throw new NotSupportedException();
    public OperationResponse<PanelAttributeCommitResult> CommitAttributes(PanelAttributeCommitRequest request,
        Func<OperationResponse> finalizeExternalCommit)
    {
        LastCommit = request;
        return OperationResponse<PanelAttributeCommitResult>.Ok(new PanelAttributeCommitResult
        {
            ObjectId = request.ObjectId, Mutated = true
        });
    }
}
