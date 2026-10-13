using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

if (args.Length != 6) throw new ArgumentException("Arguments: router source library bake-script settings-json report-json");
string checks = Path.GetFullPath("Project_Test/261010_TEST_stone-bake-nested-outlines/NativeChecks.csx");
await using var client = await McpClient.CreateAsync(new StdioClientTransport(new() {
    Name = "stone-nested-outline-regression", Command = Path.GetFullPath(args[0]) }));
var response = await client.CallToolAsync("rhino_offline_preview_rhino_script", new Dictionary<string, object?> {
    ["filePath"] = Path.GetFullPath(args[1]), ["libraryPath"] = Path.GetFullPath(args[2]),
    ["libraryType"] = "StoneSurfaceBake", ["modelUnits"] = "Inches", ["timeoutSeconds"] = 180,
    ["script"] = File.ReadAllText(checks) + "\n" + File.ReadAllText(args[3]),
    ["parametersJson"] = File.ReadAllText(args[4])
});
string text = response.Content.OfType<TextContentBlock>().First().Text;
File.WriteAllText(args[5], text);
if (response.IsError == true) throw new Exception(text);
using var report = JsonDocument.Parse(text);
var document = report.RootElement.GetProperty("document");
Console.WriteLine(JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("PASS: Native outline regression and actual drawing bake preview. No output was published.");
