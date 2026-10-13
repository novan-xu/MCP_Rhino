// Executed by the existing native offline preview tool; no fixture is published.
var outlineType = typeof(StoneSurfaceBake);
var edgeMethod = outlineType.GetMethod("SelectEdge", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
var collectMethod = outlineType.GetMethod("CollectOutlineEdges", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
int fixtureIndex = 0, assertions = 0;
var fixtureDefinitions = new List<InstanceDefinition>();
InstanceDefinition Definition(params GeometryBase[] geometry)
{
    int index = doc.InstanceDefinitions.Add("NestedOutlineTest_" + Guid.NewGuid().ToString("N"), "Regression fixture",
        Point3d.Origin, geometry, geometry.Select(g => new ObjectAttributes()));
    if (index < 0) throw new Exception("Could not create regression fixture " + fixtureIndex);
    fixtureIndex++;
    var definition = doc.InstanceDefinitions[index];
    fixtureDefinitions.Add(definition);
    foreach (var item in geometry) item.Dispose();
    return definition;
}
InstanceReferenceGeometry Ref(InstanceDefinition d, Transform x) => new InstanceReferenceGeometry(d.Id, x);
Line Edge(InstanceDefinition d) => (Line)edgeMethod.Invoke(null, new object[] { doc, d, .001 });
void Check(InstanceDefinition d, Point3d a, Point3d b, string name)
{
    Line edge = Edge(d);
    if (edge.From.DistanceTo(a) > 1e-8 || edge.To.DistanceTo(b) > 1e-8)
        throw new Exception(name + ": unexpected edge " + edge);
    assertions++;
}
void Rejected(Action operation, string expected)
{
    try { operation(); }
    catch (System.Reflection.TargetInvocationException error) when (error.InnerException is InvalidOperationException)
    {
        if (!error.InnerException.Message.Contains(expected)) throw;
        assertions++;
        return;
    }
    throw new Exception("Expected rejection: " + expected);
}
var leaf = Definition(new PolylineCurve(new[] { new Point3d(0, 0, 0), new Point3d(10, 0, 0),
    new Point3d(10, -2, 0), new Point3d(0, -2, 0), new Point3d(0, 0, 0) }));
var direct = leaf;
var translated = Definition(Ref(leaf, Transform.Translation(3, 7, 2)));
var rotated = Definition(Ref(leaf, Transform.Rotation(Math.PI / 2, Vector3d.ZAxis, Point3d.Origin)));
var scaledTransform = Transform.Identity;
scaledTransform.M00 = -2; scaledTransform.M11 = 3; scaledTransform.M03 = 4; scaledTransform.M13 = 5; scaledTransform.M23 = 2;
var mirrored = Definition(Ref(leaf, scaledTransform));
var child = Definition(Ref(leaf, Transform.Rotation(Math.PI / 2, Vector3d.ZAxis, Point3d.Origin)));
var multilevel = Definition(Ref(child, Transform.Translation(3, 7, 2)));
var sharedDefinitionRoot = Definition(Ref(leaf, Transform.Identity), Ref(leaf, Transform.Translation(0, -20, 0)));
var coincident = Definition(Ref(leaf, Transform.Identity), Ref(leaf, Transform.Identity));
var mixed = Definition(new LineCurve(new Point3d(-5, -10, 0), new Point3d(50, -10, 0)), Ref(leaf, Transform.Identity));
var ambiguous = Definition(Ref(leaf, Transform.Identity), Ref(leaf, Transform.Translation(20, 0, 0)));
var deep = leaf;
for (int i = 0; i < 24; i++) deep = Definition(Ref(deep, Transform.Identity));
var signaturesBefore = fixtureDefinitions.SelectMany(d => d.GetObjects()).ToDictionary(o => o.Id,
    o => o.Geometry.DataCRC(0) + "|" + o.Attributes.ToJSON(new Rhino.FileIO.SerializationOptions()));
int objectsBefore = doc.Objects.Count, definitionsBefore = doc.InstanceDefinitions.Count;
Check(direct, new Point3d(0, 0, 0), new Point3d(10, 0, 0), "Direct polyline");
Check(translated, new Point3d(3, 7, 2), new Point3d(13, 7, 2), "Translation");
Check(rotated, new Point3d(0, 10, 0), new Point3d(2, 10, 0), "Rotation before horizontal classification");
Check(mirrored, new Point3d(-16, 5, 2), new Point3d(4, 5, 2), "Mirror and nonuniform scale");
Check(multilevel, new Point3d(3, 17, 2), new Point3d(5, 17, 2), "Nested transform order");
Check(sharedDefinitionRoot, new Point3d(0, 0, 0), new Point3d(10, 0, 0), "Shared definition on separate paths");
Check(coincident, new Point3d(0, 0, 0), new Point3d(10, 0, 0), "Coincident duplicate edges");
Check(mixed, new Point3d(0, 0, 0), new Point3d(10, 0, 0), "Mixed direct and nested edges");
Rejected(() => Edge(ambiguous), "Ambiguous");
Rejected(() => Edge(deep), "excessively deep");
Rejected(() => collectMethod.Invoke(null, new object[] { doc, leaf, Transform.Identity, .001,
    new List<Line>(), new HashSet<Guid> { leaf.Id }, 0 }), "Cyclic");
var singular = Transform.Identity; singular.M00 = 0;
Rejected(() => collectMethod.Invoke(null, new object[] { doc, leaf, singular, .001,
    new List<Line>(), new HashSet<Guid>(), 0 }), "Invalid nested outline transform");
if (doc.Objects.Count != objectsBefore || doc.InstanceDefinitions.Count != definitionsBefore)
    throw new Exception("Read-only outline collection changed document counts.");
foreach (var original in signaturesBefore)
{
    var obj = doc.Objects.FindId(original.Key);
    if (obj == null || obj.Geometry.DataCRC(0) + "|" + obj.Attributes.ToJSON(new Rhino.FileIO.SerializationOptions()) != original.Value)
        throw new Exception("Outline collection changed a source member.");
}
Log("PASS: " + assertions + " native nested-outline assertions; source geometry/attributes and document counts unchanged.");
