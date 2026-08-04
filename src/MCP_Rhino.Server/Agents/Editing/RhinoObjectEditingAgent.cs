using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Editing;

namespace MCP_Rhino.Server.Agents.Editing;

public sealed class RhinoObjectEditingAgent
{
    private readonly ObjectEditPreviewSkill _previewSkill;
    private readonly ObjectEditApplySkill _applySkill;

    public RhinoObjectEditingAgent(
        ObjectEditPreviewSkill previewSkill,
        ObjectEditApplySkill applySkill)
    {
        _previewSkill = previewSkill;
        _applySkill = applySkill;
    }

    public OperationResponse<ObjectEditPreviewResponse> Preview(PreviewObjectEditsRequest request)
    {
        return _previewSkill.Preview(request);
    }

    public OperationResponse<ObjectEditExecutionResponse> Apply(ApplyObjectEditsRequest request)
    {
        return _applySkill.Apply(request);
    }
}