using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Scripting;

[McpServerToolType]
public sealed class GetRhinoScriptStatusTool(RhinoScriptService service)
{
    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Read a custom Rhino script execution in this MCP connection without waiting for Rhino's main thread. Returns Queued/Running/Succeeded/Failed/Cancelled, terminal flag, cancellation request, bounded logs (100 x 4096 characters), JSON result (256 KiB), elapsed time, Undo record, error and rollback outcome. Running with cancellationRequested does not mean code has stopped. Use the same target filePath and executionId returned by apply_rhino_script; never resubmit code to poll.")]
    public OperationResponse<RhinoScriptStatusResponse> GetRhinoScriptStatus(string filePath, string executionId)
        => service.GetStatus(filePath, executionId);
}
