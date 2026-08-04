using System.Globalization;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services;

public sealed class TakeoffScheduleCalculationService
{
    public OperationResponse<TakeoffScheduleCalculationResult> Calculate(
        string filePath,
        TakeoffScheduleSpecRequest spec,
        IReadOnlyList<TakeoffLiveSnapshot> snapshots,
        int sampleRowCount)
    {
        if (snapshots.Count != spec.Sheets.Count)
        {
            return OperationResponse<TakeoffScheduleCalculationResult>.Fail("TAKEOFF_SNAPSHOT_MISMATCH: Sheet snapshot count did not match spec sheet count.");
        }

        var worksheets = new List<TakeoffWorksheet>(spec.Sheets.Count);
        var previews = new List<TakeoffSheetPreviewResponse>(spec.Sheets.Count);
        var warnings = new List<ObjectEditWarning>();

        for (int i = 0; i < spec.Sheets.Count; i++)
        {
            TakeoffSheetSpecRequest sheet = spec.Sheets[i];
            TakeoffLiveSnapshot snapshot = snapshots[i];
            OperationResponse<CalculatedTakeoffSheet> calculated = CalculateSheet(sheet, snapshot, Math.Max(sampleRowCount, 0));
            if (!calculated.Success || calculated.Data is null)
            {
                return OperationResponse<TakeoffScheduleCalculationResult>.Fail(calculated.Message);
            }

            worksheets.Add(calculated.Data.Worksheet);
            previews.Add(calculated.Data.Preview);
            warnings.AddRange(calculated.Data.Preview.Warnings);
        }

        var result = new TakeoffScheduleCalculationResult
        {
            Workbook = new TakeoffWorkbook { Worksheets = worksheets },
            Preview = new TakeoffSchedulePreviewResponse
            {
                FilePath = filePath,
                SheetCount = previews.Count,
                Sheets = previews,
                Warnings = warnings
            },
            Warnings = warnings
        };

        return OperationResponse<TakeoffScheduleCalculationResult>.Ok(result, "Take-off schedule calculated.");
    }

