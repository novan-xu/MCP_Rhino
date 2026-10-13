using System.Text.Json;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

var fixtureText = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "tutorial-fixture.json"));
var fixture = JsonSerializer.Deserialize<Fixture[]>(fixtureText,
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
var context = fixture.Select(p => new PanelCladdingPidPanel(p.ObjectId, p.Normal, p.Bounds, p.UserText)).ToArray();
Guid Label(string label) => fixture.Single(p => p.Label == label).ObjectId;
var planner = new PanelCladdingPidPlanningService();
var request = new PanelCladdingPidRequest("BKT", context.Select(p => p.ObjectId).ToArray(), Label("1"), Label("1"));
var full = Required(Plan(context, request));
var fullById = full.Panels.ToDictionary(p => p.ObjectId);
string before = JsonSerializer.Serialize(context);

foreach (var panel in context)
{
    var single = Required(Plan(context, request with { PanelObjectIds = [panel.ObjectId] }));
    Check(single.ContextPanelCount == 20 && single.Panels.Count == 1, "Context and write counts are separate");
    Check(single.Panels[0] == fullById[panel.ObjectId], "Singleton must retain full-context address");
}
var subsetIds = new[] { Label("4"), Label("7"), Label("10") };
var partial = Required(Plan(context, request with { PanelObjectIds = subsetIds }));
Check(partial.Panels.Select(p => p.ObjectId).ToHashSet().SetEquals(subsetIds), "Write set equals explicit selection");
Check(partial.Panels.All(p => fullById[p.ObjectId] == p), "Unselected floors, planes and bays retain numbering");
Check(partial.Panels.Single(p => p.ObjectId == Label("4")).Pid == "PID_BKT_N2_02_01", "Unselected N1 defines N2 ordinal");
Check(partial.Panels.Single(p => p.ObjectId == Label("10")).Pid == "PID_BKT_E1_02_03", "Unselected lower/left panels define level02 bay03");
Check(JsonSerializer.Serialize(context) == before, "Neither selected nor unselected snapshot attributes are mutated by planning");
Check(!Plan(context, request with { PanelObjectIds = [Guid.NewGuid()] }).Success, "Out-of-context selection rejected");
Check(!Plan(context, request with { NorthPanelObjectId = Guid.NewGuid() }).Success, "Out-of-context reference rejected");
Console.WriteLine("[OK] all 20 singleton selections and a partial batch retain full-context numbering; only selected IDs enter the write plan");

string root = PanelCladdingPidPlanningService.PanelLayerRoot;
foreach (var path in new[] { root, root + "::WT-01", root + "::Tower::L2::WT-02", root.ToLowerInvariant() + "::nested" })
    Check(PanelCladdingPidPlanningService.IsPanelLayerPath(path), "Include scope: " + path);
foreach (var path in new[] { "", "01_CW Panels", "Surfaces-PNL", root + "-Other", root + "2::WT-01", "Other::" + root, "01_CW Panels::Other::Surfaces-PNL" })
    Check(!PanelCladdingPidPlanningService.IsPanelLayerPath(path), "Exclude scope: " + path);
Console.WriteLine("[OK] exact root, arbitrary nested sublayers and case-insensitive path boundaries; lookalike/unrelated branches excluded");

var stored = context.Select(p => p with { UserText = new Dictionary<string, string>(fullById[p.ObjectId].UserTextWrites) }).ToArray();
var identities = stored.Select(p => new PanelCladdingPidExistingIdentity(p.ObjectId,
    p.UserText["CW_1.01_PID"], p.UserText["CW_1.02_CID"], false)).ToArray();
Check(planner.ValidateDocumentIdentities(partial, stored, identities).Success, "Subset repeat checks unselected stored identities without rewriting them");

// Two unselected objects with an unrelated duplicate PID now fail the whole-scope audit.
var duplicated = stored.Select(p => p.ObjectId == Label("5") || p.ObjectId == Label("6")
    ? p with { UserText = new Dictionary<string, string>(p.UserText) { ["CW_1.01_PID"] = p.ObjectId == Label("5") ? "PID_OLD_DUP" : " pid_old_dup " } }
    : p).ToArray();
var duplicateCheck = planner.ValidateDocumentIdentities(partial, duplicated, []);
Check(!duplicateCheck.Success && duplicateCheck.Message.Contains("duplicate PID", StringComparison.OrdinalIgnoreCase) &&
    duplicateCheck.Message.Contains(Label("5").ToString()) && duplicateCheck.Message.Contains(Label("6").ToString()), "Unselected pair detected with object IDs");

// A selected correction is evaluated as a replacement before checking duplicates.
var repairing = Required(Plan(duplicated, request with { PanelObjectIds = [Label("5")] }));
Check(planner.ValidateDocumentIdentities(repairing, duplicated, []).Success, "Selected correction can resolve an old duplicate");
var newlyConflicting = stored.Select(p => p.ObjectId == Label("5")
    ? p with { UserText = new Dictionary<string, string>(p.UserText) { ["CW_1.01_PID"] = fullById[Label("10")].Pid } }
    : p).ToArray();
Check(!planner.ValidateDocumentIdentities(partial, newlyConflicting, []).Success, "Selected new PID cannot collide with retained unselected PID");
Check(planner.ValidateDocumentIdentities(partial, context, []).Success, "Blank unselected PIDs stay blank and do not count as duplicates");
Console.WriteLine("[OK] effective whole-scope PID audit detects unselected duplicate pairs, permits selected repairs and rejects selected/unselected collisions");

// Context-only geometric duplicates remain ambiguous even if their attributes are blank.
var geometryDuplicate = context.Append(context.Single(p => p.ObjectId == Label("5")) with { ObjectId = Guid.NewGuid() }).ToArray();
Check(!Plan(geometryDuplicate, request with { PanelObjectIds = subsetIds }).Success, "Unselected ambiguous addresses cannot be silently skipped");
Console.WriteLine("PASS: PCpid layer scope and selected-write regression");

OperationResponse<PanelCladdingPidPlan> Plan(IReadOnlyList<PanelCladdingPidPanel> all, PanelCladdingPidRequest selection) =>
    planner.CreatePlan(selection, all, 0.00001, Math.PI / 180);
static T Required<T>(OperationResponse<T> response) => response.Success && response.Data is not null ? response.Data : throw new InvalidOperationException(response.Message);
static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
sealed record Fixture(string Label, Guid ObjectId, PanelPoint3 Normal, PanelPoint3[] Bounds, Dictionary<string, string> UserText);
