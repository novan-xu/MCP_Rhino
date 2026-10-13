using System.Security.Cryptography;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

if (args.Length < 3) throw new ArgumentException("Configuration source.dwg linked-script.cs [apply]");
string configuration = args[0], source = Path.GetFullPath(args[1]), library = Path.GetFullPath(args[2]);
bool publish = args.Length > 3 && args[3] == "apply";
string router = Path.GetFullPath(Path.Combine("src", "MCP_Rhino.Router", "bin", configuration, "net8.0", "MCP_Rhino.Router.exe"));
string originalHash = Hash(source); DateTime originalTime = File.GetLastWriteTimeUtc(source);
await using var client = await McpClient.CreateAsync(new StdioClientTransport(new() { Name = "lintel-dwg-acceptance", Command = router }),
    new McpClientOptions { InitializationTimeout = TimeSpan.FromSeconds(30) });
string script = """
    var objects = doc.Objects.GetObjectList(new ObjectEnumeratorSettings {
        NormalObjects=true, HiddenObjects=true, LockedObjects=true, IdefObjects=false,
        ActiveObjects=true, ReferenceObjects=false, DeletedObjects=false, IncludeLights=true
    }).Concat(doc.InstanceDefinitions.Where(d=>!d.IsDeleted && !d.IsReference).SelectMany(d=>d.GetObjects())).DistinctBy(o=>o.Id).ToArray();
    if (objects.Length == 0) throw new Exception("Source preservation check requires imported objects.");
    string Signature(RhinoObject o) => o.Geometry.DataCRC(0).ToString() + "|" + o.Attributes.ToJSON(new Rhino.FileIO.SerializationOptions());
    var signatures = objects.ToDictionary(o => o.Id, Signature);
    int definitionsBefore = doc.InstanceDefinitions.Count;
    var settings = new StoneSurfaceBake.Settings(0, 4.46645, 0, 4.46645, 0, 4.46645);
    var baked = StoneSurfaceBake.RunHeadless(doc, settings, true);
    Log(baked.Report);
    if (baked.Lintels.Count == 0) throw new Exception("No lintel surfaces were produced; inspect DWG family/visibility before publishing.");
    double inches = RhinoMath.UnitScale(doc.ModelUnitSystem, UnitSystem.Inches);
    var heights = baked.Lintels.Select(b => { var box = b.GetBoundingBox(true); return (box.Max.Z-box.Min.Z)*inches; }).ToArray();
    var baseZ = baked.Lintels.Select(b => b.GetBoundingBox(true).Min.Z*inches).ToArray();
    if (heights.Any(h => Math.Abs(h-4.46645) > 1e-7)) throw new Exception("Baked lintel height did not match 4.46645 inches.");
    foreach (var item in signatures)
    {
        var current = doc.Objects.FindId(item.Key);
        if (current == null || Signature(current) != item.Value)
            throw new Exception("An imported source object/attribute changed during baking: " + item.Key);
    }
    int countAfterFirst = doc.Objects.Count, definitionsAfterFirst = doc.InstanceDefinitions.Count;
    var repeated = StoneSurfaceBake.RunHeadless(doc, settings, true);
    Log(repeated.Report);
    if (doc.Objects.Count != countAfterFirst || doc.InstanceDefinitions.Count != definitionsAfterFirst
        || repeated.Fins.Count + repeated.Flats.Count + repeated.Lintels.Count != 0)
        throw new Exception("A repeated bake added geometry or definitions.");
    Result = new {
        finSurfaces=baked.Fins.Count, flatSurfaces=baked.Flats.Count, lintelSurfaces=baked.Lintels.Count,
        bakedAssemblyCount=baked.BakedIds.Count, minLintelHeightInches=heights.Min(), maxLintelHeightInches=heights.Max(),
        minLintelBaseZInches=baseZ.Min(), maxLintelBaseZInches=baseZ.Max(),
        baselineOffsetInches=0, importedObjectsPreserved=signatures.Count, definitionsBefore, definitionsAfter=definitionsAfterFirst,
        repeatedBakeAddedObjects=doc.Objects.Count-countAfterFirst, sourceObjectsUnchanged=true, report=baked.Report
    };
    foreach (var geometry in baked.Fins.Concat(baked.Flats).Concat(baked.Lintels)) geometry.Dispose();
    """;