    private static OperationResponse<CalculatedTakeoffSheet> CalculateSheet(
        TakeoffSheetSpecRequest sheet,
        TakeoffLiveSnapshot snapshot,
        int sampleRowCount)
    {
        var warnings = new List<ObjectEditWarning>();
        List<TakeoffWorkbookRow> sourceRows = BuildSourceRows(sheet, snapshot.Objects, warnings, out string failureMessage);
        if (!string.IsNullOrWhiteSpace(failureMessage))
        {
            return OperationResponse<CalculatedTakeoffSheet>.Fail(failureMessage);
        }

        List<string> outputColumns;
        List<TakeoffWorkbookRow> outputRows;
        if (sheet.RowMode == TakeoffRowMode.Detail)
        {
            outputColumns = sheet.Columns.Select(column => column.Name).ToList();
            outputRows = SortRows(sourceRows, sheet.SortBy);
        }
        else
        {
            outputColumns = sheet.GroupBy
                .Concat(sheet.Aggregates.Select(aggregate => aggregate.Name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            outputRows = BuildAggregateRows(sheet, sourceRows, warnings);
            outputRows = SortRows(outputRows, sheet.SortBy);
        }

        int fullRowCount = outputRows.Count;
        if (outputRows.Count > sheet.MaxRows)
        {
            outputRows = outputRows.Take(sheet.MaxRows).ToList();
            warnings.Add(new ObjectEditWarning
            {
                Code = "TAKEOFF_ROW_LIMIT_APPLIED",
                Message = $"{sheet.Name} was capped at MaxRows={sheet.MaxRows}; original row count was {fullRowCount}."
            });
        }

        var worksheet = new TakeoffWorksheet
        {
            Name = sheet.Name,
            Columns = outputColumns,
            Rows = outputRows
        };

        var preview = new TakeoffSheetPreviewResponse
        {
            Name = sheet.Name,
            RowMode = sheet.RowMode,
            SourceObjectCount = snapshot.Objects.Count,
            RowCount = outputRows.Count,
            Columns = outputColumns,
            SampleRows = outputRows
                .Take(sampleRowCount)
                .Select(ToPreviewRow)
                .ToList(),
            Warnings = warnings
        };

        return OperationResponse<CalculatedTakeoffSheet>.Ok(new CalculatedTakeoffSheet
        {
            Worksheet = worksheet,
            Preview = preview
        });
    }

    private static List<TakeoffWorkbookRow> BuildSourceRows(
        TakeoffSheetSpecRequest sheet,
        IReadOnlyList<TakeoffObjectSnapshot> objects,
        List<ObjectEditWarning> warnings,
        out string failureMessage)
    {
        failureMessage = string.Empty;
        var rows = new List<TakeoffWorkbookRow>(objects.Count);

        foreach (TakeoffObjectSnapshot sourceObject in objects)
        {
            var row = new TakeoffWorkbookRow();
            bool skipRow = false;

            foreach (TakeoffColumnSpecRequest column in sheet.Columns)
            {
                CellResolution resolution = ResolveCell(sourceObject, row, column);
                if (resolution.Success)
                {
                    row.Cells[column.Name] = resolution.Value;
                    continue;
                }

                warnings.Add(new ObjectEditWarning
                {
                    Code = resolution.WarningCode,
                    Message = resolution.Message
                });

                if (resolution.ForceFail || column.NullPolicy == TakeoffNullPolicy.Fail)
                {
                    failureMessage = $"{resolution.WarningCode}: {resolution.Message}";
                    return rows;
                }

                if (column.NullPolicy == TakeoffNullPolicy.SkipRow)
                {
                    skipRow = true;
                    break;
                }

                row.Cells[column.Name] = TakeoffCellValue.FromString(string.Empty);
            }

            if (!skipRow)
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    private static CellResolution ResolveCell(
        TakeoffObjectSnapshot sourceObject,
        TakeoffWorkbookRow row,
        TakeoffColumnSpecRequest column)
    {
        switch (column.Source.Kind)
        {
            case TakeoffColumnSourceKind.ObjectId:
                return CellResolution.Ok(TakeoffCellValue.FromString(sourceObject.ObjectId.ToString()));
            case TakeoffColumnSourceKind.ObjectName:
                return CellResolution.Ok(TakeoffCellValue.FromString(sourceObject.ObjectName));
            case TakeoffColumnSourceKind.ObjectType:
                return CellResolution.Ok(TakeoffCellValue.FromString(sourceObject.ObjectType));
            case TakeoffColumnSourceKind.GeometryType:
                return CellResolution.Ok(TakeoffCellValue.FromString(sourceObject.GeometryType));
            case TakeoffColumnSourceKind.LayerName:
                return CellResolution.Ok(TakeoffCellValue.FromString(sourceObject.LayerName));
            case TakeoffColumnSourceKind.LayerFullPath:
                return CellResolution.Ok(TakeoffCellValue.FromString(sourceObject.LayerFullPath));
            case TakeoffColumnSourceKind.MaterialName:
                return CellResolution.Ok(TakeoffCellValue.FromString(sourceObject.MaterialName));
            case TakeoffColumnSourceKind.UserText:
                return ResolveUserText(sourceObject, column);
            case TakeoffColumnSourceKind.GeometryMetric:
                return ResolveMetric(sourceObject, column);
            case TakeoffColumnSourceKind.Expression:
                return ResolveExpression(row, column);
            default:
                return CellResolution.Fail(
                    "TAKEOFF_SOURCE_UNSUPPORTED",
                    $"Unsupported take-off source kind for column {column.Name}: {column.Source.Kind}",
                    forceFail: true);
        }
    }

    private static CellResolution ResolveUserText(TakeoffObjectSnapshot sourceObject, TakeoffColumnSpecRequest column)
    {
        if (sourceObject.UserText.TryGetValue(column.Source.Key, out string? value))
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
            {
                return CellResolution.Ok(TakeoffCellValue.FromNumber(number));
            }

            return CellResolution.Ok(TakeoffCellValue.FromString(value));
        }

        return CellResolution.Fail(
            "TAKEOFF_USER_TEXT_MISSING",
            $"Object {sourceObject.ObjectId} does not have user text key {column.Source.Key} for column {column.Name}.",
            forceFail: false);
    }

    private static CellResolution ResolveMetric(TakeoffObjectSnapshot sourceObject, TakeoffColumnSpecRequest column)
    {
        double? metricValue = column.Source.Metric switch
        {
            TakeoffGeometryMetric.Count => 1d,
            TakeoffGeometryMetric.Length => sourceObject.Metrics.Length,
            TakeoffGeometryMetric.Area => sourceObject.Metrics.Area,
            TakeoffGeometryMetric.Volume => sourceObject.Metrics.Volume,
            TakeoffGeometryMetric.BoundingBoxSizeX => sourceObject.Metrics.BoundingBoxSizeX,
            TakeoffGeometryMetric.BoundingBoxSizeY => sourceObject.Metrics.BoundingBoxSizeY,
            TakeoffGeometryMetric.BoundingBoxSizeZ => sourceObject.Metrics.BoundingBoxSizeZ,
            _ => null
        };

        return metricValue.HasValue
            ? CellResolution.Ok(TakeoffCellValue.FromNumber(metricValue.Value))
            : CellResolution.Fail(
                "TAKEOFF_METRIC_UNSUPPORTED",
                $"Object {sourceObject.ObjectId} does not support metric {column.Source.Metric} for column {column.Name}.",
                forceFail: false);
    }

    private static CellResolution ResolveExpression(TakeoffWorkbookRow row, TakeoffColumnSpecRequest column)
    {
        if (!TakeoffExpressionEvaluator.TryEvaluate(column.Source.Expression, row.Cells, out double value, out string error))
        {
            return CellResolution.Fail(
                "TAKEOFF_INVALID_EXPRESSION",
                $"Column {column.Name} expression could not be evaluated: {error}",
                forceFail: true);
        }

        return CellResolution.Ok(TakeoffCellValue.FromNumber(value));
    }

    private static List<TakeoffWorkbookRow> BuildAggregateRows(
        TakeoffSheetSpecRequest sheet,
        IReadOnlyList<TakeoffWorkbookRow> sourceRows,
        List<ObjectEditWarning> warnings)
    {
        IEnumerable<IGrouping<string, TakeoffWorkbookRow>> groups = sourceRows.GroupBy(
            row => BuildGroupKey(row, sheet.GroupBy),
            StringComparer.Ordinal);

        var aggregateRows = new List<TakeoffWorkbookRow>();
        foreach (IGrouping<string, TakeoffWorkbookRow> group in groups)
        {
            TakeoffWorkbookRow first = group.First();
            var aggregateRow = new TakeoffWorkbookRow();

            foreach (string groupColumn in sheet.GroupBy)
            {
                if (first.Cells.TryGetValue(groupColumn, out TakeoffCellValue? value))
                {
                    aggregateRow.Cells[groupColumn] = value;
                }
                else
                {
                    aggregateRow.Cells[groupColumn] = TakeoffCellValue.FromString(string.Empty);
                }
            }

            foreach (TakeoffAggregateSpecRequest aggregate in sheet.Aggregates)
            {
                aggregateRow.Cells[aggregate.Name] = CalculateAggregate(aggregate, group.ToList(), warnings);
            }

            aggregateRows.Add(aggregateRow);
        }

        return aggregateRows;
    }

    private static TakeoffCellValue CalculateAggregate(
        TakeoffAggregateSpecRequest aggregate,
        IReadOnlyList<TakeoffWorkbookRow> rows,
        List<ObjectEditWarning> warnings)
    {
        if (aggregate.Function == TakeoffAggregateFunction.Count)
        {
            return TakeoffCellValue.FromNumber(rows.Count);
        }

        List<TakeoffCellValue> values = rows
            .Select(row => row.Cells.TryGetValue(aggregate.Column, out TakeoffCellValue? value) ? value : null)
            .Where(value => value is not null)
            .Cast<TakeoffCellValue>()
            .ToList();

        switch (aggregate.Function)
        {
            case TakeoffAggregateFunction.Sum:
                return TakeoffCellValue.FromNumber(values.Where(value => value.Number.HasValue).Sum(value => value.Number!.Value));
            case TakeoffAggregateFunction.Min:
                return NumericAggregate(values, aggregate, warnings, numbers => numbers.Min());
            case TakeoffAggregateFunction.Max:
                return NumericAggregate(values, aggregate, warnings, numbers => numbers.Max());
            case TakeoffAggregateFunction.Average:
                return NumericAggregate(values, aggregate, warnings, numbers => numbers.Average());
            case TakeoffAggregateFunction.First:
                return values.FirstOrDefault() ?? TakeoffCellValue.FromString(string.Empty);
            case TakeoffAggregateFunction.DistinctJoin:
                return TakeoffCellValue.FromString(string.Join("; ",
                    values.Select(value => value.Text)
                        .Where(text => !string.IsNullOrWhiteSpace(text))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Order(StringComparer.OrdinalIgnoreCase)));
            default:
                return TakeoffCellValue.FromString(string.Empty);
        }
    }

    private static TakeoffCellValue NumericAggregate(
        IReadOnlyList<TakeoffCellValue> values,
        TakeoffAggregateSpecRequest aggregate,
        List<ObjectEditWarning> warnings,
        Func<IReadOnlyList<double>, double> calculator)
    {
        List<double> numbers = values.Where(value => value.Number.HasValue).Select(value => value.Number!.Value).ToList();
        if (numbers.Count == 0)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "TAKEOFF_AGGREGATE_NO_NUMERIC_VALUES",
                Message = $"Aggregate {aggregate.Name} had no numeric values for {aggregate.Column}."
            });
            return TakeoffCellValue.FromString(string.Empty);
        }

        return TakeoffCellValue.FromNumber(calculator(numbers));
    }

