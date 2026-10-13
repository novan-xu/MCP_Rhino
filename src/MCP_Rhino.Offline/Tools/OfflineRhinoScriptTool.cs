using System.ComponentModel;
using System.Text.Json;
using MCP_Rhino.Offline.Application;
using MCP_Rhino.Offline.Contracts;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Offline.Tools;

[McpServerToolType]
public sealed class OfflineRhinoScriptTool(OfflineRhinoScriptService service)
{
    [McpServerTool(Name = "rhino_offline_preview_rhino_script", ReadOnly = false, Destructive = true, OpenWorld = true)]
    [Description("Explicit offline DWG/DXF/3DM processing through licensed Rhino 8 on Windows, in an isolated no-window worker. Source is read-locked and a temporary snapshot is imported; no final copy until apply. Empty script converts/inspects only. Otherwise executes TRUSTED FULL-TRUST synchronous C# (not a sandbox): globals doc (RhinoDoc), Parameters (JsonElement), Result (JSON-serializable object), Log(string). No await or active-document routing. Optional absolute libraryPath .cs/.csx plus libraryType extracts that named class without running a Grasshopper top-level entry. Optional modelUnits (e.g. Inches) controls DWG import; 3dm keeps native units. parametersJson must be a JSON object. timeoutSeconds 1–600. Source/script/library hashes and counts returned with a ten-minute one-use token; publish via rhino_offline_apply_rhino_script in this Router session. Do not run untrusted C#; full-trust code can affect the OS. Does not use an open document or provide live-to-offline fallback.")]
    public Task<CallToolResult> PreviewRhinoScript(string filePath, string script = "", string parametersJson = "{}",
        string? libraryPath = null, string? libraryType = null, string? modelUnits = null, int timeoutSeconds = 120,
        CancellationToken cancellationToken = default) => Execute(async () =>
            await service.PreviewAsync(filePath, script, parametersJson, libraryPath, libraryType, modelUnits, timeoutSeconds, cancellationToken));

    [McpServerTool(Name = "rhino_offline_apply_rhino_script", ReadOnly = false, Destructive = false, OpenWorld = true)]
    [Description("Publish an exact headless Rhino preview as a NEW sibling <stem>_YYYYMMDD_HHMMSS.3dm using local date and 24-hour time, preserving the original DWG/DXF/3DM and all prior copies. Same-second collisions wait for the next available second (bounded, cancellable); never overwrite. Requires original absolute filePath and previewToken from rhino_offline_preview_rhino_script in this same Router session within ten minutes. Rechecks source hash under a read-only lock; never reruns C#. Returns outputPath; subsequent edits create another copy without accumulating suffixes.")]
    public Task<CallToolResult> ApplyRhinoScript(string filePath, string previewToken, CancellationToken cancellationToken = default) =>
        Execute(async () => await service.ApplyAsync(filePath, previewToken, cancellationToken));

    private static async Task<CallToolResult> Execute(Func<Task<object>> action)
    {
        try { return Result(await action(), false); }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) when (error is not OutOfMemoryException)
        { return Result(new { code = error is OfflineEditException offline ? offline.Code : "OFFLINE_RHINO_FAILED", message = error.Message }, true); }
    }
    private static CallToolResult Result(object value, bool error)
    {
        var json = JsonSerializer.SerializeToElement(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return new() { IsError = error, StructuredContent = json, Content = [new TextContentBlock { Text = json.GetRawText() }] };
    }
}
