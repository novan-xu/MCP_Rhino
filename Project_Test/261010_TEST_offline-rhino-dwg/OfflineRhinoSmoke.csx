using System.Security.Cryptography;
using System.Text.Json;
using MCP_Rhino.Offline.Application;
using MCP_Rhino.Offline.Contracts;
using MCP_Rhino.Offline.Infrastructure;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Rhino.FileIO;
using Rhino.Geometry;

string configuration = args.Length > 0 ? args[0] : "Debug";
string repository = Directory.GetCurrentDirectory();
string router = args.Length > 1 ? Path.GetFullPath(args[1]) : Path.Combine(repository, "src", "MCP_Rhino.Router", "bin", configuration, "net8.0", "MCP_Rhino.Router.exe");
string worker = Path.Combine(Path.GetDirectoryName(router)!, "RhinoWorker", "MCP_Rhino.RhinoWorker.exe");
string root = Path.Combine(repository, "Runtime_Test", "local", "offline-rhino-dwg", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
string source = Path.Combine(root, "source.3dm");
using (var model = new File3dm())
{
    model.Settings.ModelUnitSystem = Rhino.UnitSystem.Inches;
    model.Objects.AddPoint(new Point3d(1, 2, 3));
    Require(model.Write(source, 8), "Fixture created.");
}
string before = Hash(source); DateTime time = File.GetLastWriteTimeUtc(source);
var service = new OfflineRhinoScriptService(new OfflineRhinoStore(), new RhinoWorkerProcess(worker));
var preview = await service.PreviewAsync(source, "doc.Objects.AddPoint(new Point3d(4,5,6)); Result = new { tag = \"native-test\" }; Log(\"native C# ran\");");
Require(preview.Document.ObjectCountBefore == 1 && preview.Document.ObjectCountAfter == 2, "Real native Rhino preview adds a point.");
Require(Directory.GetFiles(root).Length == 1 && Hash(source) == before, "Preview publishes no sibling and preserves source.");
var applied = await service.ApplyAsync(source, preview.PreviewToken);
Require(System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(applied.OutputPath), @"^source_\d{8}_\d{6}\.3dm$") && Hash(source) == before && File.GetLastWriteTimeUtc(source) == time, "Apply publishes a date/time suffixed copy; source bytes/time unchanged.");
using (var model = File3dm.Read(applied.OutputPath)) Require(model.Objects.Count == 2, "Native copy reopens through Rhino3dm.");
await Fail(() => service.ApplyAsync(source, preview.PreviewToken), "PREVIEW_NOT_FOUND");
var locked = await service.PreviewAsync(source, """
    bool denied = false;
    try { using var stream = System.IO.File.Open(Parameters.GetProperty("source").GetString(), System.IO.FileMode.Open, System.IO.FileAccess.Write, System.IO.FileShare.None); }
    catch (System.IO.IOException) { denied = true; }
    if (!denied) throw new Exception("Original was writable during native execution.");
    Log("Original write denied by parent read lock");
    """, JsonSerializer.Serialize(new { source }));
Require(locked.Document.Log.Single().Contains("denied"), "Original is read-locked even against full-trust code.");
await Fail(() => service.ApplyAsync(applied.OutputPath, locked.PreviewToken), "STALE_PREVIEW");
int publishedFiles = Directory.GetFiles(root).Length;
await Fail(() => service.PreviewAsync(source, "throw new Exception(\"expected-script-failure\");"), "RHINO_SCRIPT_FAILED");
await Fail(() => service.PreviewAsync(source, "System.Threading.Thread.Sleep(60000);", timeoutSeconds: 5), "RHINO_TIMEOUT");
Require(Hash(source) == before && Directory.GetFiles(root).Length == publishedFiles, "Failed/timeout scripts publish nothing.");
using (var model = File3dm.Read(source)) { model.Strings.SetString("Changed", "externally"); Require(model.Write(source, 8), "Fixture source changed for freshness test."); }
await Fail(() => service.ApplyAsync(source, locked.PreviewToken), "STALE_PREVIEW");

// Small ASCII DXF fixture: conversion-only uses the same native importer as DWG.
string dxf = Path.Combine(root, "line.dxf");
File.WriteAllText(dxf, string.Join("\n", new[] { "0", "SECTION", "2", "HEADER", "9", "$ACADVER", "1", "AC1009", "0", "ENDSEC",
    "0", "SECTION", "2", "ENTITIES", "0", "LINE", "8", "0", "10", "0", "20", "0", "30", "0", "11", "10", "21", "0", "31", "0", "0", "ENDSEC", "0", "EOF", "" }));
string dxfHash = Hash(dxf);
var conversion = await service.PreviewAsync(dxf, modelUnits: "Inches");
Require(conversion.Document.ObjectCountBefore == 1 && conversion.Document.ObjectCountAfter == 1 && conversion.Document.ModelUnits == "Inches", "Empty-script DXF conversion preserves one line and explicit units.");
var converted = await service.ApplyAsync(dxf, conversion.PreviewToken);
Require(Hash(dxf) == dxfHash && Path.GetExtension(converted.OutputPath) == ".3dm", "DXF source preserved; converted copy published.");

await using var client = await McpClient.CreateAsync(new StdioClientTransport(new() { Name = "offline-rhino-smoke", Command = router }));
var tools = await client.ListToolsAsync();
Require(tools.Any(t => t.Name == "rhino_offline_preview_rhino_script") && tools.Any(t => t.Name == "rhino_offline_apply_rhino_script"), "Native tools advertised through the supported Router.");
var p = await client.CallToolAsync("rhino_offline_preview_rhino_script", new Dictionary<string, object?> { ["filePath"] = source, ["script"] = "doc.Strings.SetString(\"NativeMcp\", \"yes\");" });
var pJson = Json(p); string token = pJson.GetProperty("previewToken").GetString()!;
var a = await client.CallToolAsync("rhino_offline_apply_rhino_script", new Dictionary<string, object?> { ["filePath"] = source, ["previewToken"] = token });
string output = Json(a).GetProperty("outputPath").GetString()!;
using (var model = File3dm.Read(output)) Require(model.Strings.GetValue("NativeMcp") == "yes", "Real stdio native preview/apply round-trip passed.");
Console.WriteLine("PASS: native Rhino startup, C# execution, source protection, exact copy publication, token/path/freshness rejection, failure/timeout and MCP round-trip.");

static JsonElement Json(CallToolResult result)
{
    string text = result.Content.OfType<TextContentBlock>().First().Text;
    if (result.IsError == true) throw new Exception(text);
    return JsonDocument.Parse(text).RootElement.Clone();
}
static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
static void Require(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS: " + message); }
static async Task Fail<T>(Func<Task<T>> action, string code)
{
    try { await action(); } catch (OfflineEditException error) when (error.Code == code) { Console.WriteLine("PASS: " + code); return; }
    throw new Exception("Expected " + code);
}
