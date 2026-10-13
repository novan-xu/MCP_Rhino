using System.Text.Json;
using System.IO;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoScriptService(ILiveRhinoScriptExecutor executor)
{
    public OperationResponse<RhinoScriptPreviewResponse> Preview(RhinoScriptRequest request)
    {
        if (!ValidPath(request.FilePath)) return OperationResponse<RhinoScriptPreviewResponse>.Fail("Absolute live document filePath is required.");
        if (string.IsNullOrWhiteSpace(request.Script) || request.Script.Length > 131072)
            return OperationResponse<RhinoScriptPreviewResponse>.Fail("C# script must contain 1–131072 characters.");
        if (request.TimeoutSeconds is < 1 or > 600)
            return OperationResponse<RhinoScriptPreviewResponse>.Fail("timeoutSeconds must be 1–600 (cooperative).");
        if (request.ParametersJson is null || request.ParametersJson.Length > 65536)
            return OperationResponse<RhinoScriptPreviewResponse>.Fail("parametersJson must be a JSON object of at most 65536 characters.");
        try
        {
            using var parameters = JsonDocument.Parse(request.ParametersJson);
            if (parameters.RootElement.ValueKind != JsonValueKind.Object)
                return OperationResponse<RhinoScriptPreviewResponse>.Fail("parametersJson must be a JSON object.");
        }
        catch (JsonException ex) { return OperationResponse<RhinoScriptPreviewResponse>.Fail(ex.Message); }
        return executor.Preview(request);
    }

    public OperationResponse<RhinoScriptStatusResponse> Apply(string filePath, string token)
        => ValidTarget(filePath, token) ? executor.Apply(filePath, token) : InvalidTarget();
    public OperationResponse<RhinoScriptStatusResponse> GetStatus(string filePath, string id)
        => ValidTarget(filePath, id) ? executor.GetStatus(filePath, id) : InvalidTarget();
    public OperationResponse<RhinoScriptStatusResponse> Cancel(string filePath, string id)
        => ValidTarget(filePath, id) ? executor.Cancel(filePath, id) : InvalidTarget();

    private static bool ValidPath(string path) => !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path);
    private static bool ValidTarget(string path, string id) => ValidPath(path) && Guid.TryParseExact(id, "N", out _);
    private static OperationResponse<RhinoScriptStatusResponse> InvalidTarget()
        => OperationResponse<RhinoScriptStatusResponse>.Fail("Absolute live filePath and the opaque token/id returned in this MCP connection are required.");
}