var preview = Read(await client.CallToolAsync("rhino_offline_preview_rhino_script", new Dictionary<string, object?> {
    ["filePath"]=source, ["script"]=script, ["libraryPath"]=library, ["libraryType"]="StoneSurfaceBake",
    ["modelUnits"]="Inches", ["timeoutSeconds"]=180
}, cancellationToken: CancellationToken.None));
Console.WriteLine(JsonSerializer.Serialize(preview, new JsonSerializerOptions { WriteIndented=true }));
JsonElement? applied = null;
object? persisted = null;
if (publish)
{
    applied = Read(await client.CallToolAsync("rhino_offline_apply_rhino_script", new Dictionary<string, object?> {
        ["filePath"]=source, ["previewToken"]=preview.GetProperty("previewToken").GetString()
    }));
    Console.WriteLine(JsonSerializer.Serialize(applied, new JsonSerializerOptions { WriteIndented=true }));
    string output = applied.Value.GetProperty("outputPath").GetString()!;
    using var file = Rhino.FileIO.File3dm.Read(output);
    var byId = file.Objects.ToDictionary(o=>o.Id);
    var definitions = file.AllInstanceDefinitions.ToDictionary(d=>d.Id);
    var instances = file.Objects.Where(o=>o.Geometry is Rhino.Geometry.InstanceReferenceGeometry &&
        o.Attributes.GetUserString("WF_Bake_Tool") == "GrasshopperStoneSurfaceBake/assembly-surfaces-v2").ToArray();
    var heights = new List<double>();
    foreach (var instance in instances)
    {
        var reference = (Rhino.Geometry.InstanceReferenceGeometry)instance.Geometry;
        foreach (Guid id in definitions[reference.ParentIdefId].GetObjectIds())
        {
            var member = byId[id];
            if (member.Geometry is not Rhino.Geometry.Brep || string.IsNullOrEmpty(member.Attributes.GetUserString("STN_ID"))
                || member.Attributes.GetUserString("Assembly_ID") != instance.Attributes.GetUserString("Assembly_ID"))
                throw new Exception("Saved block lost direct stone surfaces/metadata.");
            using var world = member.Geometry.Duplicate();
            if (!world.Transform(reference.Xform)) throw new Exception("Invalid saved instance transform.");
            var box = world.GetBoundingBox(true);
            heights.Add((box.Max.Z-box.Min.Z)*Rhino.RhinoMath.UnitScale(file.Settings.ModelUnitSystem, Rhino.UnitSystem.Inches));
        }
    }
    if (instances.Length != 26 || heights.Count != 26 || heights.Any(h=>Math.Abs(h-4.46645)>1e-7))
        throw new Exception("Saved output did not retain 26 lintels at 4.46645 inches.");
    string outputHash = Hash(output);
    var repeat = Read(await client.CallToolAsync("rhino_offline_preview_rhino_script", new Dictionary<string, object?> {
        ["filePath"]=output, ["libraryPath"]=library, ["libraryType"]="StoneSurfaceBake", ["timeoutSeconds"]=180,
        ["script"]="int count=doc.Objects.Count,defs=doc.InstanceDefinitions.Count; var r=StoneSurfaceBake.RunHeadless(doc,new StoneSurfaceBake.Settings(0,4.46645,0,4.46645,0,4.46645),true); if(doc.Objects.Count!=count || doc.InstanceDefinitions.Count!=defs || r.Fins.Count+r.Flats.Count+r.Lintels.Count!=0) throw new Exception(\"Saved-copy bake added duplicates\"); Log(r.Report); Result=new { existingAssemblyIds=r.BakedIds.Count, addedObjects=doc.Objects.Count-count };"
    }));
    if (Hash(output) != outputHash) throw new Exception("Saved copy changed during repeat preview.");
    persisted = new { assemblyInstances=instances.Length, surfaceOccurrences=heights.Count, minHeightInches=heights.Min(),
        maxHeightInches=heights.Max(), metadataPreserved=true, savedCopyRepeat=repeat };
}
if (Hash(source) != originalHash || File.GetLastWriteTimeUtc(source) != originalTime) throw new Exception("Source bytes/time changed.");
var evidence = new { originalHash, originalTime, sourceUnchanged=true, preview, applied, persisted };
string evidenceDirectory = Path.Combine("Runtime_Test", "local", "offline-rhino-dwg");
Directory.CreateDirectory(evidenceDirectory);
string evidencePath = Path.Combine(evidenceDirectory, publish ? "dwg-bake-result.json" : "dwg-preview-result.json");
File.WriteAllText(evidencePath, JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented=true }));
Console.WriteLine("PASS: DWG bake, exact height, source object preservation, repeat-bake no-op and original file hash/time.");

static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
static JsonElement Read(CallToolResult result)
{
    string text = result.Content.OfType<TextContentBlock>().First().Text;
    if (result.IsError == true) throw new Exception(text);
    return JsonDocument.Parse(text).RootElement.Clone();
}
