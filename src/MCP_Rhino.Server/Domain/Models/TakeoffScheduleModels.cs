using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class TakeoffObjectSnapshot
{
    public Guid ObjectId { get; set; }
    public string ObjectName { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public string GeometryType { get; set; } = string.Empty;
    public string LayerName { get; set; } = string.Empty;
    public string LayerFullPath { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
    public IReadOnlyDictionary<string, string> UserText { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public TakeoffGeometryMetrics Metrics { get; set; } = new();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class TakeoffGeometryMetrics
{
    public double? Length { get; set; }
    public double? Area { get; set; }
    public double? Volume { get; set; }
    public double? BoundingBoxSizeX { get; set; }
    public double? BoundingBoxSizeY { get; set; }
    public double? BoundingBoxSizeZ { get; set; }
}

public sealed class TakeoffLiveSnapshot
{
    public string FilePath { get; set; } = string.Empty;
    public int TotalObjectCount { get; set; }
    public int MatchedObjectCount { get; set; }
    public IReadOnlyList<TakeoffObjectSnapshot> Objects { get; set; } = Array.Empty<TakeoffObjectSnapshot>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class TakeoffWorkbook
{
    public IReadOnlyList<TakeoffWorksheet> Worksheets { get; set; } = Array.Empty<TakeoffWorksheet>();
}

public sealed class TakeoffSpreadsheetWriteResult
{
    public string OutputPath { get; set; } = string.Empty;
    public long ByteCount { get; set; }
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class TakeoffWorksheet
{
    public string Name { get; set; } = "Takeoff";
    public IReadOnlyList<string> Columns { get; set; } = Array.Empty<string>();
    public IReadOnlyList<TakeoffWorkbookRow> Rows { get; set; } = Array.Empty<TakeoffWorkbookRow>();
}

public sealed class TakeoffWorkbookRow
{
    public Dictionary<string, TakeoffCellValue> Cells { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class TakeoffCellValue
{
    public string Text { get; set; } = string.Empty;
    public double? Number { get; set; }

    public static TakeoffCellValue FromString(string? text)
    {
        return new TakeoffCellValue { Text = text ?? string.Empty };
    }

    public static TakeoffCellValue FromNumber(double value)
    {
        return new TakeoffCellValue
        {
            Text = value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Number = value
        };
    }
}
