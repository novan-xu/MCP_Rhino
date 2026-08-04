extern alias rhinocommon;

using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILiveExternalReferenceManager
{
    OperationResponse<IReadOnlyList<WorksessionAttachmentResult>> ListWorksession(RhinoDoc document);

    OperationResponse<IReadOnlyList<LinkedBlockResult>> UpdateLinkedBlock(RhinoDoc document, IReadOnlyList<string> definitionNames);
}
