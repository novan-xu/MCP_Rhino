using System.Drawing;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using MCP_Rhino.Offline.Application;
using MCP_Rhino.Offline.Contracts;
using MCP_Rhino.Offline.Server;
using MCP_Rhino.Offline.Infrastructure;
using MCP_Rhino.Router.Routing;
using MCP_Rhino.Transport.Routing;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;

string root = Path.GetFullPath(Path.Combine("Runtime_Test", "local", "offline-copy-edit", Guid.NewGuid().ToString("N")));
Directory.CreateDirectory(root);
string source = Path.Combine(root, "source.3dm");
Guid pointId, lineId;
using (var fixture = new File3dm())
{
    fixture.AllLayers.AddLayer("Panels", Color.Blue);
    fixture.Strings.SetString("Original", "Keep");
    var attributes = new ObjectAttributes { Name = "Panel 1", LayerIndex = 0 };
    attributes.SetUserString("Original", "Keep");
    pointId = fixture.Objects.AddPoint(new Point3d(1, 2, 3), attributes);
    lineId = fixture.Objects.AddLine(new Point3d(0, 0, 0), new Point3d(1, 0, 0), new ObjectAttributes { LayerIndex = 0 });
    Require(fixture.Write(source, 8), "Fixture created.");
}
byte[] original = File.ReadAllBytes(source);
DateTime originalTime = File.GetLastWriteTimeUtc(source);
string originalHash = Hash(source);
var service = new OfflineScriptService(new OfflineFileStore());
_ = service.Inspect(source);
string script = $$$"""
    const layer = model.addLayer('Edited', '#123456');
    const objects = model.objects().items;
    for (const item of objects) {
        if (item.name.startsWith('Panel')) {
            model.setAttributes([item.id], {name:'Edited Panel',layerId:layer,color:'#CC8844',userText:{Reviewed:'Yes'}});
            model.transform([item.id], [1,0,0,10, 0,1,0,0, 0,0,1,0, 0,0,0,1]);
        }
    }
    model.deleteObjects(['{{{lineId}}}']);
    model.editLayer(layer, {name:'Edited final',visible:true,locked:false});
    model.setDocumentUserText('Reviewed', 'Yes');
    model.addPoint([4,5,6], {layerId:layer,name:'New point'});
    model.addLine([0,0,0],[2,3,4]);
    model.addPolyline([[0,0,0],[2,0,0],[2,2,0]]);
    model.addSphere([0,0,0], 2);
    model.addBox([0,0,0], [1,2,3]);
    model.log('complete');
    """;
OfflinePreviewResponse preview = service.PreviewScript(source, script);
Require(Directory.GetFiles(root).Length == 1, "Preview writes no files.");
Require(preview.ObjectCountAfter == 6 && preview.Log.Single() == "complete", "Script loops, filtering and multiple edits preview successfully.");
OfflineApplyResponse applied = service.ApplyScript(source, preview.PreviewToken);
Require(Hash(source) == originalHash && File.GetLastWriteTimeUtc(source) == originalTime, "Source bytes and timestamp remain unchanged.");
Require(System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(applied.OutputPath), @"^source_\d{8}_\d{6}\.3dm$") && applied.OutputPath != source, "Apply generates only the requested date/time suffix.");
DateTime namedTime = DateTime.ParseExact(Path.GetFileNameWithoutExtension(applied.OutputPath)["source_".Length..], "yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
Require(Math.Abs((DateTime.Now-namedTime).TotalSeconds) < 5, "Filename timestamp is actual local time.");
Require(preview.OutputNaming == "<source-stem>_<local-yyyyMMdd_HHmmss>.3dm", "Preview advertises the new naming rule.");
using (File3dm copy = File3dm.Read(applied.OutputPath))
{
    File3dmObject edited = copy.Objects.FindId(pointId);
    Require(edited.Attributes.Name == "Edited Panel" && edited.Attributes.GetUserString("Original") == "Keep" && edited.Attributes.GetUserString("Reviewed") == "Yes", "Attributes and existing user text persist.");
    Require(((Rhino.Geometry.Point)edited.Geometry).Location == new Point3d(11, 2, 3), "Transform persists and preserves object UUID.");
    Require(copy.Objects.FindId(lineId) is null && copy.Objects.Count == 6, "Deletion and primitive additions persist.");
    Require(copy.AllLayers.Any(layer => layer.Name == "Edited final") && copy.Strings.GetValue("Original") == "Keep" && copy.Strings.GetValue("Reviewed") == "Yes", "Layers and document metadata round-trip.");
}
ExpectFailure(() => service.ApplyScript(source, preview.PreviewToken), "Consumed preview cannot replay.");
string firstCopyHash = Hash(applied.OutputPath);
OfflinePreviewResponse second = service.PreviewScript(applied.OutputPath, "model.setDocumentUserText('Second', 'Yes');");
OfflineApplyResponse secondApplied = service.ApplyScript(applied.OutputPath, second.PreviewToken);
Require(Hash(applied.OutputPath) == firstCopyHash && secondApplied.OutputPath != applied.OutputPath, "Editing a previous copy creates another copy.");
Require(System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(secondApplied.OutputPath), @"^source_\d{8}_\d{6}\.3dm$"), "Editing a copy does not accumulate timestamp suffixes.");

