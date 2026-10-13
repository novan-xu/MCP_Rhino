using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Scripting;

[McpServerToolType]
public sealed class PreviewRhinoScriptTool(RhinoScriptService service)
{
    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Compile trusted synchronous inline C# for the Router-selected open saved Rhino document without executing it. Use custom scripts for ad hoc operations missing from atomic tools. Globals: doc (explicit routed RhinoDoc, never ActiveDoc), Parameters (JsonElement from parametersJson object), Result (JSON-serializable), Log(string), CancellationToken, Checkpoint(). RhinoCommon and standard System/Linq/Collections/Json imports are provided. No #r/#load, async/await, background Rhino work, manual Undo, commands, switching/saving/closing documents. Returns compilation diagnostics, hash, target serial and ten-minute token; this is not a geometry simulation. Apply uses current live state, so code must validate modeling assumptions. Limit 128 KiB source, 64 KiB parameters, 16 retained submissions per backend connection. timeoutSeconds 1–600 is cooperative. Full-trust execution is not sandboxed.")]
    public OperationResponse<RhinoScriptPreviewResponse> PreviewRhinoScript(string filePath, string script, string parametersJson = "{}", int timeoutSeconds = 60)
        => service.Preview(new RhinoScriptRequest { FilePath = filePath, Script = script, ParametersJson = parametersJson, TimeoutSeconds = timeoutSeconds });
}
