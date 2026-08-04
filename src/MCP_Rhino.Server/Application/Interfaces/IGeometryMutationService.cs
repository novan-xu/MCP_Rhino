extern alias rhinocommon;

using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Application.Models;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface IGeometryMutationService
{
    OperationResponse<IReadOnlyList<ObjectEditWarning>> ReplaceWithMetadata(
        string filePath,
        Guid objectId,
        GeometryBase geometry,
        string undoRecordName);

    OperationResponse<IReadOnlyDictionary<Guid, IReadOnlyList<ObjectEditWarning>>> ReplaceManyWithMetadata(
        string filePath,
        IReadOnlyList<GeometryReplacementWithMetadata> replacements,
        string undoRecordName);
}
