extern alias rhinocommon;

using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILiveLotVisualizationOperator
{
    OperationResponse<LotVisualizationPlan> Preview(RhinoDoc document, LotVisualizationSpec spec);

    OperationResponse<LotVisualizationApplyResult> Apply(RhinoDoc document, LotVisualizationPlan plan, string groupPrefix);
}