    private static List<TakeoffWorkbookRow> SortRows(
        IReadOnlyList<TakeoffWorkbookRow> rows,
        IReadOnlyList<TakeoffSortSpecRequest> sortBy)
    {
        List<TakeoffWorkbookRow> sorted = rows.ToList();
        for (int i = sortBy.Count - 1; i >= 0; i--)
        {
            TakeoffSortSpecRequest sort = sortBy[i];
            sorted = sort.Direction == TakeoffSortDirection.Descending
                ? sorted.OrderByDescending(row => GetSortValue(row, sort.Column), TakeoffSortValueComparer.Instance).ToList()
                : sorted.OrderBy(row => GetSortValue(row, sort.Column), TakeoffSortValueComparer.Instance).ToList();
        }

        return sorted;
    }

    private static TakeoffSortValue GetSortValue(TakeoffWorkbookRow row, string column)
    {
        if (!row.Cells.TryGetValue(column, out TakeoffCellValue? cell))
        {
            return new TakeoffSortValue(false, 0d, string.Empty);
        }

        return cell.Number.HasValue
            ? new TakeoffSortValue(true, cell.Number.Value, cell.Text)
            : new TakeoffSortValue(false, 0d, cell.Text);
    }

    private static string BuildGroupKey(TakeoffWorkbookRow row, IReadOnlyList<string> groupBy)
    {
        if (groupBy.Count == 0)
        {
            return "__all__";
        }

        return string.Join('\u001f', groupBy.Select(column =>
            row.Cells.TryGetValue(column, out TakeoffCellValue? value) ? value.Text : string.Empty));
    }

