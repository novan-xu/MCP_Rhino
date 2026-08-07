using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models.Grasshopper;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Grasshopper;

[McpServerToolType]
public sealed class SearchGrasshopperComponentsTool
{
    private readonly RhinoGrasshopperAuthoringService _service;
    public SearchGrasshopperComponentsTool(RhinoGrasshopperAuthoringService service) => _service = service;

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Search installed visible, non-obsolete Grasshopper component proxy metadata by name, nickname, description, category, or subcategory; includes and explicitly classifies installed script/code components without creating instances.")]
    public OperationResponse<GrasshopperComponentSearchResponse> SearchGrasshopperComponents(
        string filePath,
        string query,
        int maxResults = 25,
        GrasshopperEngine engine = GrasshopperEngine.Gh1) =>
        _service.SearchComponents(new SearchGrasshopperComponentsRequest
        {
            FilePath = filePath,
            Engine = engine,
            Query = query,
            MaxResults = maxResults
        });
}
