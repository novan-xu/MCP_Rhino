using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ISpreadsheetWorkbookWriter
{
    bool Supports(TakeoffSpreadsheetFormat format);

    OperationResponse<TakeoffSpreadsheetWriteResult> Write(
        TakeoffWorkbook workbook,
        TakeoffSpreadsheetFormat format,
        string outputPath,
        bool overwriteExisting);
}
