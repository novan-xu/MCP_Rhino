using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Domain.Models.PanelCladding;

var service = new PanelCladdingPointOrderService();
int count = 0;
foreach (double angle in new[] { 0d, Math.PI / 2, Math.PI, 3 * Math.PI / 2, .73 })
foreach (double tilt in new[] { 0d, .42 })
{
    var normal = new PanelPoint3(Math.Sin(angle) * Math.Cos(tilt), Math.Cos(angle) * Math.Cos(tilt), Math.Sin(tilt));
    var right = new PanelPoint3(-Math.Cos(angle), Math.Sin(angle), 0);
    var up = new PanelPoint3(-normal.Z * right.Y, normal.Z * right.X, Math.Cos(tilt));
    PanelPoint3 Point(double x, double y) => new(1e6 + right.X * x + up.X * y, -2e6 + right.Y * x + up.Y * y, 500 + up.Z * y);
    PanelPoint3[] expected = [Point(0, 0), Point(0, 20), Point(30, 20), Point(30, 0)];
    foreach (var permutation in Permute(expected))
    {
        var result = service.Order(permutation, normal, 1e-5);
        Check(result.Success && result.Data!.SequenceEqual(expected), "Front-local LL, UL, UR, LR independent of input order");
        var reverse = service.Order(permutation, new(-normal.X, -normal.Y, -normal.Z), 1e-5);
        Check(reverse.Success && reverse.Data!.SequenceEqual(new[] { expected[3], expected[2], expected[1], expected[0] }), "Opposite front changes lower-left and winding");
        count++;
    }
}
PanelPoint3[] quad = [new(0, 0, 0), new(0, 0, 20), new(-30, 0, 20), new(-30, 0, 0)];
Check(!service.Order(quad, new(0, 0, 1), 1e-5).Success, "Horizontal gravity degeneracy");
Check(!service.Order([new(0, 0, 0), new(-10, 0, 10), new(0, 0, 20), new(10, 0, 10)], new(0, 1, 0), 1e-5).Success, "Ambiguous diamond anchor");
Check(!service.Order(quad[..3], new(0, 1, 0), 1e-5).Success, "Nonquad rejected");
Check(!service.Order([quad[0], quad[1], quad[2], new(-30, 1, 0)], new(0, 1, 0), 1e-5).Success, "Nonplanar rejected");
Check(!service.Order([quad[0], quad[1], new(-3, 0, 10), quad[3]], new(0, 1, 0), 1e-5).Success, "Concave quad rejected");
Console.WriteLine($"PASS: {count * 2} point-order cases (permutations, rotations, slopes, opposite fronts), plus invalid/ambiguous cases");
if (args.Contains("--native")) NativePointOrderSmoke.Run();

static IEnumerable<PanelPoint3[]> Permute(PanelPoint3[] input)
{
    if (input.Length == 0) { yield return []; yield break; }
    for (int i = 0; i < input.Length; i++)
        foreach (var rest in Permute(input.Where((_, j) => j != i).ToArray())) yield return new[] { input[i] }.Concat(rest).ToArray();
}
static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
