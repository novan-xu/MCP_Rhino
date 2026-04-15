using System.ComponentModel;
using ModelContextProtocol.Server;
using MCP_Rhino.Server.Skills.Inspection;

namespace MCP_Rhino.Server.Tools.Analysis;

[McpServerToolType]
public sealed class FilterObjectsByTypeTool
{
    private readonly ObjectTypeFilterSkill _objectTypeFilterSkill;

    public FilterObjectsByTypeTool(ObjectTypeFilterSkill objectTypeFilterSkill)
    {
        _objectTypeFilterSkill = objectTypeFilterSkill;
    }

    [McpServerTool]
    [Description("按对象类型筛查 Rhino 物体，例如 points、surfaces、breps、blocks、annotation dots 等。")]
    public string FilterObjectsByType(string filePath, List<string> objectTypes)
    {
        return _objectTypeFilterSkill.Filter(filePath, objectTypes);
    }
}