using System.Text;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Infrastructure.Spreadsheet;

public sealed class CsvSpreadsheetWorkbookWriter : ISpreadsheetWorkbookWriter
{
    public bool Supports(TakeoffSpreadsheetFormat format)
    {
        return format == TakeoffSpreadsheetFormat.Csv;
    }

    public OperationResponse<TakeoffSpreadsheetWriteResult> Write(
        TakeoffWorkbook workbook,
        TakeoffSpreadsheetFormat format,
        string outputPath,
        bool overwriteExisting)
    {
        if (!Supports(format))
        {
            return OperationResponse<TakeoffSpreadsheetWriteResult>.Fail($"CSV writer does not support format {format}.");
        }

        if (workbook.Worksheets.Count != 1)
        {
            return OperationResponse<TakeoffSpreadsheetWriteResult>.Fail(
                "TAKEOFF_CSV_REQUIRES_SINGLE_SHEET: CSV export supports exactly one worksheet.");
        }

        if (System.IO.File.Exists(outputPath) && !overwriteExisting)
        {
            return OperationResponse<TakeoffSpreadsheetWriteResult>.Fail(
                "TAKEOFF_OUTPUT_OVERWRITE_BLOCKED: output file exists and overwriteExisting is false.");
        }

        var warnings = new List<ObjectEditWarning>();
        string tempPath = BuildTempPath(outputPath);
        try
        {
            TakeoffWorksheet worksheet = workbook.Worksheets[0];
            using (var writer = new StreamWriter(tempPath, false, Encoding.UTF8))
            {
                writer.WriteLine(string.Join(",", worksheet.Columns.Select(EscapeCsv)));
                foreach (TakeoffWorkbookRow row in worksheet.Rows)
                {
                    IEnumerable<string> fields = worksheet.Columns.Select(column =>
                    {
                        row.Cells.TryGetValue(column, out TakeoffCellValue? cell);
                        return EscapeCsv(SanitizeCell(cell, warnings));
                    });
                    writer.WriteLine(string.Join(",", fields));
                }
            }

            ReplaceOutput(tempPath, outputPath, overwriteExisting);
            long byteCount = new FileInfo(outputPath).Length;
            if (byteCount <= 0L)
            {
                return OperationResponse<TakeoffSpreadsheetWriteResult>.Fail("TAKEOFF_OUTPUT_EMPTY: CSV writer produced an empty file.");
            }

            return OperationResponse<TakeoffSpreadsheetWriteResult>.Ok(new TakeoffSpreadsheetWriteResult
            {
                OutputPath = outputPath,
                ByteCount = byteCount,
                Warnings = warnings
            }, "CSV take-off export written.");
        }
        catch (Exception ex)
        {
            TryDelete(tempPath);
            return OperationResponse<TakeoffSpreadsheetWriteResult>.Fail($"TAKEOFF_CSV_WRITE_FAILED: {ex.Message}");
        }
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\r') || value.Contains('\n'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }

    internal static string SanitizeCell(TakeoffCellValue? cell, List<ObjectEditWarning> warnings)
    {
        if (cell is null)
        {
            return string.Empty;
        }

        if (cell.Number.HasValue)
        {
            return cell.Text;
        }

        string text = cell.Text ?? string.Empty;
        if (text.Length > 0 && "=+-@".IndexOf(text[0], StringComparison.Ordinal) >= 0)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "TAKEOFF_FORMULA_TEXT_ESCAPED",
                Message = "A text cell beginning with a spreadsheet formula trigger was prefixed with an apostrophe."
            });
            return "'" + text;
        }

        return text;
    }

    internal static string BuildTempPath(string outputPath)
    {
        string directory = Path.GetDirectoryName(outputPath) ?? Directory.GetCurrentDirectory();
        string fileName = Path.GetFileName(outputPath);
        return Path.Combine(directory, $".{fileName}.{Guid.NewGuid():N}.tmp");
    }

    internal static void ReplaceOutput(string tempPath, string outputPath, bool overwriteExisting)
    {
        if (System.IO.File.Exists(outputPath))
        {
            if (!overwriteExisting)
            {
                throw new IOException("Output exists and overwriteExisting is false.");
            }

            System.IO.File.Delete(outputPath);
        }

        System.IO.File.Move(tempPath, outputPath);
    }

    internal static void TryDelete(string path)
    {
        try
        {
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup of temporary writer output.
        }
    }
}
