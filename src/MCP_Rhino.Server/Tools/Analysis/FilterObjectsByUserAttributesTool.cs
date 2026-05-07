using System.ComponentModel;
using ModelContextProtocol.Server;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Skills.Inspection;

namespace MCP_Rhino.Server.Tools.Analysis;

[McpServerToolType]
public sealed class FilterObjectsByUserAttributesTool
{
    private readonly UserAttributeObjectFilterSkill _userAttributeObjectFilterSkill;

    public FilterObjectsByUserAttributesTool(UserAttributeObjectFilterSkill userAttributeObjectFilterSkill)
    {
        _userAttributeObjectFilterSkill = userAttributeObjectFilterSkill;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("按 Rhino 对象的 user key/value attributes 筛查物体。支持 Exact、Contains、Exists 三种匹配方式。")]
    public string FilterObjectsByUserAttributes(
        string filePath,
        List<UserAttributeConditionRequest> conditions,
        FilterMatchMode matchMode = FilterMatchMode.All)
    {
        return _userAttributeObjectFilterSkill.Filter(filePath, conditions, matchMode);
    }
}