foreach (string invalid in new[] {
    "model.setAttributes(['00000000-0000-0000-0000-000000000001'], {name:'bad'});",
    "try { model.setDocumentUserText('x','y'); model.deleteObjects(['bad']); } catch(e) {}",
    "model.addPoint([NaN,0,0]);",
    "model.setAttributes([model.objects().items[0].id], {outputPath:'source.3dm'});",
    "model.write('source.3dm');", "System.IO.File.WriteAllText('source.3dm','bad');",
    "require('fs').writeFileSync('source.3dm','bad');", "importNamespace('System.IO');",
    "model.objects.GetType().Assembly;", "model.objects.constructor('return process')();",
    "__modelCall('write','{}');", "eval('1+1');", "while(true) {}" })
{
    int before = Directory.GetFiles(root).Length;
    ExpectFailure(() => service.PreviewScript(source, invalid), "Invalid/forbidden script rejected: " + invalid[..Math.Min(65, invalid.Length)]);
    Require(Directory.GetFiles(root).Length == before && Hash(source) == originalHash, "Rejected script publishes nothing and preserves source.");
}
OfflinePreviewResponse stale = service.PreviewScript(source, "model.setDocumentUserText('Stale','Yes');");
using (File3dm changed = File3dm.Read(source)) { changed.Strings.SetString("ExternalChange", "Yes"); Require(changed.Write(source, 8), "External source change simulated."); }
ExpectFailure(() => service.ApplyScript(source, stale.PreviewToken), "Changed source rejects stale preview.");
File.WriteAllBytes(source, original);
ExpectFailure(() => service.ApplyScript(applied.OutputPath, stale.PreviewToken), "Preview cannot target another file.");
ExpectFailure(() => service.PreviewScript("relative.3dm", "model.log('x');"), "Relative paths rejected.");
using (var cancellation = new CancellationTokenSource())
{
    cancellation.Cancel();
    ExpectFailure(() => service.PreviewScript(source, "model.addPoint([1,2,3]);", cancellation.Token), "Canceled preview publishes nothing.");
}

var concurrent = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
{
    var own = new OfflineScriptService(new OfflineFileStore());
    var p = own.PreviewScript(source, "model.addPoint([1,2,3]);");
    return own.ApplyScript(source, p.PreviewToken).OutputPath;
})));
Require(concurrent.Distinct().Count() == 4 && Hash(source) == originalHash, "Concurrent applies produce four distinct copies without source changes.");
Require(concurrent.All(path=>System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(path), @"^source_\d{8}_\d{6}\.3dm$")) && Hash(applied.OutputPath)==firstCopyHash, "Concurrent same-stem copies retain short names and preserve earlier outputs.");
string legacySource = Path.Combine(root, "legacy_offline-edit_20261010_120000_0123456789abcdef0123456789abcdef.3dm");
File.WriteAllBytes(legacySource, original);
var legacyPreview = service.PreviewScript(legacySource, "model.log('legacy');");
var legacyCopy = service.ApplyScript(legacySource, legacyPreview.PreviewToken);
Require(System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(legacyCopy.OutputPath), @"^legacy_\d{8}_\d{6}\.3dm$") && Hash(legacySource)==originalHash, "Legacy long suffix is replaced without changing its source.");
string collisionRoot = Path.Combine(root, "occupied-seconds");
Directory.CreateDirectory(collisionRoot);
string collisionSource = Path.Combine(collisionRoot, "source.3dm");
File.WriteAllBytes(collisionSource, original);
var collisionPreview = service.PreviewScript(collisionSource, "model.log('collision cancellation');");
DateTime occupiedTime = DateTime.Now;
var occupiedPaths = Enumerable.Range(-2, 16).Select(i=>Path.Combine(collisionRoot,
    "source_"+occupiedTime.AddSeconds(i).ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture)+".3dm")).ToArray();
