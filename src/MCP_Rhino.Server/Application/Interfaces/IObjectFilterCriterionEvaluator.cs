using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface IObjectFilterCriterionEvaluator
{
    string CriterionName { get; }
    bool CanEvaluate(RhinoObjectFilterCriteria criteria);
    bool Evaluate(RhinoObjectInfo objectInfo, RhinoObjectFilterCriteria criteria);
}