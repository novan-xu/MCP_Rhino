using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Infrastructure.Reference;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Reference;

[McpServerToolType]
public sealed class GetRhinoReferenceFunctionTool
{
    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Get curated RhinoCommon function notes by exact function name. This is reference-only and does not execute RhinoScript, C#, macros, or commands.")]
    public OperationResponse<RhinoReferenceFunctionDetailResponse> GetRhinoReferenceFunction(string functionName)
    {
        RhinoReferenceFunctionDetailResponse? function = RhinoReferenceIndex.GetFunction(functionName);
        return function is null
            ? OperationResponse<RhinoReferenceFunctionDetailResponse>.Fail($"Rhino reference function was not found: {functionName}")
            : OperationResponse<RhinoReferenceFunctionDetailResponse>.Ok(function);
    }
}