foreach (string path in occupiedPaths) File.WriteAllText(path, "existing output must survive");
using (var cancelWait = new CancellationTokenSource(100))
{
    bool canceled = false;
    try { service.ApplyScript(collisionSource, collisionPreview.PreviewToken, cancelWait.Token); }
    catch (OperationCanceledException) { canceled = true; }
    Require(canceled && occupiedPaths.All(path=>File.ReadAllText(path)=="existing output must survive")
        && Hash(collisionSource)==originalHash && !Directory.GetFiles(collisionRoot,"*.tmp").Any(),
        "Collision wait cancels promptly, preserves every occupied filename/source and cleans staging.");
}

var exactService = new OfflineScriptService(new OfflineFileStore());
var randomPreview = exactService.PreviewScript(source, "const value = String(Math.random()); model.setDocumentUserText('Random',value); model.log(value);");
var randomOutput = exactService.ApplyScript(source, randomPreview.PreviewToken);
using (File3dm copy = File3dm.Read(randomOutput.OutputPath))
    Require(copy.Strings.GetValue("Random") == randomPreview.Log.Single(), "Apply preserves the exact preview output of a nondeterministic script.");

var clock = new TestTimeProvider();
var expiring = new OfflineScriptService(new OfflineFileStore(), clock);
var expiringPreview = expiring.PreviewScript(source, "model.log('expire');");
clock.Advance(TimeSpan.FromMinutes(11));
ExpectFailure(() => expiring.ApplyScript(source, expiringPreview.PreviewToken), "Expired preview rejected.");
for (int i = 0; i < 4; i++) expiring.PreviewScript(source, "model.log('capacity');");
ExpectFailure(() => expiring.PreviewScript(source, "model.log('overflow');"), "Preview retention capacity enforced.");

