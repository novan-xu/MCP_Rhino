using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public static class LintelChecks
{
    public static void Run(byte[] compiledScript, string source)
    {
        var types = Assembly.Load(compiledScript).GetTypes();
        Type type = types.Single(t => t.Name == "StoneSurfaceBake");
        var family = type.GetMethod("StoneFamily").CreateDelegate<Func<string, int>>();
        foreach (string name in new[] { "STN-MET-3010S-020", "stn-met-3010c-026", "STN-MET-3CUSTOM" })
            Check(family(name) == 3, "Lintel prefix rejected: " + name);
        Check(family("STN-MET-1010S-020") == 1 && family("STN-MET-2010S-020") == 2, "Existing stone families changed.");
        foreach (string name in new[] { null, "", "OTHER", "STN-MET-GA-3010S-020", "WF_STN-MET-3010S-020", "STN-MET-4010S-020", "STN-MET-3010S-020_EL", "STN-MET-3010S-020_v" })
            Check(family(name) == 0, "Non-lintel/excluded view was admitted: " + name);

        Type settings = type.GetNestedType("Settings");
        var ctor = settings.GetConstructors().Single();
        var parameters = ctor.GetParameters();
        Check(parameters[4].HasDefaultValue && (double)parameters[4].DefaultValue == 0
            && parameters[5].HasDefaultValue && (double)parameters[5].DefaultValue == 5, "Legacy constructor lost lintel defaults.");
        object options = ctor.Invoke(new object[] { -3d, 8d, 2.5d, 4d, -6d, 18d });
        double Read(string method, int code) => (double)settings.GetMethod(method).Invoke(options, new object[] { code });
        Check(Read("BaseOffsetFor", 1) == 2.5 && Read("HeightFor", 1) == 4, "Flat settings changed.");
        Check(Read("BaseOffsetFor", 2) == -3 && Read("HeightFor", 2) == 8, "Fin settings changed.");
        Check(Read("BaseOffsetFor", 3) == -6 && Read("HeightFor", 3) == 18, "Lintel settings are not independent.");
        foreach (var pair in new[] { new[] { double.NaN, 5d }, new[] { double.PositiveInfinity, 5d },
            new[] { 0d, double.NaN }, new[] { 0d, double.PositiveInfinity }, new[] { 0d, 0d }, new[] { 0d, -5d } })
        {
            bool rejected = false;
            try { ctor.Invoke(new object[] { 0d, 5d, 0d, 5d, pair[0], pair[1] }); }
            catch (TargetInvocationException error) when (error.InnerException is ArgumentException) { rejected = true; }
            Check(rejected, "Invalid lintel offset/height was accepted.");
        }
        var layer = type.GetMethod("LayerFor").CreateDelegate<Func<int, string>>();
        Check(layer(1).EndsWith("::Back") && layer(2).EndsWith("::Fin")
            && layer(3) == "01_MET-IEF-Curtain Wall::Surfaces - STN detailed::Lintel", "Family layer routing is wrong.");
        Type resultType = type.GetNestedType("Result");
        object result = Activator.CreateInstance(resultType);
        foreach (var item in new[] { (1, "Flats"), (2, "Fins"), (3, "Lintels") })
            Check(ReferenceEquals(resultType.GetMethod("PreviewFor").Invoke(result, new object[] { item.Item1 }),
                resultType.GetField(item.Item2).GetValue(result)), "Preview families were mixed.");
        Console.WriteLine("PASS: Actual linked lintel family/exclusions, independent settings/defaults/validation, layer and preview routing.");

        var select = type.GetMethod("SelectBackEdge").CreateDelegate<Func<IEnumerable<Line>, double, Line>>();
        var corners = type.GetMethod("WorldCorners").CreateDelegate<Func<Line, Transform, double, double, double, Point3d[]>>();
        var back = new Line(new Point3d(0, 0, 0), new Point3d(72, 0, 0));
        var outline = new[] { new Line(back.To, back.From), new Line(new Point3d(0, -14, 0), new Point3d(72, -14, 0)),
            new Line(new Point3d(0, -14, 0), back.From), new Line(new Point3d(72, -14, 0), back.To),
            new Line(new Point3d(0, -2, 0), new Point3d(72, -2, 0)), new Line(back.From, new Point3d(10, 0, 0)) };
        Line edge = select(outline, .001);
        Check(edge.From.DistanceTo(back.From) < 1e-9 && edge.To.DistanceTo(back.To) < 1e-9,
            "Lintel baseline did not use the full back edge in left-to-right order.");
        Check(select(outline.Reverse(), .001).From.DistanceTo(back.From) < 1e-9, "Back selection depends on member order.");
        Check(select(outline.Concat(new[] { back }), .001).Length == 72, "Duplicate identical back edges are ambiguous.");
        ExpectEdgeFailure(select, new[] { new Line(Point3d.Origin, new Point3d(0, 2, 0)) }, "Non-horizontal edge accepted.");
        ExpectEdgeFailure(select, Array.Empty<Line>(), "Missing outline accepted.");
        ExpectEdgeFailure(select, new[] { back, new Line(new Point3d(100, 0, 0), new Point3d(172, 0, 0)) }, "Ambiguous back edges accepted.");
        var translate = Transform.Identity; translate.M03 = 30; translate.M13 = -10; translate.M23 = 4;
        var rotate = translate; rotate.M00 = 0; rotate.M01 = -1; rotate.M10 = 1; rotate.M11 = 0;
        var mirror = translate; mirror.M00 = -2; mirror.M11 = 3; mirror.M22 = -1;
        var tilt = translate; tilt.M00 = .8; tilt.M02 = -.6; tilt.M20 = .6; tilt.M22 = .8;
        foreach (var transform in new[] { Transform.Identity, translate, rotate, mirror, tilt })
        foreach (double offset in new[] { -6d, 0d, 2.5d })
        foreach (double height in new[] { 4d, 5d, 18d })
        {
            var world = corners(edge, transform, offset, height, .001);
            Point3d start = back.From, end = back.To; start.Transform(transform); end.Transform(transform);
            start.Z += offset; end.Z += offset;
            Check(world[0].DistanceTo(start) < 1e-9 && world[1].DistanceTo(end) < 1e-9
                && world[2].DistanceTo(end + new Vector3d(0, 0, height)) < 1e-9
                && world[3].DistanceTo(start + new Vector3d(0, 0, height)) < 1e-9,
                "Lintel geometry lost its baseline offset or world-Z height.");
        }
        Console.WriteLine("PASS: Lintel back-edge selection, reversed/duplicate/ambiguous outline cases, and 45 offset/height/transform combinations.");

        Type componentType = types.Single(t => t.Name == "LiveStoneSurfaceComponent");
        var layout = componentType.GetMethod("LayoutVersion").CreateDelegate<Func<IEnumerable<string>, IEnumerable<string>, int>>();
        string[] previousInputs = { "FinBaseZ", "FinHeight", "FlatBaseZ", "FlatHeight", "Bake" };
        string[] previousOutputs = { "Fins", "Flats", "BakedIds", "Report" };
        string[] newInputs = previousInputs.Concat(new[] { "LintelBaseZ", "LintelHeight" }).ToArray();
        string[] newOutputs = previousOutputs.Concat(new[] { "Lintels" }).ToArray();
        Check(layout(previousInputs, previousOutputs) == 1, "Previous linked component cannot upgrade.");
        Check(layout(newInputs, newOutputs) == 2, "Upgraded component would repeatedly reconfigure.");
        Check(layout(new[] { "x", "y" }, new[] { "a" }) == 0, "Fresh component mistaken for an upgrade.");
        Check(layout(previousInputs.Reverse(), previousOutputs) == 0 && layout(newInputs, previousOutputs) == 0
            && layout(previousInputs, newOutputs) == 0, "Custom/partially upgraded ports accepted.");

        var root = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(kind: Microsoft.CodeAnalysis.SourceCodeKind.Script)).GetRoot();
        var component = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.ValueText == "LiveStoneSurfaceComponent");
        var methods = component.Members.OfType<MethodDeclarationSyntax>().ToDictionary(m => m.Identifier.ValueText);
        var upgrade = methods["ScheduleLintelUpgrade"];
        Check(upgrade.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(i => i.Expression.ToString() == "document.ScheduleSolution"),
            "Port upgrade runs during the active solve.");
        var calls = upgrade.DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
        Check(calls.Single(i => i.Expression.ToString() == "component.RecordUndoEvent").SpanStart
            < calls.Single(i => i.Expression.ToString() == "AppendLintelPorts").SpanStart, "Port changes precede Grasshopper Undo.");
        foreach (string method in new[] { "ScheduleLintelUpgrade", "AppendLintelPorts", "FinishConfiguration", "AddInput", "AddOutput" })
        {
            foreach (var call in methods[method].DescendantNodes().OfType<InvocationExpressionSyntax>())
                Check(!new[] { "UnregisterInputParameter", "UnregisterOutputParameter", "RemoveAllSources", "RemoveSource", "SetSource", "Configure" }
                    .Any(name => call.Expression.ToString() == name || call.Expression.ToString().EndsWith("." + name)),
                    "Upgrade can discard existing parameters/wires: " + call.Expression);
        }
        var appended = methods["AppendLintelPorts"].DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
        Check(appended.Count(i => i.Expression.ToString() == "AddInput") == 2
            && appended.Count(i => i.Expression.ToString() == "AddOutput") == 1, "Upgrade appends an unexpected port count.");
        Check(methods["WriteResults"].ToString().Contains("result.Lintels"), "Lintel preview is not exposed by the component.");
        Console.WriteLine("PASS: Actual layout upgrade detection/idempotence and additive port/Undo contract preserve previous indices and wires.");
    }

    private static void ExpectEdgeFailure(Func<IEnumerable<Line>, double, Line> select, IEnumerable<Line> edges, string message)
    {
        bool rejected = false;
        try { select(edges, .001); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, message);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
