using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILiveRhinoScriptExecutor
{
    OperationResponse<RhinoScriptPreviewResponse> Preview(RhinoScriptRequest request);
    OperationResponse<RhinoScriptStatusResponse> Apply(string filePath, string previewToken);
    OperationResponse<RhinoScriptStatusResponse> GetStatus(string filePath, string executionId);
    OperationResponse<RhinoScriptStatusResponse> Cancel(string filePath, string executionId);
}