FileAttributes savedAttributes = File.GetAttributes(source);
try
{
    File.SetAttributes(source, savedAttributes | FileAttributes.ReadOnly);
    var readOnly = exactService.PreviewScript(source, "model.setDocumentUserText('ReadOnly','Allowed');");
    _ = exactService.ApplyScript(source, readOnly.PreviewToken);
    Require(Hash(source) == originalHash, "A read-only source supports duplicate-only editing.");
}
finally { File.SetAttributes(source, savedAttributes); }
using (var held = new FileStream(source, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
    ExpectFailure(() => exactService.PreviewScript(source, "model.log('locked');"), "A locked source fails closed.");
var canceledPreview = exactService.PreviewScript(source, "model.log('cancel apply');");
using (var canceled = new CancellationTokenSource())
{
    canceled.Cancel();
    int before = Directory.GetFiles(root).Length;
    ExpectFailure(() => exactService.ApplyScript(source, canceledPreview.PreviewToken, canceled.Token), "Canceled apply rejected.");
    Require(Directory.GetFiles(root).Length == before, "Canceled apply leaves no output or temporary file.");
}

VerifySafety();
string blockSource = Path.Combine(root, "blocks.3dm");
Guid instanceId, definitionId;
using (var blocks = new File3dm())
{
    blocks.AllLayers.AddLayer("Blocks", Color.Black);
    blocks.AllMaterials.Add(new Material { Name = "Retained material", DiffuseColor = Color.Green });
    using var geometry = new Sphere(Point3d.Origin, 3).ToBrep();
    int definition = blocks.AllInstanceDefinitions.Add("Retained block", "test", Point3d.Origin,
        new GeometryBase[] { geometry }, new[] { new ObjectAttributes { LayerIndex = 0 } });
    definitionId = blocks.AllInstanceDefinitions.First().Id;
    instanceId = blocks.Objects.AddInstanceObject(definition, Transform.Identity, new ObjectAttributes { LayerIndex = 0, Name = "Instance" });
    Require(blocks.Write(blockSource, 8), "Block/material fixture created.");
}
string blockHash = Hash(blockSource);
var blockService = new OfflineScriptService(new OfflineFileStore());
var blockPreview = blockService.PreviewScript(blockSource, $$$"""
    model.transform(['{{{instanceId}}}'], [1,0,0,5, 0,1,0,0, 0,0,1,0, 0,0,0,1]);
    model.setDocumentUserText('Block review','Yes');
    """);
var blockOutput = blockService.ApplyScript(blockSource, blockPreview.PreviewToken);
using (File3dm blocks = File3dm.Read(blockOutput.OutputPath))
{
    var instance = (InstanceReferenceGeometry)blocks.Objects.FindId(instanceId).Geometry;
    Require(instance.ParentIdefId == definitionId && instance.Xform.M03 == 5 && blocks.AllInstanceDefinitions.Count == 1,
        "Block instance transform preserves UUID, definition and geometry.");
    Require(blocks.AllMaterials.Single().Name == "Retained material" && Hash(blockSource) == blockHash,
        "Unchanged materials and original block file are preserved.");
}
ExpectFailure(() => blockService.PreviewScript(blockSource,
    "model.deleteObjects(model.objects().items.filter(o=>o.isDefinitionGeometry).map(o=>o.id));"),
    "Block-definition geometry edits are rejected.");

using ILoggerFactory logger = LoggerFactory.Create(_ => { });
await using var catalog = new BackendCatalog(new RouteDiscovery(new RouteRegistry(Path.Combine(root, "empty-registry"))), logger);
var router = new RouterRuntime(catalog);
var tools = await router.ListToolsCoreAsync(CancellationToken.None);
Require(tools.Tools.Count == 8 && tools.Tools.Count(tool => tool.Name.StartsWith("rhino_offline_")) == 5, "Offline tools list without any Rhino endpoint.");
var liveError = await router.CallToolCoreAsync(new CallToolRequestParams { Name = "create_points" }, CancellationToken.None);
Require(liveError.IsError == true, "Missing live endpoint never falls back to offline execution.");

string configuration = typeof(RouterRuntime).Assembly.Location.Contains("Release") ? "Release" : "Debug";
string routerDll = Path.GetFullPath($"src/MCP_Rhino.Router/bin/{configuration}/net8.0/MCP_Rhino.Router.dll");
await using var client = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions {
    Name = "offline-copy-edit-smoke", Command = "dotnet", Arguments = [routerDll]
}));
var protocolTools = await client.ListToolsAsync();
Require(protocolTools.Any(tool => tool.Name == "rhino_offline_apply_script"), "Real stdio MCP advertises offline tools.");
CallToolResult pResult = await client.CallToolAsync("rhino_offline_preview_script", new Dictionary<string, object?> {
    ["filePath"] = source, ["script"] = "model.setDocumentUserText('MCP','Passed');"
});
Require(pResult.IsError != true, "Real stdio MCP executes a script preview." + (pResult.IsError == true ? JsonText(pResult) : ""));
using var pJson = JsonDocument.Parse(JsonText(pResult));
CallToolResult aResult = await client.CallToolAsync("rhino_offline_apply_script", new Dictionary<string, object?> {
    ["filePath"] = source, ["previewToken"] = pJson.RootElement.GetProperty("previewToken").GetString()
});
Require(aResult.IsError != true && Hash(source) == originalHash, "Real stdio MCP applies only to a duplicate.");
Console.WriteLine("[OK] offline-copy-edit-smoke-test");
return 0;

static string JsonText(CallToolResult value) => value.StructuredContent?.GetRawText() ?? ((TextContentBlock)value.Content.First()).Text;
static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
static void Require(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("[OK] " + message); }
static void ExpectFailure(Action action, string message)
{
    bool failed = false;
    try { action(); } catch (Exception) { failed = true; }
    Require(failed, message);
}
static void VerifySafety()
{
    var methods = typeof(OfflineToolRegistration).Assembly.GetTypes().SelectMany(type => type.GetMethods())
        .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null).ToArray();
    Require(methods.Length == 5, "Three bounded JavaScript tools and two native Rhino script tools.");
    foreach (MethodInfo method in methods)
    {
        var attribute = method.GetCustomAttribute<McpServerToolAttribute>()!;
        bool nativePreview = attribute.Name == "rhino_offline_preview_rhino_script";
        bool apply = attribute.Name is "rhino_offline_apply_script" or "rhino_offline_apply_rhino_script";
        Require(attribute.ReadOnly == !(apply || nativePreview) && attribute.Destructive == nativePreview
            && attribute.OpenWorld == (apply || nativePreview), "Offline safety annotations: " + attribute.Name);
        var supplied = method.GetCustomAttributesData().Single(a => a.AttributeType == typeof(McpServerToolAttribute)).NamedArguments.Select(a => a.MemberName).ToArray();
        Require(new[] { "ReadOnly", "Destructive", "OpenWorld" }.All(supplied.Contains), "Explicit safety annotations.");
    }
}

sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan interval) => _now += interval;
}