    private static TakeoffPreviewRowResponse ToPreviewRow(TakeoffWorkbookRow row)
    {
        var preview = new TakeoffPreviewRowResponse();
        foreach ((string column, TakeoffCellValue value) in row.Cells)
        {
            preview.Values[column] = value.Text;
            if (value.Number.HasValue)
            {
                preview.NumericValues[column] = value.Number.Value;
            }
        }

        return preview;
    }

    private sealed class CalculatedTakeoffSheet
    {
        public TakeoffWorksheet Worksheet { get; set; } = new();
        public TakeoffSheetPreviewResponse Preview { get; set; } = new();
    }

    private sealed class CellResolution
    {
        public bool Success { get; private init; }
        public TakeoffCellValue Value { get; private init; } = TakeoffCellValue.FromString(string.Empty);
        public string WarningCode { get; private init; } = string.Empty;
        public string Message { get; private init; } = string.Empty;
        public bool ForceFail { get; private init; }

        public static CellResolution Ok(TakeoffCellValue value)
        {
            return new CellResolution { Success = true, Value = value };
        }

        public static CellResolution Fail(string warningCode, string message, bool forceFail)
        {
            return new CellResolution
            {
                Success = false,
                WarningCode = warningCode,
                Message = message,
                ForceFail = forceFail
            };
        }
    }

