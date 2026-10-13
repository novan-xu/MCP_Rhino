using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Scripting;

[McpServerToolType]
public sealed class CancelRhinoScriptTool(RhinoScriptService service)
{
    [McpServerTool(ReadOnly = false, Destructive = true, OpenWorld = true)]
    [Description("Request cooperative cancellation of one custom Rhino script execution in this MCP connection. Queued code is skipped; running code must check Checkpoint/CancellationToken or return. Poll status for the actual terminal/rollback result. Does not kill Rhino or pretend arbitrary full-trust code has stopped. Script cancellation callbacks can execute code; ordinary target-document changes are rolled back if the owned Undo boundary remains valid. Completed executions are unchanged.")]
    public OperationResponse<RhinoScriptStatusResponse> CancelRhinoScript(string filePath, string executionId)
        => service.Cancel(filePath, executionId);
}
