extern alias rhinocommon;

using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILiveFileExporter
{
    OperationResponse<FileExportExecutionResult> Export(RhinoDoc document, FileExportSpec spec);
}
