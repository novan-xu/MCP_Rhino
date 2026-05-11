using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.SubD;

[McpServerToolType]
public sealed class PreviewSubDCageTool
{
    private readonly RhinoSubDModelingService _service;

    public PreviewSubDCageTool(RhinoSubDModelingService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Validate an explicit SubD cage topology without mutating Rhino. Use before CreateSubDCage to check vertex/face counts, face indices, boundary edges, non-manifold edges, and bounding box; this is geometry topology validation, not image or material interpretation.")]
    public OperationResponse<SubDCagePreviewResponse> PreviewSubDCage(
        List<SubDVertexRequest> vertices,
        List<SubDFaceRequest> faces)
    {
        return _service.PreviewCage(new PreviewSubDCageRequest
        {
            Vertices = vertices ?? new List<SubDVertexRequest>(),
            Faces = faces ?? new List<SubDFaceRequest>()
        });
    }
}
