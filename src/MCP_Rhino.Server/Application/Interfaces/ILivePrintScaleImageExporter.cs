extern alias rhinocommon;

using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILivePrintScaleImageExporter
{
    OperationResponse<PrintScaleImageExportExecutionResult> Export(RhinoDoc document, PrintScaleImageExportSpec spec);
}