    private readonly record struct TakeoffSortValue(bool IsNumeric, double Number, string Text);

    private sealed class TakeoffSortValueComparer : IComparer<TakeoffSortValue>
    {
        public static TakeoffSortValueComparer Instance { get; } = new();

        public int Compare(TakeoffSortValue x, TakeoffSortValue y)
        {
            if (x.IsNumeric && y.IsNumeric)
            {
                return x.Number.CompareTo(y.Number);
            }

            return string.Compare(x.Text, y.Text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class TakeoffExpressionEvaluator
    {
        private readonly string _expression;
        private readonly IReadOnlyDictionary<string, TakeoffCellValue> _cells;
        private int _index;

        private TakeoffExpressionEvaluator(string expression, IReadOnlyDictionary<string, TakeoffCellValue> cells)
        {
            _expression = expression;
            _cells = cells;
        }

        public static bool TryEvaluate(
            string expression,
            IReadOnlyDictionary<string, TakeoffCellValue> cells,
            out double value,
            out string error)
        {
            var parser = new TakeoffExpressionEvaluator(expression, cells);
            try
            {
                value = parser.ParseExpression();
                parser.SkipWhitespace();
                if (!parser.IsEnd)
                {
                    error = $"Unexpected token at position {parser._index}.";
                    return false;
                }

                error = string.Empty;
                return true;
            }
            catch (InvalidOperationException ex)
            {
                value = 0d;
                error = ex.Message;
                return false;
            }
        }

        private bool IsEnd => _index >= _expression.Length;

        private double ParseExpression()
        {
            double value = ParseTerm();
            while (true)
            {
                SkipWhitespace();
                if (TryConsume('+'))
                {
                    value += ParseTerm();
                }
                else if (TryConsume('-'))
                {
                    value -= ParseTerm();
                }
                else
                {
                    return value;
                }
            }
        }

        private double ParseTerm()
        {
            double value = ParseFactor();
            while (true)
            {
                SkipWhitespace();
                if (TryConsume('*'))
                {
                    value *= ParseFactor();
                }
                else if (TryConsume('/'))
                {
                    double divisor = ParseFactor();
                    if (Math.Abs(divisor) < double.Epsilon)
                    {
                        throw new InvalidOperationException("Division by zero.");
                    }

                    value /= divisor;
                }
                else
                {
                    return value;
                }
            }
        }

        private double ParseFactor()
        {
            SkipWhitespace();
            if (TryConsume('-'))
            {
                return -ParseFactor();
            }

            if (TryConsume('('))
            {
                double value = ParseExpression();
                SkipWhitespace();
                Require(')');
                return value;
            }

            if (TryConsume('['))
            {
                string reference = ReadUntil(']');
                Require(']');
                return ResolveReference(reference);
            }

            if (!IsEnd && (char.IsLetter(_expression[_index]) || _expression[_index] == '_'))
            {
                string identifier = ReadIdentifier();
                SkipWhitespace();
                if (TryConsume('('))
                {
                    return EvaluateFunction(identifier, ReadFunctionArguments());
                }

                return ResolveReference(identifier);
            }

            return ReadNumber();
        }

        private IReadOnlyList<double> ReadFunctionArguments()
        {
            var args = new List<double>();
            SkipWhitespace();
            if (TryConsume(')'))
            {
                return args;
            }

            while (true)
            {
                args.Add(ParseExpression());
                SkipWhitespace();
                if (TryConsume(')'))
                {
                    return args;
                }

                Require(',');
            }
        }

        private static double EvaluateFunction(string identifier, IReadOnlyList<double> args)
        {
            switch (identifier.ToLowerInvariant())
            {
                case "min":
                    if (args.Count == 0)
                    {
                        throw new InvalidOperationException("min requires at least one argument.");
                    }

                    return args.Min();
                case "max":
                    if (args.Count == 0)
                    {
                        throw new InvalidOperationException("max requires at least one argument.");
                    }

                    return args.Max();
                case "round":
                    if (args.Count is < 1 or > 2)
                    {
                        throw new InvalidOperationException("round requires one or two arguments.");
                    }

                    int digits = args.Count == 2 ? (int)Math.Round(args[1]) : 0;
                    return Math.Round(args[0], digits);
                default:
                    throw new InvalidOperationException($"Unsupported function {identifier}.");
            }
        }

        private double ResolveReference(string reference)
        {
            string trimmed = reference.Trim();
            if (!_cells.TryGetValue(trimmed, out TakeoffCellValue? cell))
            {
                throw new InvalidOperationException($"Unknown column reference {trimmed}.");
            }

            if (cell.Number.HasValue)
            {
                return cell.Number.Value;
            }

            if (double.TryParse(cell.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
            {
                return parsed;
            }

            throw new InvalidOperationException($"Column reference {trimmed} is not numeric.");
        }

        private double ReadNumber()
        {
            SkipWhitespace();
            int start = _index;
            while (!IsEnd && (char.IsDigit(_expression[_index]) || _expression[_index] == '.'))
            {
                _index++;
            }

            if (start == _index)
            {
                throw new InvalidOperationException($"Expected number or column reference at position {_index}.");
            }

            string token = _expression[start.._index];
            return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                ? value
                : throw new InvalidOperationException($"Invalid number {token}.");
        }

        private string ReadIdentifier()
        {
            int start = _index;
            while (!IsEnd && (char.IsLetterOrDigit(_expression[_index]) || _expression[_index] == '_'))
            {
                _index++;
            }

            return _expression[start.._index];
        }

        private string ReadUntil(char terminator)
        {
            int start = _index;
            while (!IsEnd && _expression[_index] != terminator)
            {
                _index++;
            }

            if (IsEnd)
            {
                throw new InvalidOperationException($"Missing closing {terminator}.");
            }

            return _expression[start.._index];
        }

        private void Require(char expected)
        {
            if (!TryConsume(expected))
            {
                throw new InvalidOperationException($"Expected {expected} at position {_index}.");
            }
        }

        private bool TryConsume(char expected)
        {
            SkipWhitespace();
            if (IsEnd || _expression[_index] != expected)
            {
                return false;
            }

            _index++;
            return true;
        }

        private void SkipWhitespace()
        {
            while (!IsEnd && char.IsWhiteSpace(_expression[_index]))
            {
                _index++;
            }
        }
    }
}

public sealed class TakeoffScheduleCalculationResult
{
    public TakeoffWorkbook Workbook { get; set; } = new();
    public TakeoffSchedulePreviewResponse Preview { get; set; } = new();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}
