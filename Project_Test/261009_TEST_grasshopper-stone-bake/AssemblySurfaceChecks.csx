using System.Reflection;

public static class AssemblySurfaceChecks
{
    public static void Run(byte[] compiledScript)
    {
        Type type = Assembly.Load(compiledScript).GetTypes().Single(t => t.Name == "StoneSurfaceBake");
        var isAssembly = type.GetMethod("IsAssembly").CreateDelegate<Func<string, bool>>();
        foreach (string name in new[] { "STN-MET", "STN-MET-GA-2010C-026", "STN-MET-GA2010C-026", "stn-met-custom" })
            Check(isAssembly(name), "New STN-MET prefix rejected: " + name);
        foreach (string name in new[] { null, "", "OTHER", "WF_STN-MET-GA-2010C-026", "STN-MET-custom_EL", "STN-MET-custom_v" })
            Check(!isAssembly(name), "Excluded prefix/suffix accepted: " + name);
        var visible = type.GetMethod("VisibilityAllows").CreateDelegate<Func<bool, bool, bool, IEnumerable<bool>, bool>>();
        Check(visible(false, true, true, new[] { true, true, true }), "Visible block/layer chain rejected.");
        Check(!visible(true, true, true, new[] { true }), "Explicitly hidden block accepted.");
        Check(!visible(false, false, true, new[] { true }), "Invisible object attributes accepted.");
        Check(!visible(false, true, false, new[] { true }), "Block from another viewport/space accepted.");
        Check(!visible(false, true, true, new[] { false, true }), "Hidden block layer accepted.");
        Check(!visible(false, true, true, new[] { true, true, false }), "Hidden ancestor/model/detail layer accepted.");
        var scopeName = type.GetMethod("ScopedAssemblyName").CreateDelegate<Func<string, IEnumerable<Guid>, string>>();
        var canChange = type.GetMethod("CanChangeSharedDefinition").CreateDelegate<Func<int, IEnumerable<Guid>, ISet<Guid>, bool>>();
        Guid visibleId = Guid.NewGuid(), hiddenId = Guid.NewGuid();
        Check(!canChange(0, new[] { visibleId, hiddenId }, new HashSet<Guid> { visibleId }), "Unselected reference allowed a shared definition update.");
        Check(scopeName("WF_STN-MET-A", new[] { visibleId, hiddenId }) == scopeName("WF_STN-MET-A", new[] { hiddenId, visibleId }), "Scope name depends on enumeration order.");
        Check(scopeName("WF_STN-MET-A", new[] { visibleId }) != scopeName("WF_STN-MET-A", new[] { hiddenId }), "Disjoint visible scopes share an output name.");
        Console.WriteLine("PASS: Actual linked STN-MET prefix, hidden/object/ancestor/view visibility gates, and isolated shared-definition scopes.");
        var identity = type.GetMethod("SurfaceIdentity").CreateDelegate<Func<string, string, string, Dictionary<string, string>>>();
        var matches = type.GetMethod("IdentityMatches").CreateDelegate<Func<Func<string, string>, IDictionary<string, string>, bool>>();
        var corners = type.GetMethod("WorldCorners").CreateDelegate<Func<Line, Transform, double, double, double, Point3d[]>>();
        var shares = type.GetMethod("CanShareAssembly").CreateDelegate<Func<IEnumerable<Point3d[]>, IEnumerable<Point3d[]>, Transform, double, bool>>();
        var key = type.GetMethod("AssemblyOccurrenceKey").CreateDelegate<Func<Guid, string>>();
        const string stone = "STN-MET-2010S-020", assembly = "STN-MET-GA-2010C-026";
        var expected = identity(stone, assembly, "member/path");
        Check(expected["STN_ID"] == stone && expected["Assembly_ID"] == assembly, "Requested example IDs differ.");
        var second = identity(stone, "STN-MET-GA-2010S-020", "member/path");
        Check(second["STN_ID"] == expected["STN_ID"] && second["Assembly_ID"] != expected["Assembly_ID"],
            "The same stone in two assemblies lost its distinct parent ID.");
        var actual = new Dictionary<string, string>(expected) { ["Unrelated"] = "preserved" };
        Func<string, string> get = name => actual.TryGetValue(name, out var value) ? value : null;
        Check(matches(get, expected), "Correct existing member failed no-op matching.");
        foreach (string name in new[] { "STN_ID", "Assembly_ID", "WF_Bake_Source", "WF_Bake_Tool" })
        {
            string value = actual[name];
            actual.Remove(name); Check(!matches(get, expected), "Missing " + name + " did not request repair.");
            actual[name] = "incorrect"; Check(!matches(get, expected), "Wrong " + name + " did not request repair.");
            actual[name] = value;
        }
        Check(matches(get, expected), "Repaired member did not become a no-op.");
        Guid parent = Guid.NewGuid();
        Check(key(parent) == "assembly/" + parent.ToString("D") && key(parent) != key(Guid.NewGuid()),
            "Assembly occurrence keys collide with other parents or legacy stone paths.");

        var edge = new Line(new Point3d(-7, 2, 3), new Point3d(-1, 2, 3));
        var translated = Transform.Identity; translated.M03 = 20; translated.M13 = -30; translated.M23 = 4;
        var rotated = translated; rotated.M00 = 0; rotated.M01 = -1; rotated.M10 = 1; rotated.M11 = 0;
        var mirrored = translated; mirrored.M00 = -2; mirrored.M11 = 3;
        var flipped = translated; flipped.M22 = -1;
        var tilted = translated; tilted.M00 = 0.8; tilted.M02 = -0.6; tilted.M20 = 0.6; tilted.M22 = 0.8;
        foreach (Transform parentTransform in new[] { translated, rotated, mirrored, flipped, tilted })
        foreach (double offset in new[] { -3d, 0d, 2.5d })
        foreach (double height in new[] { 4d, 5d, 8d })
        {
            var world = corners(edge, parentTransform, offset, height, .001);
            var local = corners(edge, Transform.Identity, offset, height, .001);
            Point3d start = edge.From, end = edge.To; start.Transform(parentTransform); end.Transform(parentTransform);
            start.Z += offset; end.Z += offset;
            Check(world[0].DistanceTo(start) < 1e-9 && world[1].DistanceTo(end) < 1e-9,
                "Assembly planning changed the base curve.");
            Check(world[3].DistanceTo(start + new Vector3d(0, 0, height)) < 1e-9
                && world[2].DistanceTo(end + new Vector3d(0, 0, height)) < 1e-9,
                "Assembly planning changed world +Z height.");
            bool compatible = parentTransform.M22 == 1;
            Check(shares(new[] { local }, new[] { world }, parentTransform, .001) == compatible,
                "Assembly definition reuse failed to distinguish world-Z-compatible transforms.");
        }
        var zero = corners(edge, Transform.Identity, 0, 5, .001);
        Check(!shares(new[] { zero }, Array.Empty<Point3d[]>(), Transform.Identity, .001), "Different member counts shared a definition.");
        var moved = (Point3d[])zero.Clone(); moved[2].Z += .1;
        Check(!shares(new[] { zero }, new[] { moved }, Transform.Identity, .001), "Different surface corners shared a definition.");
        Console.WriteLine("PASS: Actual linked assembly-surface IDs, missing/wrong attribute repair, stable occurrence keys, and 45 world-Z sharing cases.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
