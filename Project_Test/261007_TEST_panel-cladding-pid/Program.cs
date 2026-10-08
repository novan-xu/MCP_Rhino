using System.Text.Json;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

var fixture = JsonSerializer.Deserialize<Fixture[]>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "tutorial-fixture.json")),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
var panels = fixture.Select(p => new PanelCladdingPidPanel(p.ObjectId, p.Normal, p.Bounds, p.UserText)).ToArray();
Guid Label(string label) => fixture.Single(p => p.Label == label).ObjectId;
var request = new PanelCladdingPidRequest(" bkt ", panels.Select(p => p.ObjectId).ToArray(), Label("1"), Label("1"));
var planner = new PanelCladdingPidPlanningService();
var baseline = Required(Plan(panels));
Check(baseline.Panels.Count == 20 && baseline.Panels.Select(p => p.Pid).Distinct().Count() == 20, "20 unique tutorial PIDs");
var expected = new Dictionary<string, string>
{
    ["1"] = "N1_01_01", ["2"] = "N1_02_01", ["3"] = "N2_01_01", ["4"] = "N2_02_01",
    ["5"] = "E1_01_01", ["6"] = "E1_02_01", ["8"] = "E1_01_02", ["7"] = "E1_02_02",
    ["9"] = "E1_01_03", ["10"] = "E1_02_03",
    ["unlabelled-1"] = "W1_01_01", ["unlabelled-2"] = "W1_02_01",
    ["unlabelled-9"] = "S1_01_01", ["unlabelled-10"] = "S1_02_01",
    ["unlabelled-11"] = "W2_02_01", ["unlabelled-12"] = "W2_01_01",
    ["unlabelled-15"] = "S1_01_02", ["unlabelled-16"] = "S1_02_02",
    ["unlabelled-17"] = "W1_01_02", ["unlabelled-18"] = "W1_02_02"
};
foreach (var pair in expected)
{
    var assigned = baseline.Panels.Single(p => p.ObjectId == Label(pair.Key));
    Check(assigned.Pid == "PID_BKT_" + pair.Value, $"Tutorial panel {pair.Key}: {assigned.Pid}");
    Check(assigned.Cid == "CID_BKT_" + pair.Value, "Tutorial CID");
    Check(assigned.UserTextWrites.Count == 4 && assigned.UserTextWrites[PanelCladdingPidPlanningService.ElevationKey] == assigned.Elevation &&
        assigned.UserTextWrites[PanelCladdingPidPlanningService.LevelKey] == assigned.Level, "Canonical writes");
}
Console.WriteLine("[OK] all 20 live-derived tutorial expectations, including N1/N2, E1 bays 01-03, both levels and four directions");

var random = new Random(79);
for (int i = 0; i < 40; i++) Same(Required(Plan(panels.OrderBy(_ => random.Next()).ToArray())), baseline);
foreach (double angle in new[] { 0.31, Math.PI / 2, -1.72, 2.64 })
{
    PanelPoint3 Rotate(PanelPoint3 p) => new(p.X * Math.Cos(angle) - p.Y * Math.Sin(angle), p.X * Math.Sin(angle) + p.Y * Math.Cos(angle), p.Z);
    var rotated = panels.Select(p => p with { Normal = Rotate(p.Normal), Bounds = p.Bounds.Select(Rotate).ToArray() }).ToArray();
    Same(Required(Plan(rotated)), baseline);
}
var scaled = panels.Select(p => p with { Bounds = p.Bounds.Select(b => new PanelPoint3(b.X * 1000 + 1e7, b.Y * 1000 - 2e7, b.Z * 1000 + 6000)).ToArray() }).ToArray();
Same(Required(Plan(scaled, tolerance: 0.01)), baseline);
var reseeded = panels.Select(p => p with { UserText = new Dictionary<string, string>(baseline.Panels.Single(a => a.ObjectId == p.ObjectId).UserTextWrites) }).ToArray();
Same(Required(Plan(reseeded)), baseline);
Check(panels.All(p => p.UserText.Count == 2 && p.UserText["unrelated"] == "keep"), "Planning never mutates source metadata");
Console.WriteLine("[OK] selection-order independence, rotated project north, translated/scaled geometry and repeat planning");

