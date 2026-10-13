using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Scripting;

[McpServerToolType]
public sealed class ApplyRhinoScriptTool(RhinoScriptService service)
{
    [McpServerTool(ReadOnly = false, Destructive = true, OpenWorld = true)]
    [Description("Execute the exact trusted C# submission from preview_rhino_script once in the same MCP connection and target document. Returns an executionId promptly; poll get_rhino_script_status until terminal. Repeating the same token returns the same execution and never reruns it. Runs synchronously on Rhino main thread using the routed doc, one owned Undo record, and ordinary target-document rollback on failure/cancellation. Full-trust code may affect external state, which cannot be rolled back; no sandbox or forced termination. Timeout/cancellation requires Checkpoint or CancellationToken cooperation (also checked on return). No automatic file save, UI activation, COM, script-server setup or offline fallback. Document must be idle with Undo enabled. Disconnect requests cancellation, never replay; status/token retention is scoped to this backend connection.")]
    public OperationResponse<RhinoScriptStatusResponse> ApplyRhinoScript(string filePath, string previewToken)
        => service.Apply(filePath, previewToken);
}
