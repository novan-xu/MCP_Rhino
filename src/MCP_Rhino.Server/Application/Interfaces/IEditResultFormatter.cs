using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface IEditResultFormatter
{
    string FormatPreview(ObjectEditPreviewResponse response);
    string FormatExecution(ObjectEditExecutionResponse response);
}