foreach (var role in new (string Type, string Suffix)[] { ("flat", ""), ("corner_parent", "-P"), (" CORNER_CHILD ", "-C"), ("", ""), ("unknown", "") })
{
    var typed = panels.Select(p => p with { UserText = new Dictionary<string, string>
    {
        ["cw_1.06_unit_type"] = role.Type, ["CW_1.02_CID"] = "CUSTOM_OLD_CID",
        ["CW_1.99_PARENT"] = "TRUE"
    }}).ToArray();
    var plan = Required(Plan(typed));
    Check(plan.Panels.All(p => p.Cid == PanelCladdingCidService.FromPanelId(p.Pid) + role.Suffix), "PCpid recalculates CID with canonical unit roles");
}
var upperReference = Required(Plan(panels, request with { FirstFloorPanelObjectId = Label("2") }));
Check(upperReference.Panels.Single(p => p.ObjectId == Label("2")).Level == "01" &&
    upperReference.Panels.Single(p => p.ObjectId == Label("1")).Level == "00", "Reference floor is the numbering origin");
var easternNorth = Required(Plan(panels, request with { NorthPanelObjectId = Label("5") }));
Check(easternNorth.Panels.Single(p => p.ObjectId == Label("5")).Elevation == "N1" &&
    easternNorth.Panels.Single(p => p.ObjectId == Label("1")).Elevation.StartsWith('W'), "North reference changes project directions");
Console.WriteLine("[OK] flat/parent/child CID regeneration, legacy/custom IDs, and floor/north reference changes");

foreach (string code in new[] { "", "AB", "ABCD", "A1B", "A B", "北AB" })
    Reject(Plan(panels, request with { ProjectCode = code }), "project code");
Reject(Plan(panels, request with { NorthPanelObjectId = Guid.NewGuid() }), "references");
Reject(Plan(panels, request with { FirstFloorPanelObjectId = Guid.NewGuid() }), "references");
Reject(Plan(panels[..^1]), "could not all be read");
Reject(Plan(panels, tolerance: double.NaN), "tolerance");
Reject(Plan(panels, tolerance: -1), "tolerance");
var bad = panels.ToArray();
bad[0] = bad[0] with { Normal = new(0, 0, 1) };
Reject(Plan(bad), "not vertical");
bad[0] = panels[0] with { Normal = new(double.NaN, 0, 0) };
Reject(Plan(bad), "Invalid panel geometry");
bad[0] = panels[0] with { Bounds = panels[0].Bounds.Select((p, i) => i == 0 ? p with { X = p.X + 1 } : p).ToArray() };
Reject(Plan(bad), "not planar");
bad[0] = panels[0] with { Bounds = panels[0].Bounds.Select(p => p with { Z = 0 }).ToArray() };
Reject(Plan(bad), "degenerate");
var duplicate = panels.Append(panels[0] with { ObjectId = Guid.NewGuid() }).ToArray();
Reject(Plan(duplicate, request with { PanelObjectIds = duplicate.Select(p => p.ObjectId).ToArray() }), "Multiple panels occupy");
var diagonal = panels.Append(Rect(Math.PI / 4, 0, 600, 0)).ToArray();
Reject(Plan(diagonal, request with { PanelObjectIds = diagonal.Select(p => p.ObjectId).ToArray() }), "ambiguous");
Console.WriteLine("[OK] invalid project/reference/tolerance/geometry, nonplanar/diagonal/degenerate panels and duplicate addresses rejected");

// Variable widths/heights do not move a panel to another row/column when starts align.
var lower = Rect(0, 0, 100, 0, 30, 17);
var upper = Rect(0, 0, 100, 20, 60, 80);
var leftBay = Rect(0, -100, 100, 0, 40, 30);
var custom = new[] { lower, upper, leftBay };
var customRequest = Request(custom, lower.ObjectId);
var customPlan = Required(Plan(custom, customRequest));
Check(customPlan.Panels.Single(p => p.ObjectId == lower.ObjectId).Bay == "02" &&
    customPlan.Panels.Single(p => p.ObjectId == upper.ObjectId).Bay == "02", "Left edges, not centers, establish bay");
Check(customPlan.Panels.Single(p => p.ObjectId == lower.ObjectId).Level == "01" &&
    customPlan.Panels.Single(p => p.ObjectId == leftBay.ObjectId).Level == "01", "Bottom edges, not centers, establish level");

// Planes aligned at the same left edge are sorted bottom-to-top, even with tiny x noise.
var lowerPlane = Rect(0, 0, 110, 0);
var upperPlane = Rect(0, -0.000001, 120, 50);
custom = [upperPlane, lowerPlane];
customPlan = Required(Plan(custom, Request(custom, lowerPlane.ObjectId)));
Check(customPlan.Panels.Single(p => p.ObjectId == lowerPlane.ObjectId).Elevation == "N1", "Plane order uses tolerance-aware bottom tie break");
Check(customPlan.Panels.Single(p => p.ObjectId == upperPlane.ObjectId).Bay == "01", "Bay restarts for each plane");

