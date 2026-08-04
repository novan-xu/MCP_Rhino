using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Skills.Inspection;

namespace MCP_Rhino.Server.Agents.Inspection;

public sealed class RhinoObjectFilterAgent
{
    private readonly CompositeObjectFilterSkill _compositeObjectFilterSkill;

    public RhinoObjectFilterAgent(CompositeObjectFilterSkill compositeObjectFilterSkill)
    {
        _compositeObjectFilterSkill = compositeObjectFilterSkill;
    }

    public string Filter(FilterObjectsRequest request)
    {
        return _compositeObjectFilterSkill.Filter(request);
    }
}