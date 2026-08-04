using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services.Filters;

public sealed class UserAttributeFilterCriterionEvaluator : IObjectFilterCriterionEvaluator
{
    public string CriterionName => "UserAttributes";

    public bool CanEvaluate(RhinoObjectFilterCriteria criteria)
    {
        return criteria.UserAttributeConditions.Count > 0;
    }

    public bool Evaluate(RhinoObjectInfo objectInfo, RhinoObjectFilterCriteria criteria)
    {
        IEnumerable<bool> evaluations = criteria.UserAttributeConditions.Select(condition => EvaluateCondition(objectInfo, condition));
        return criteria.UserAttributeMatchMode == FilterMatchMode.All
            ? evaluations.All(result => result)
            : evaluations.Any(result => result);
    }

    private static bool EvaluateCondition(RhinoObjectInfo objectInfo, RhinoUserAttributeCondition condition)
    {
        string? value = objectInfo.GetUserAttributeValue(condition.Key);

        return condition.ComparisonMode switch
        {
            UserAttributeComparisonMode.Exists => value is not null,
            UserAttributeComparisonMode.Contains => value is not null
                && value.Contains(condition.ExpectedValue ?? string.Empty, StringComparison.OrdinalIgnoreCase),
            _ => value is not null
                && string.Equals(value, condition.ExpectedValue ?? string.Empty, StringComparison.OrdinalIgnoreCase)
        };
    }
}