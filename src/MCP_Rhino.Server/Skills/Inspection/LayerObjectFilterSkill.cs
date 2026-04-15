using MCP_Rhino.Server.Application.Services;

namespace MCP_Rhino.Server.Skills.Inspection;

public sealed class LayerObjectFilterSkill
{
    private readonly CompositeObjectFilterSkill _compositeObjectFilterSkill;

    public LayerObjectFilterSkill(CompositeObjectFilterSkill compositeObjectFilterSkill)
    {
        _compositeObjectFilterSkill = compositeObjectFilterSkill;
    }

    public string Filter(string filePath, string layerQuery, string? confirmedLayerFullPath = null)
    {
        return _compositeObjectFilterSkill.Filter(new Contracts.Requests.FilterObjectsRequest
        {
            FilePath = filePath,
            LayerQueries = new List<string> { layerQuery },
            ConfirmedLayerFullPaths = string.IsNullOrWhiteSpace(confirmedLayerFullPath)
                ? new List<string>()
                : new List<string> { confirmedLayerFullPath }
        });
    }
}