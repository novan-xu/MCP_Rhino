using System.IO.Compression;
using System.Xml;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Infrastructure.Spreadsheet;

public sealed class XlsxSpreadsheetWorkbookWriter : ISpreadsheetWorkbookWriter
{
    public bool Supports(TakeoffSpreadsheetFormat format)
    {
        return format == TakeoffSpreadsheetFormat.Xlsx;
    }

    public OperationResponse<TakeoffSpreadsheetWriteResult> Write(
        TakeoffWorkbook workbook,
        TakeoffSpreadsheetFormat format,
        string outputPath,
        bool overwriteExisting)
    {
        if (!Supports(format))
        {
            return OperationResponse<TakeoffSpreadsheetWriteResult>.Fail($"XLSX writer does not support format {format}.");
        }

        if (workbook.Worksheets.Count == 0)
        {
            return OperationResponse<TakeoffSpreadsheetWriteResult>.Fail("TAKEOFF_XLSX_REQUIRES_SHEET: XLSX export needs at least one worksheet.");
        }

        if (System.IO.File.Exists(outputPath) && !overwriteExisting)
        {
            return OperationResponse<TakeoffSpreadsheetWriteResult>.Fail(
                "TAKEOFF_OUTPUT_OVERWRITE_BLOCKED: output file exists and overwriteExisting is false.");
        }

        var warnings = new List<ObjectEditWarning>();
        string tempPath = CsvSpreadsheetWorkbookWriter.BuildTempPath(outputPath);
        try
        {
            using (FileStream stream = System.IO.File.Open(tempPath, FileMode.CreateNew, FileAccess.ReadWrite))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteContentTypes(archive, workbook.Worksheets.Count);
                WriteRootRelationships(archive);
                WriteWorkbook(archive, workbook.Worksheets);
                WriteWorkbookRelationships(archive, workbook.Worksheets.Count);
                for (int i = 0; i < workbook.Worksheets.Count; i++)
                {
                    WriteWorksheet(archive, i + 1, workbook.Worksheets[i], warnings);
                }
            }

            CsvSpreadsheetWorkbookWriter.ReplaceOutput(tempPath, outputPath, overwriteExisting);
            long byteCount = new FileInfo(outputPath).Length;
            if (byteCount <= 0L)
            {
                return OperationResponse<TakeoffSpreadsheetWriteResult>.Fail("TAKEOFF_OUTPUT_EMPTY: XLSX writer produced an empty file.");
            }

            return OperationResponse<TakeoffSpreadsheetWriteResult>.Ok(new TakeoffSpreadsheetWriteResult
            {
                OutputPath = outputPath,
                ByteCount = byteCount,
                Warnings = warnings
            }, "XLSX take-off export written.");
        }
        catch (Exception ex)
        {
            CsvSpreadsheetWorkbookWriter.TryDelete(tempPath);
            return OperationResponse<TakeoffSpreadsheetWriteResult>.Fail($"TAKEOFF_XLSX_WRITE_FAILED: {ex.Message}");
        }
    }

    private static void WriteContentTypes(ZipArchive archive, int sheetCount)
    {
        using XmlWriter writer = CreateEntryWriter(archive, "[Content_Types].xml");
        writer.WriteStartDocument();
        writer.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
        writer.WriteStartElement("Default");
        writer.WriteAttributeString("Extension", "rels");
        writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-package.relationships+xml");
        writer.WriteEndElement();
        writer.WriteStartElement("Default");
        writer.WriteAttributeString("Extension", "xml");
        writer.WriteAttributeString("ContentType", "application/xml");
        writer.WriteEndElement();
        writer.WriteStartElement("Override");
        writer.WriteAttributeString("PartName", "/xl/workbook.xml");
        writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
        writer.WriteEndElement();
        for (int i = 1; i <= sheetCount; i++)
        {
            writer.WriteStartElement("Override");
            writer.WriteAttributeString("PartName", $"/xl/worksheets/sheet{i}.xml");
            writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteRootRelationships(ZipArchive archive)
    {
        using XmlWriter writer = CreateEntryWriter(archive, "_rels/.rels");
        writer.WriteStartDocument();
        writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
        writer.WriteStartElement("Relationship");
        writer.WriteAttributeString("Id", "rId1");
        writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument");
        writer.WriteAttributeString("Target", "xl/workbook.xml");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteWorkbook(ZipArchive archive, IReadOnlyList<TakeoffWorksheet> worksheets)
    {
        using XmlWriter writer = CreateEntryWriter(archive, "xl/workbook.xml");
        writer.WriteStartDocument();
        writer.WriteStartElement("workbook", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        writer.WriteAttributeString("xmlns", "r", null, "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
        writer.WriteStartElement("sheets");
        for (int i = 0; i < worksheets.Count; i++)
        {
            writer.WriteStartElement("sheet");
            writer.WriteAttributeString("name", NormalizeSheetName(worksheets[i].Name, i));
            writer.WriteAttributeString("sheetId", (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteAttributeString("r", "id", null, $"rId{i + 1}");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteWorkbookRelationships(ZipArchive archive, int sheetCount)
    {
        using XmlWriter writer = CreateEntryWriter(archive, "xl/_rels/workbook.xml.rels");
        writer.WriteStartDocument();
        writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
        for (int i = 1; i <= sheetCount; i++)
        {
            writer.WriteStartElement("Relationship");
            writer.WriteAttributeString("Id", $"rId{i}");
            writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet");
            writer.WriteAttributeString("Target", $"worksheets/sheet{i}.xml");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteWorksheet(
        ZipArchive archive,
        int sheetNumber,
        TakeoffWorksheet worksheet,
        List<ObjectEditWarning> warnings)
    {
        using XmlWriter writer = CreateEntryWriter(archive, $"xl/worksheets/sheet{sheetNumber}.xml");
        writer.WriteStartDocument();
        writer.WriteStartElement("worksheet", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        writer.WriteStartElement("sheetData");
        WriteRow(writer, 1, worksheet.Columns.Select(TakeoffCellValue.FromString).ToList(), warnings);
        for (int i = 0; i < worksheet.Rows.Count; i++)
        {
            List<TakeoffCellValue> values = worksheet.Columns.Select(column =>
            {
                worksheet.Rows[i].Cells.TryGetValue(column, out TakeoffCellValue? value);
                return value ?? TakeoffCellValue.FromString(string.Empty);
            }).ToList();
            WriteRow(writer, i + 2, values, warnings);
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteRow(
        XmlWriter writer,
        int rowNumber,
        IReadOnlyList<TakeoffCellValue> values,
        List<ObjectEditWarning> warnings)
    {
        writer.WriteStartElement("row");
        writer.WriteAttributeString("r", rowNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));
        for (int i = 0; i < values.Count; i++)
        {
            string cellReference = GetCellReference(i + 1, rowNumber);
            TakeoffCellValue value = values[i];
            writer.WriteStartElement("c");
            writer.WriteAttributeString("r", cellReference);
            if (value.Number.HasValue)
            {
                writer.WriteElementString("v", value.Number.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            else
            {
                writer.WriteAttributeString("t", "inlineStr");
                writer.WriteStartElement("is");
                writer.WriteStartElement("t");
                writer.WriteAttributeString("xml", "space", null, "preserve");
                writer.WriteString(CsvSpreadsheetWorkbookWriter.SanitizeCell(value, warnings));
                writer.WriteEndElement();
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static XmlWriter CreateEntryWriter(ZipArchive archive, string name)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        return XmlWriter.Create(entry.Open(), new XmlWriterSettings
        {
            Indent = false,
            CloseOutput = true
        });
    }

    private static string GetCellReference(int columnNumber, int rowNumber)
    {
        var letters = new Stack<char>();
        int current = columnNumber;
        while (current > 0)
        {
            current--;
            letters.Push((char)('A' + current % 26));
            current /= 26;
        }

        return new string(letters.ToArray()) + rowNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string NormalizeSheetName(string name, int index)
    {
        string candidate = string.IsNullOrWhiteSpace(name) ? $"Sheet{index + 1}" : name;
        char[] invalid = { ':', '\\', '/', '?', '*', '[', ']' };
        foreach (char invalidChar in invalid)
        {
            candidate = candidate.Replace(invalidChar, '_');
        }

        return candidate.Length > 31 ? candidate[..31] : candidate;
    }
}
