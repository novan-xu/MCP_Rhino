namespace MCP_Rhino.Server.Domain.Enums;

public enum TakeoffAgentMode
{
    Clarify,
    Preview,
    Export
}

public enum TakeoffAgentStatus
{
    NeedsClarification,
    PreviewReady,
    Exported,
    CapabilityGap,
    Failed
}

public enum TakeoffColumnSourceKind
{
    ObjectId,
    ObjectName,
    ObjectType,
    GeometryType,
    LayerName,
    LayerFullPath,
    MaterialName,
    UserText,
    GeometryMetric,
    Expression
}

public enum TakeoffGeometryMetric
{
    Count,
    Length,
    Area,
    Volume,
    BoundingBoxSizeX,
    BoundingBoxSizeY,
    BoundingBoxSizeZ
}

public enum TakeoffAggregateFunction
{
    Count,
    Sum,
    Min,
    Max,
    Average,
    First,
    DistinctJoin
}

public enum TakeoffSortDirection
{
    Ascending,
    Descending
}

public enum TakeoffRowMode
{
    Detail,
    Aggregate
}

public enum TakeoffSpreadsheetFormat
{
    Csv,
    Xlsx
}

public enum TakeoffNullPolicy
{
    Blank,
    Fail,
    SkipRow
}
