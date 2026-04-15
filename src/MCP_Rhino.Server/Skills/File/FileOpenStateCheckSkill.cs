using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Skills.File;

public sealed class FileOpenStateCheckSkill
{
    private readonly IFileOpenStateInspector _fileOpenStateInspector;

    public FileOpenStateCheckSkill(IFileOpenStateInspector fileOpenStateInspector)
    {
        _fileOpenStateInspector = fileOpenStateInspector;
    }

    public OperationResponse<FileMutationReadinessResponse> Check(string filePath)
    {
        return _fileOpenStateInspector.Inspect(filePath);
    }
}