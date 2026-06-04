using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Application.Services;

public sealed class TakeoffScheduleValidationService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csv",
        ".xlsx"
    };

    public OperationResponse ValidateFilePath(string filePath)
    {
        return string.IsNullOrWhiteSpace(filePath)
            ? OperationResponse.Fail("FilePath is required.")
            : OperationResponse.Ok();
    }

    public OperationResponse ValidateSpec(TakeoffScheduleSpecRequest? spec)
    {
        if (spec is null || spec.Sheets.Count == 0)
        {
            return OperationResponse.Fail("TAKEOFF_SPEC_REQUIRED: At least one take-off sheet is required.");
        }

        for (int sheetIndex = 0; sheetIndex < spec.Sheets.Count; sheetIndex++)
        {
            TakeoffSheetSpecRequest sheet = spec.Sheets[sheetIndex];
            string sheetLabel = string.IsNullOrWhiteSpace(sheet.Name) ? $"Sheet {sheetIndex + 1}" : sheet.Name;
            if (string.IsNullOrWhiteSpace(sheet.Name))
            {
                return OperationResponse.Fail($"TAKEOFF_SHEET_NAME_REQUIRED: Sheet {sheetIndex + 1} needs a name.");
            }

            if (sheet.Name.Length > 31)
            {
                return OperationResponse.Fail($"TAKEOFF_SHEET_NAME_INVALID: Sheet name is longer than 31 characters: {sheet.Name}");
            }

            if (sheet.Columns.Count == 0)
            {
                return OperationResponse.Fail($"TAKEOFF_COLUMNS_REQUIRED: {sheetLabel} needs at least one column.");
            }

            var columnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (TakeoffColumnSpecRequest column in sheet.Columns)
            {
                if (string.IsNullOrWhiteSpace(column.Name))
                {
                    return OperationResponse.Fail($"TAKEOFF_COLUMN_NAME_REQUIRED: {sheetLabel} contains a column without a name.");
                }

                if (!columnNames.Add(column.Name))
                {
                    return OperationResponse.Fail($"TAKEOFF_DUPLICATE_COLUMN: {sheetLabel} contains duplicate column name {column.Name}.");
                }

                OperationResponse columnValidation = ValidateColumnSource(sheetLabel, column);
                if (!columnValidation.Success)
                {
                    return columnValidation;
                }
            }

            foreach (string groupColumn in sheet.GroupBy)
            {
                if (!columnNames.Contains(groupColumn))
                {
                    return OperationResponse.Fail($"TAKEOFF_GROUP_COLUMN_UNKNOWN: {sheetLabel} groups by unknown column {groupColumn}.");
                }
            }

            var aggregateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (TakeoffAggregateSpecRequest aggregate in sheet.Aggregates)
            {
                if (string.IsNullOrWhiteSpace(aggregate.Name))
                {
                    return OperationResponse.Fail($"TAKEOFF_AGGREGATE_NAME_REQUIRED: {sheetLabel} contains an aggregate without a name.");
                }

                if (!aggregateNames.Add(aggregate.Name))
                {
                    return OperationResponse.Fail($"TAKEOFF_DUPLICATE_AGGREGATE: {sheetLabel} contains duplicate aggregate name {aggregate.Name}.");
                }

                if (aggregate.Function != TakeoffAggregateFunction.Count
                    && string.IsNullOrWhiteSpace(aggregate.Column))
                {
                    return OperationResponse.Fail($"TAKEOFF_AGGREGATE_COLUMN_REQUIRED: {aggregate.Name} needs a source column.");
                }

                if (!string.IsNullOrWhiteSpace(aggregate.Column) && !columnNames.Contains(aggregate.Column))
                {
                    return OperationResponse.Fail($"TAKEOFF_AGGREGATE_COLUMN_UNKNOWN: {aggregate.Name} references unknown column {aggregate.Column}.");
                }
            }

            foreach (TakeoffSortSpecRequest sort in sheet.SortBy)
            {
                bool known = columnNames.Contains(sort.Column) || aggregateNames.Contains(sort.Column);
                if (string.IsNullOrWhiteSpace(sort.Column) || !known)
                {
                    return OperationResponse.Fail($"TAKEOFF_SORT_COLUMN_UNKNOWN: {sheetLabel} sorts by unknown column {sort.Column}.");
                }
            }

            if (sheet.MaxRows <= 0)
            {
                return OperationResponse.Fail($"TAKEOFF_MAX_ROWS_INVALID: {sheetLabel} MaxRows must be greater than zero.");
            }
        }

        return OperationResponse.Ok();
    }

    public OperationResponse<TakeoffValidatedOutput> ValidateOutput(
        string activeFilePath,
        string outputDirectory,
        string outputFileName,
        bool overwriteExisting)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory) || string.IsNullOrWhiteSpace(outputFileName))
        {
            return OperationResponse<TakeoffValidatedOutput>.Fail(
                "TAKEOFF_OUTPUT_REQUIRED: Export requires both outputDirectory and outputFileName.");
        }

        if (!Path.IsPathFullyQualified(outputDirectory))
        {
            return OperationResponse<TakeoffValidatedOutput>.Fail(
                "TAKEOFF_OUTPUT_DIRECTORY_INVALID: outputDirectory must be an absolute path.");
        }

        if (!Directory.Exists(outputDirectory))
        {
            return OperationResponse<TakeoffValidatedOutput>.Fail(
                $"TAKEOFF_OUTPUT_DIRECTORY_NOT_FOUND: outputDirectory does not exist: {outputDirectory}");
        }

        if (outputFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || !string.Equals(Path.GetFileName(outputFileName), outputFileName, StringComparison.Ordinal))
        {
            return OperationResponse<TakeoffValidatedOutput>.Fail(
                "TAKEOFF_OUTPUT_FILE_NAME_INVALID: outputFileName must be a file name only, with no path traversal or invalid characters.");
        }

        string extension = Path.GetExtension(outputFileName);
        if (!AllowedExtensions.Contains(extension))
        {
            return OperationResponse<TakeoffValidatedOutput>.Fail(
                "TAKEOFF_OUTPUT_EXTENSION_UNSUPPORTED: outputFileName must end in .csv or .xlsx.");
        }

        string finalPath = Path.GetFullPath(Path.Combine(outputDirectory, outputFileName));
        string normalizedDirectory = Path.GetFullPath(outputDirectory);
        if (!finalPath.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return OperationResponse<TakeoffValidatedOutput>.Fail(
                "TAKEOFF_OUTPUT_FILE_NAME_INVALID: outputFileName resolved outside outputDirectory.");
        }

        if (!string.IsNullOrWhiteSpace(activeFilePath)
            && string.Equals(Path.GetFullPath(activeFilePath), finalPath, StringComparison.OrdinalIgnoreCase))
        {
            return OperationResponse<TakeoffValidatedOutput>.Fail(
                "TAKEOFF_OUTPUT_ACTIVE_FILE_BLOCKED: outputPath cannot be the active .3dm file path.");
        }

        if (File.Exists(finalPath) && !overwriteExisting)
        {
            return OperationResponse<TakeoffValidatedOutput>.Fail(
                "TAKEOFF_OUTPUT_OVERWRITE_BLOCKED: output file exists and overwriteExisting is false.");
        }

        return OperationResponse<TakeoffValidatedOutput>.Ok(new TakeoffValidatedOutput
        {
            OutputPath = finalPath,
            Format = string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase)
                ? TakeoffSpreadsheetFormat.Xlsx
                : TakeoffSpreadsheetFormat.Csv
        });
    }

    private static OperationResponse ValidateColumnSource(string sheetLabel, TakeoffColumnSpecRequest column)
    {
        switch (column.Source.Kind)
        {
            case TakeoffColumnSourceKind.UserText:
                return string.IsNullOrWhiteSpace(column.Source.Key)
                    ? OperationResponse.Fail($"TAKEOFF_ATTRIBUTE_MAPPING_REQUIRED: {sheetLabel} column {column.Name} needs a user text key.")
                    : OperationResponse.Ok();

            case TakeoffColumnSourceKind.Expression:
                return string.IsNullOrWhiteSpace(column.Source.Expression)
                    ? OperationResponse.Fail($"TAKEOFF_EXPRESSION_REQUIRED: {sheetLabel} column {column.Name} needs an expression.")
                    : OperationResponse.Ok();

            default:
                return OperationResponse.Ok();
        }
    }
}

public sealed class TakeoffValidatedOutput
{
    public string OutputPath { get; set; } = string.Empty;
    public TakeoffSpreadsheetFormat Format { get; set; }
}
