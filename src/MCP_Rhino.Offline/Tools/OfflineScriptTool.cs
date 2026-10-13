using System.ComponentModel;
using System.Text.Json;
using MCP_Rhino.Offline.Application;
using MCP_Rhino.Offline.Contracts;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Offline.Tools;

[McpServerToolType]
public sealed class OfflineScriptTool(OfflineScriptService service)
{
    [McpServerTool(Name = "rhino_offline_inspect_document", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Inspect saved .3dm disk state without Rhino: object UUIDs, bounds, attributes, layers, document user text. Absolute filePath; objects are paged (limit 1–1000). Does not see unsaved live edits. Use live tools for open documents. Offline scripting help and limits are returned with the result.")]
    public CallToolResult InspectDocument(string filePath, int offset = 0, int limit = 200) => Execute(() =>
        new { result = service.Inspect(filePath, offset, limit), scripting = ScriptHelp });

    [McpServerTool(Name = "rhino_offline_preview_script", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Preview JavaScript edits to a saved .3dm entirely in memory, with no disk writes or Rhino required. Use rhino_offline_inspect_document first for script help and UUIDs. The restricted model object supports filtering via JS, attributes/user text, layers, affine transforms, delete and point/line/polyline/sphere/box creation. No filesystem, CLR, imports, process, network, Rhino commands, Grasshopper or booleans. Returns a ten-minute, one-use previewToken for the exact validated result; no automatic live fallback.")]
    public CallToolResult PreviewScript(string filePath, string script, CancellationToken cancellationToken) =>
        Execute(() => service.PreviewScript(filePath, script, cancellationToken));

    [McpServerTool(Name = "rhino_offline_apply_script", ReadOnly = false, Destructive = false, OpenWorld = true)]
    [Description("Publish the exact offline script preview as a NEW sibling <stem>_YYYYMMDD_HHMMSS.3dm using local date and 24-hour time. Same-second collisions wait for the next available second (bounded, cancellable); never overwrite. Requires absolute source filePath and an unexpired previewToken from this Router session. Refuses changed sources. No output-path/overwrite option. Consumes token, returns outputPath; use that path for further offline edits, which create another copy without accumulating suffixes. Does not rerun the script or save any live document.")]
    public CallToolResult ApplyScript(string filePath, string previewToken, CancellationToken cancellationToken) =>
        Execute(() => service.ApplyScript(filePath, previewToken, cancellationToken));

    public static readonly string ScriptHelp = """
        JavaScript only; a fresh restricted engine per preview. Read-only saved source, no host filesystem/process/network/CLR/modules/eval.
        model.objects(offset=0, limit=200) -> {total,offset,items}; each item has id,type,name,layerIndex,visible,isDefinitionGeometry,color,colorSource,userText,bounds [minX,minY,minZ,maxX,maxY,maxZ]. Page through all objects as needed.
        model.layers() -> [{id,index,name,path,parentId,color,visible,locked}]. model.documentUserText() -> key/value object.
        model.setAttributes([uuid,...], {name?,layerId?,color?:'#RRGGBB',visible?,userText?:{key:'value',removeKey:null}}).
        model.setDocumentUserText(key, value); null deletes the key.
        model.transform([uuid,...], [16 row-major affine matrix values]); translation is at indices 3,7,11. Invertible matrices only.
        model.deleteObjects([uuid,...]); block-definition geometry is excluded from edits/deletion/transforms.
        model.addLayer(name,color='#000000',parentId=null) -> layer UUID. model.editLayer(id,{name?,color?,visible?,locked?}).
        model.addPoint([x,y,z],attributes={}); model.addLine(start,end,attributes={}); model.addPolyline([[x,y,z],...],attributes={});
        model.addSphere(center,radius,attributes={}); model.addBox(min,max,attributes={}); each returns object UUID. Coordinates use existing model units.
        model.log(message) adds a preview log message. Scripts may loop, branch, calculate, and use JS functions.
        Limits: 64 KiB script, 100000 statements, 5s JS deadline, 32 MB JS allocations, 10000 model calls/mutations, 100 logs, 256 MiB source/output. Native geometry work also counts against model call limits. A failed model call aborts the preview even if caught by JS.
        Preview retains exact output bytes for ten minutes (four previews / 512 MiB per Router). Apply publishes those bytes once; inspect/open the returned copy. Original and earlier copies remain unchanged. Advanced Rhino modeling and plug-in execution require live tools. This is a constrained modeling runtime, not general Python/C# execution or an OS sandbox for hostile code.
        Collect IDs before adding/deleting/transforming while paging; those operations can change object table order.
        Example: for (const o of model.objects().items) { if (!o.isDefinitionGeometry && o.name.startsWith('Panel')) model.setAttributes([o.id], {userText:{Reviewed:'Yes'},color:'#4488CC'}); } model.log('Panel attributes updated');
        """;

    private static CallToolResult Execute(Func<object> action)
    {
        try { return Result(action(), false); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return Result(new { code = ex is OfflineEditException offline ? offline.Code : "OFFLINE_SCRIPT_FAILED", message = ex.Message }, true);
        }
    }

    private static CallToolResult Result(object value, bool error)
    {
        JsonElement json = JsonSerializer.SerializeToElement(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return new() { IsError = error, StructuredContent = json, Content = [new TextContentBlock { Text = json.GetRawText() }] };
    }
}
