using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class TakeoffSpreadsheetAgentResponse
{
    public string FilePath { get; set; } = string.Empty;
    public TakeoffAgentStatus Status { get; set; } = TakeoffAgentStatus.NeedsClarification;
    public string StatusReason { get; set; } = string.Empty;
    public TakeoffScheduleSpecRequest? ProposedSpec { get; set; }
    public TakeoffSourcesInspectionResponse? Discovery { get; set; }
    public TakeoffSchedulePreviewResponse? Preview { get; set; }
    public TakeoffScheduleExportResponse? Export { get; set; }
    public IReadOnlyList<TakeoffClarificationQuestion> ClarificationQuestions { get; set; } = Array.Empty<TakeoffClarificationQuestion>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class TakeoffClarificationQuestion
{
    public string Id { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public IReadOnlyList<string> Options { get; set; } = Array.Empty<string>();
}

public sealed class TakeoffSourcesInspectionResponse
{
    public string FilePath { get; set; } = string.Empty;
    public int TotalObjectCount { get; set; }
    public int MatchedObjectCount { get; set; }
    public IReadOnlyList<TakeoffLayerSummaryResponse> Layers { get; set; } = Array.Empty<TakeoffLayerSummaryResponse>();
    public IReadOnlyList<TakeoffObjectTypeSummaryResponse> ObjectTypes { get; set; } = Array.Empty<TakeoffObjectTypeSummaryResponse>();
    public IReadOnlyList<TakeoffUserTextKeySummaryResponse> UserTextKeys { get; set; } = Array.Empty<TakeoffUserTextKeySummaryResponse>();
    public IReadOnlyList<TakeoffMetricAvailabilityResponse> MetricAvailability { get; set; } = Array.Empty<TakeoffMetricAvailabilityResponse>();
    public IReadOnlyList<TakeoffObjectSampleResponse> SampleObjects { get; set; } = Array.Empty<TakeoffObjectSampleResponse>();
    public IReadOnlyList<TakeoffClarificationQuestion> ClarificationQuestions { get; set; } = Array.Empty<TakeoffClarificationQuestion>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class TakeoffLayerSummaryResponse
{
    public string FullPath { get; set; } = string.Empty;
    public int ObjectCount { get; set; }
}

public sealed class TakeoffObjectTypeSummaryResponse
{
    public string ObjectType { get; set; } = string.Empty;
    public int ObjectCount { get; set; }
}

public sealed class TakeoffUserTextKeySummaryResponse
{
    public string Key { get; set; } = string.Empty;
    public int ObjectCount { get; set; }
    public int NumericValueCount { get; set; }
    public IReadOnlyList<string> SampleValues { get; set; } = Array.Empty<string>();
}

public sealed class TakeoffMetricAvailabilityResponse
{
    public TakeoffGeometryMetric Metric { get; set; }
    public int AvailableObjectCount { get; set; }
}

public sealed class TakeoffObjectSampleResponse
{
    public Guid ObjectId { get; set; }
    public string ObjectName { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public string GeometryType { get; set; } = string.Empty;
    public string LayerFullPath { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, string> UserText { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed class TakeoffSchedulePreviewResponse
{
    public string FilePath { get; set; } = string.Empty;
    public int SheetCount { get; set; }
    public IReadOnlyList<TakeoffSheetPreviewResponse> Sheets { get; set; } = Array.Empty<TakeoffSheetPreviewResponse>();
    public IReadOnlyList<TakeoffClarificationQuestion> ClarificationQuestions { get; set; } = Array.Empty<TakeoffClarificationQuestion>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class TakeoffSheetPreviewResponse
{
    public string Name { get; set; } = string.Empty;
    public TakeoffRowMode RowMode { get; set; }
    public int SourceObjectCount { get; set; }
    public int RowCount { get; set; }
    public IReadOnlyList<string> Columns { get; set; } = Array.Empty<string>();
    public IReadOnlyList<TakeoffPreviewRowResponse> SampleRows { get; set; } = Array.Empty<TakeoffPreviewRowResponse>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class TakeoffPreviewRowResponse
{
    public Dictionary<string, string> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, double> NumericValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class TakeoffScheduleExportResponse
{
    public string FilePath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public TakeoffSpreadsheetFormat Format { get; set; }
    public int SheetCount { get; set; }
    public int RowCount { get; set; }
    public long OutputFileSizeBytes { get; set; }
    public long DurationMs { get; set; }
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}
