using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class TakeoffSpreadsheetAgentRequest
{
    public string FilePath { get; set; } = string.Empty;
    public string UserRequest { get; set; } = string.Empty;
    public TakeoffScopeRequest? ScopeHints { get; set; }
    public List<TakeoffConceptMappingRequest> KnownMappings { get; set; } = new();
    public string OutputDirectory { get; set; } = string.Empty;
    public string OutputFileName { get; set; } = string.Empty;
    public bool OverwriteExisting { get; set; } = false;
    public TakeoffAgentMode Mode { get; set; } = TakeoffAgentMode.Clarify;
    public TakeoffScheduleSpecRequest? Spec { get; set; }
}

public sealed class InspectTakeoffSourcesRequest
{
    public string FilePath { get; set; } = string.Empty;
    public TakeoffScopeRequest? Scope { get; set; }
    public int SampleValueCount { get; set; } = 5;
}

public sealed class PreviewTakeoffScheduleRequest
{
    public string FilePath { get; set; } = string.Empty;
    public TakeoffScheduleSpecRequest Spec { get; set; } = new();
    public int SampleRowCount { get; set; } = 20;
}

public sealed class ExportTakeoffScheduleRequest
{
    public string FilePath { get; set; } = string.Empty;
    public string OutputDirectory { get; set; } = string.Empty;
    public string OutputFileName { get; set; } = string.Empty;
    public bool OverwriteExisting { get; set; } = false;
    public TakeoffScheduleSpecRequest Spec { get; set; } = new();
}

public sealed class TakeoffScopeRequest
{
    public List<Guid> ObjectIds { get; set; } = new();
    public bool UseCurrentSelection { get; set; } = false;
    public List<string> ConfirmedLayerFullPaths { get; set; } = new();
    public List<string> ObjectTypes { get; set; } = new();
    public List<UserAttributeConditionRequest> UserAttributeConditions { get; set; } = new();
    public FilterMatchMode MatchMode { get; set; } = FilterMatchMode.All;
    public FilterMatchMode UserAttributeMatchMode { get; set; } = FilterMatchMode.All;
}

public sealed class TakeoffConceptMappingRequest
{
    public string Concept { get; set; } = string.Empty;
    public TakeoffColumnSourceRequest Source { get; set; } = new();
}

public sealed class TakeoffScheduleSpecRequest
{
    public List<TakeoffSheetSpecRequest> Sheets { get; set; } = new();
}

public sealed class TakeoffSheetSpecRequest
{
    public string Name { get; set; } = "Takeoff";
    public TakeoffScopeRequest? Scope { get; set; }
    public List<TakeoffColumnSpecRequest> Columns { get; set; } = new();
    public List<string> GroupBy { get; set; } = new();
    public List<TakeoffAggregateSpecRequest> Aggregates { get; set; } = new();
    public List<TakeoffSortSpecRequest> SortBy { get; set; } = new();
    public TakeoffRowMode RowMode { get; set; } = TakeoffRowMode.Aggregate;
    public int MaxRows { get; set; } = 5000;
}

public sealed class TakeoffColumnSpecRequest
{
    public string Name { get; set; } = string.Empty;
    public TakeoffColumnSourceRequest Source { get; set; } = new();
    public string Unit { get; set; } = string.Empty;
    public TakeoffNullPolicy NullPolicy { get; set; } = TakeoffNullPolicy.Blank;
}

public sealed class TakeoffColumnSourceRequest
{
    public TakeoffColumnSourceKind Kind { get; set; } = TakeoffColumnSourceKind.UserText;
    public string Key { get; set; } = string.Empty;
    public TakeoffGeometryMetric Metric { get; set; } = TakeoffGeometryMetric.Area;
    public string Expression { get; set; } = string.Empty;
}

public sealed class TakeoffAggregateSpecRequest
{
    public string Name { get; set; } = string.Empty;
    public TakeoffAggregateFunction Function { get; set; } = TakeoffAggregateFunction.Count;
    public string Column { get; set; } = string.Empty;
}

public sealed class TakeoffSortSpecRequest
{
    public string Column { get; set; } = string.Empty;
    public TakeoffSortDirection Direction { get; set; } = TakeoffSortDirection.Ascending;
}
