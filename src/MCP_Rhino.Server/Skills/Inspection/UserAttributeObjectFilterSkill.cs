using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Skills.Inspection;

public sealed class UserAttributeObjectFilterSkill
{
    private readonly RhinoObjectFilterService _filterService;

    public UserAttributeObjectFilterSkill(RhinoObjectFilterService filterService)
    {
        _filterService = filterService;
    }

    public string Filter(string filePath, List<UserAttributeConditionRequest> conditions, FilterMatchMode matchMode = FilterMatchMode.All)
    {
        var result = _filterService.FilterByUserAttributes(new FilterObjectsByUserAttributesRequest
        {
            FilePath = filePath,
            Conditions = conditions,
            MatchMode = matchMode
        });

        return result.Success && result.Data is not null
            ? _filterService.FormatFilterResult(result.Data)
            : result.Message;
    }
}