// Coordinate bands must not chain 0 -> .0000075 -> .000015 into one .00001 row.
custom = [Rect(0, 0, 100, 0), Rect(0, 40, 100, 0.0000075), Rect(0, 80, 100, 0.000015)];
customPlan = Required(Plan(custom, Request(custom, custom[0].ObjectId)));
Check(customPlan.Panels.Single(p => p.ObjectId == custom[2].ObjectId).Level == "02", "Row tolerance does not chain");
Console.WriteLine("[OK] varying sizes, plane bottom ties, bay resets and bounded coordinate tolerance");

Check(planner.ValidateDocumentIdentities(baseline, panels, []).Success, "Initial document preflight");
var first = baseline.Panels[0];
var external = new PanelCladdingPidExistingIdentity(Guid.NewGuid(), first.Pid.ToLowerInvariant(), "", false);
Check(!planner.ValidateDocumentIdentities(baseline, panels, [external]).Success, "Unselected source PID collision");
Check(!planner.ValidateDocumentIdentities(baseline, panels, [external with { Pid = "", Cid = first.Cid }]).Success, "Unselected CID collision");
Check(!planner.ValidateDocumentIdentities(baseline, panels, [external with { IsManagedDependency = true }]).Success, "Do not adopt orphan dependency identity");
var writtenSources = panels.Select(p => p with { UserText = new Dictionary<string, string>(baseline.Panels.Single(a => a.ObjectId == p.ObjectId).UserTextWrites) }).ToArray();
var dep = external with { Pid = first.Pid, Cid = first.Cid + "-0A", IsManagedDependency = true };
Check(planner.ValidateDocumentIdentities(baseline, writtenSources, [dep]).Success, "Idempotent run allowed with dependencies");
var renumbered = new PanelCladdingPidPlan(baseline.Panels.Select(p => p.ObjectId == first.ObjectId ?
    p with { Pid = "PID_NEW_N1_01_01", Cid = "CID_NEW_N1_01_01" } : p).ToArray());
Check(!planner.ValidateDocumentIdentities(renumbered, writtenSources, [dep]).Success, "Do not orphan dependencies when project/address changes");
Check(!planner.ValidateDocumentIdentities(renumbered, writtenSources, [dep with { Pid = "" }]).Success, "CID-only dependency protects its source");
Check(planner.ValidateDocumentIdentities(baseline, writtenSources,
    baseline.Panels.Select(p => new PanelCladdingPidExistingIdentity(p.ObjectId, p.Pid, p.Cid, false)).ToArray()).Success, "Self identity is not a collision");
Console.WriteLine("[OK] document PID/CID conflicts, self identity, dependency ownership and safe repeat preflight");

Console.WriteLine("PASS: PCpid planning and document preflight regression");
if (args.Contains("--native")) NativeSmoke.Run();

OperationResponse<PanelCladdingPidPlan> Plan(IReadOnlyList<PanelCladdingPidPanel> selected, PanelCladdingPidRequest? input = null, double tolerance = 0.00001) =>
    planner.CreatePlan(input ?? request, selected, tolerance, Math.PI / 180);
static PanelCladdingPidRequest Request(IReadOnlyList<PanelCladdingPidPanel> selected, Guid reference) =>
    new("BKT", selected.Select(p => p.ObjectId).ToArray(), reference, reference);
static PanelCladdingPidPanel Rect(double angle, double left, double depth, double bottom, double width = 30, double height = 20)
{
    var normal = new PanelPoint3(Math.Sin(angle), Math.Cos(angle), 0);
    var right = new PanelPoint3(-normal.Y, normal.X, 0);
    PanelPoint3 Point(double x, double z) => new(normal.X * depth + right.X * x, normal.Y * depth + right.Y * x, z);
    return new(Guid.NewGuid(), normal, [Point(left, bottom), Point(left + width, bottom), Point(left + width, bottom + height), Point(left, bottom + height)],
        new Dictionary<string, string>());
}
static T Required<T>(OperationResponse<T> response) => response.Success && response.Data is not null ? response.Data : throw new InvalidOperationException(response.Message);
static void Same(PanelCladdingPidPlan actual, PanelCladdingPidPlan expected)
{
    var map = expected.Panels.ToDictionary(p => p.ObjectId);
    Check(actual.Panels.Count == map.Count && actual.Panels.All(p => map[p.ObjectId] == p), "Assignment invariance");
}
static void Reject(OperationResponse response, string message) => Check(!response.Success && response.Message.Contains(message, StringComparison.OrdinalIgnoreCase), "Expected rejection containing: " + message + "; got " + response.Message);
static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
sealed record Fixture(string Label, Guid ObjectId, PanelPoint3 Normal, PanelPoint3[] Bounds, Dictionary<string, string> UserText);
