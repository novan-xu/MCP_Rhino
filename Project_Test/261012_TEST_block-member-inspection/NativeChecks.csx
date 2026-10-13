extern alias rhinocommon;
using System.Runtime.CompilerServices;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Infrastructure.Rhino.Live;
using rhinocommon::Rhino;
using rhinocommon::Rhino.DocObjects;
using rhinocommon::Rhino.Geometry;
using rhinocommon::Rhino.Runtime.InProcess;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        try { RhinoInside.Resolver.Initialize(); Run(); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run()
    {
        using var core = new RhinoCore(new[] { "/NOSPLASH", "/NOTEMPLATE" }, WindowStyle.NoWindow);
        using var doc = RhinoDoc.CreateHeadless(null);
        doc.ModelUnitSystem = UnitSystem.Inches; doc.ModelAbsoluteTolerance = .001;
        int assertions = 0;
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            assertions++; Console.WriteLine("PASS: " + message);
        }
        ObjectAttributes Text(string key, string value)
        {
            var a = new ObjectAttributes(); a.SetUserString(key, value); return a;
        }
        InstanceDefinition Definition(GeometryBase[] geometry, ObjectAttributes[]? attributes = null)
        {
            int index = doc.InstanceDefinitions.Add("Fixture_" + Guid.NewGuid().ToString("N"), "", Point3d.Origin,
                geometry, attributes ?? geometry.Select(_ => new ObjectAttributes()).ToArray());
            if (index < 0) throw new Exception("Fixture creation failed.");
            return doc.InstanceDefinitions[index];
        }
        using var quad = NurbsSurface.CreateFromCorners(new(0, 0, 0), new(10, 0, 0), new(10, 5, 0), new(0, 5, 0));
        var leaf = Definition(new GeometryBase[] { quad }, new[] { Text("STN_ID", "STONE-A") });
        Guid Insert(InstanceDefinition d, Transform t) => doc.Objects.AddInstanceObject(d.Index, t, Text("Assembly_ID", "ASSEMBLY-A"));
        var rotation = Transform.Rotation(.71, Vector3d.ZAxis, Point3d.Origin);
        var translation = Transform.Translation(10, -20, 30);
        var mirror = Transform.Identity; mirror.M00 = -1;
        var scale = Transform.Identity; scale.M00 = 2; scale.M11 = 3;
        var shear = Transform.Identity; shear.M01 = .5;
        var roots = new[] { Transform.Identity, translation * rotation, translation * mirror, scale, shear }.Select(t => Insert(leaf, t)).ToList();
        using var nestedRef = new InstanceReferenceGeometry(leaf.Id, rotation);
        var parent = Definition(new GeometryBase[] { nestedRef }, new[] { Text("Nested", "metadata") });
        Guid nestedRoot = Insert(parent, translation * scale);
        roots.Add(nestedRoot);
        using var sharedA = new InstanceReferenceGeometry(leaf.Id, Transform.Identity);
        using var sharedB = new InstanceReferenceGeometry(leaf.Id, translation);
        var shared = Definition(new GeometryBase[] { sharedA, sharedB });
        Guid sharedRoot = Insert(shared, Transform.Identity);
        roots.Add(sharedRoot);
        doc.Objects.Hide(roots[0], true); doc.Objects.Lock(roots[1], true);
        using var circle = new Circle(Plane.WorldXY, 5).ToNurbsCurve();
        using var disk = Brep.CreatePlanarBreps(circle, .001)[0];
        using var triangle = new PolylineCurve(new[] { Point3d.Origin, new Point3d(5, 0, 0), new Point3d(0, 5, 0), Point3d.Origin });
        using var triangleFace = Brep.CreatePlanarBreps(triangle, .001)[0];
        using var outer = new Rectangle3d(Plane.WorldXY, 10, 10).ToNurbsCurve();
        using var inner = new Circle(new Point3d(5, 5, 0), 1).ToNurbsCurve();
        using var holed = Brep.CreatePlanarBreps(new[] { outer, inner }, .001)[0];
        using var sphere = new Sphere(Point3d.Origin, 5).ToBrep();
        using var line = new LineCurve(Point3d.Origin, new Point3d(2, 0, 0));
        var unsupported = Definition(new GeometryBase[] { disk, triangleFace, holed, sphere, line });
        Guid unsupportedRoot = Insert(unsupported, Transform.Identity);
        roots.Add(unsupportedRoot);
        var deep = leaf;
        for (int i = 0; i < 33; i++)
        {
            using var child = new InstanceReferenceGeometry(deep.Id, Transform.Identity);
            deep = Definition(new GeometryBase[] { child });
        }
        Guid deepRoot = Insert(deep, Transform.Identity);
        Guid plainObject = doc.Objects.AddPoint(Point3d.Origin);
        var allMembers = doc.InstanceDefinitions.Where(d => !d.IsDeleted).SelectMany(d => d.GetObjects()).ToList();
        string Attributes(RhinoObject o) => o.Attributes.ToJSON(new rhinocommon::Rhino.FileIO.SerializationOptions());
        var signatures = allMembers.ToDictionary(o => o.Id, o => (o.Geometry.DataCRC(0), Attributes(o)));
        int count = doc.Objects.Count, definitions = doc.InstanceDefinitions.Count;
        doc.Modified = false;
        uint undoNext = doc.NextUndoRecordSerialNumber, undoCurrent = doc.CurrentUndoRecordSerialNumber;
        var accessor = new FixtureAccessor(doc);
        var service = new RhinoBlockMemberInspectionService(new LiveBlockMemberInspector(accessor));
        var result = service.Inspect(new() { FilePath = "fixture.3dm", ObjectIds = roots.Concat(new[] { roots[0] }).ToList() });
        Check(result.Success && result.Data is not null, "Complete native inspection: " + result.Message);
        var data = result.Data!;
        Check(data.RootInstanceCount == roots.Count && data.LeafCount == 13, "Nested/shared leaves and duplicate root counts");
        Check(data.UnitSystem == "Inches" && data.AbsoluteTolerance == .001, "Model units and tolerance");
        var direct = data.Members.Where(m => m.RootInstanceId == roots[0]).Single();
        Check(direct.Geometry.Status == "PlanarQuad" && direct.Geometry.EdgeLengths.Order().SequenceEqual(new[] { 5d, 5d, 10d, 10d }), "Direct 10 x 5 quad");
        foreach (Guid id in roots.Take(3))
        {
            var g = data.Members.Single(m => m.RootInstanceId == id).Geometry;
            Check(g.Status == "PlanarQuad" && Math.Abs(g.Area!.Value - 50) < 1e-7
                && g.EdgeLengths.Order().Zip(new[] { 5d, 5d, 10d, 10d }).All(x => Math.Abs(x.First - x.Second) < 1e-8),
                "Translation/rotation/mirror invariant; hidden/locked root included");
        }
        var scaled = data.Members.Single(m => m.RootInstanceId == roots[3]);
        Check(Math.Abs(scaled.Geometry.Area!.Value - 300) < 1e-6
            && scaled.Geometry.EdgeLengths.Order().Zip(new[] { 15d, 15d, 20d, 20d }).All(x => Math.Abs(x.First - x.Second) < 1e-8), "Nonuniform scale measured");
        Check(data.Members.Single(m => m.RootInstanceId == roots[4]).Geometry.DiagonalLengths.Distinct().Count() == 2, "Shear retained in diagonal sizes");
        var nested = data.Members.Single(m => m.RootInstanceId == nestedRoot);
        Check(nested.Ancestry.Count == 2 && nested.UserText["STN_ID"] == "STONE-A"
            && nested.Ancestry[0].UserText["Assembly_ID"] == "ASSEMBLY-A" && nested.Ancestry[1].UserText["Nested"] == "metadata", "Member and ancestor attributes remain separate");
        var expected = new Point3d(10, 5, 0); expected.Transform(translation * scale * rotation);
        Check(nested.Geometry.Corners.Any(p => new Point3d(p[0], p[1], p[2]).DistanceTo(expected) < 1e-7), "Full nested transform multiplication order");
        var sharedLeaves = data.Members.Where(m => m.RootInstanceId == sharedRoot).ToArray();
        Check(sharedLeaves.Length == 2 && sharedLeaves[0].MemberObjectId == sharedLeaves[1].MemberObjectId
            && sharedLeaves[0].Ancestry[1].InstanceObjectId != sharedLeaves[1].Ancestry[1].InstanceObjectId, "Shared definition occurrences stay distinct");
        Check(data.Members.Where(m => m.RootInstanceId == unsupportedRoot).All(m => m.Geometry.Status == "Unsupported"), "Curved/holed/nonquad/nonsurface shapes excluded");
        var collapsed = Transform.Identity; collapsed.M00 = 0;
        var invalidSurface = BlockMemberGeometryReader.Read(quad, collapsed, .001);
        Check(invalidSurface.IsSurface && invalidSurface.Status == "Unsupported", "Failed surface geometry stays in the unchecked surface scope");
        Check(!service.Inspect(new() { FilePath = "fixture.3dm", ObjectIds = roots, MaxLeaves = 1 }).Success, "Leaf limit fails without partial results");
        Check(!service.Inspect(new() { FilePath = "fixture.3dm", ObjectIds = new() { roots[0], Guid.NewGuid() } }).Success, "Missing root fails complete request");
        Check(!service.Inspect(new() { FilePath = "fixture.3dm", ObjectIds = new() { plainObject } }).Success, "Non-block root rejected");
        Check(!service.Inspect(new() { FilePath = "fixture.3dm", ObjectIds = new() { parent.GetObjects()[0].Id } }).Success, "Definition member cannot be a top-level root");
        Check(!service.Inspect(new() { FilePath = "fixture.3dm", ObjectIds = new() { deepRoot } }).Success, "Excessive depth rejected");
        Check(doc.Objects.Count == count && doc.InstanceDefinitions.Count == definitions && !doc.Modified
            && doc.NextUndoRecordSerialNumber == undoNext && doc.CurrentUndoRecordSerialNumber == undoCurrent && accessor.UndoCalls == 0, "Object/definition counts, modified flag and Undo unchanged");
        Check(allMembers.All(o => signatures[o.Id] == (o.Geometry.DataCRC(0), Attributes(o))), "All definition geometry and attributes unchanged");
        doc.ModelUnitSystem = UnitSystem.Millimeters;
        Check(service.Inspect(new() { FilePath = "fixture.3dm", ObjectIds = new() { roots[0] } }).Data!.UnitSystem == "Millimeters", "Non-inch model units explicitly reported");
        Console.WriteLine($"PASS: {assertions} native assertions.");
    }

    private sealed class FixtureAccessor(RhinoDoc doc) : ILiveRhinoDocumentAccessor
    {
        public int UndoCalls { get; private set; }
        public OperationResponse<T> Execute<T>(string path, Func<RhinoDoc, OperationResponse<T>> work, TimeSpan? timeout = null) => work(doc);
        public OperationResponse<T> ExecuteWithUndo<T>(string path, string description, Func<RhinoDoc, OperationResponse<(bool Mutated, T Result)>> work, TimeSpan? timeout = null)
        { UndoCalls++; throw new InvalidOperationException("Read operation attempted Undo."); }
        public bool TryGetActiveDocumentState(string path, out bool modified) { modified = doc.Modified; return true; }
    }
}
