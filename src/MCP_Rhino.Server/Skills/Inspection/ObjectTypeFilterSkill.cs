using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;

namespace MCP_Rhino.Server.Skills.Inspection;

public sealed class ObjectTypeFilterSkill
{
    private readonly RhinoObjectFilterService _filterService;

    public ObjectTypeFilterSkill(RhinoObjectFilterService filterService)
    {
        _filterService = filterService;
    }

    public string Filter(string filePath, List<string> objectTypes)
    {
        var result = _filterService.FilterByType(new FilterObjectsByTypeRequest
        {
            FilePath = filePath,
            ObjectTypes = objectTypes
        });

        return result.Success && result.Data is not null
            ? _filterService.FormatFilterResult(result.Data)
            : result.Message;
    }
}