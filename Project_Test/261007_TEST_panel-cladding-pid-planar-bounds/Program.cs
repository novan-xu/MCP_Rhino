using System.Text.Json;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Domain.Models.PanelCladding;

var fixture = JsonSerializer.Deserialize<Fixture>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "reported-panel.json")),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
var id = Guid.Parse("00000000-0000-0000-0000-000000000001");
var panel = new PanelCladdingPidPanel(id, fixture.Normal, fixture.Points, new Dictionary<string, string>());
var planner = new PanelCladdingPidPlanningService();
var request = new PanelCladdingPidRequest("TST", [id], id, id);
var plan = planner.CreatePlan(request, [panel], fixture.Tolerance, Math.PI / 1800);
Check(plan.Success && plan.Data!.Panels.Single().Pid == "PID_TST_N1_01_01", plan.Message);
var setup = new PanelCladdingPidSetupService().CreateWrites(plan.Data!.Panels.Single(), panel);
Check(setup.Success && setup.Data!["CW_2.00_UNIT_DIMENSION"] == "90.00000x180.00000", "Reported live panel dimensions");
Check(setup.Data!.Count == 30, "All setup keys");
Check(new PanelCladdingPointOrderService().Order(panel.Bounds, panel.Normal, fixture.Tolerance).Success, "Reported boundary can be ordered");
var warped = panel with { Bounds = panel.Bounds.Select((p, i) => i == 3 ? p with { X = p.X + .1 } : p).ToArray() };
Check(!planner.CreatePlan(request, [warped], fixture.Tolerance, Math.PI / 1800).Success, "Pure geometry guard still rejects nonplanarity");
Console.WriteLine("PASS: reported live boundary plans, dimensions 90x180, all 30 keys, point order and nonplanarity guard");
if (args.Contains("--geometry")) NativeGeometrySmoke.Run(fixture.Points);

static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
internal sealed record Fixture(double Tolerance, PanelPoint3 Normal, PanelPoint3[] Points);
