using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services.Filters;

public sealed class LayerFilterCriterionEvaluator : IObjectFilterCriterionEvaluator
{
    public string CriterionName => "Layer";

    public bool CanEvaluate(RhinoObjectFilterCriteria criteria)
    {
        return criteria.LayerQueries.Count > 0 || criteria.LayerFullPaths.Count > 0;
    }

    public bool Evaluate(RhinoObjectInfo objectInfo, RhinoObjectFilterCriteria criteria)
    {
        bool fullPathMatch = criteria.LayerFullPaths.Count == 0
            || criteria.LayerFullPaths.Any(path => string.Equals(path, objectInfo.LayerFullPath, StringComparison.OrdinalIgnoreCase));

        bool queryMatch = criteria.LayerQueries.Count == 0
            || criteria.LayerQueries.Any(query =>
                string.Equals(query, objectInfo.LayerName, StringComparison.OrdinalIgnoreCase)
                || objectInfo.LayerFullPath.Contains(query, StringComparison.OrdinalIgnoreCase));

        return fullPathMatch && queryMatch;
    }
}