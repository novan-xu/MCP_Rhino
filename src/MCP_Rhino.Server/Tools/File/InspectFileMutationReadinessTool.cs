using System.ComponentModel;
using ModelContextProtocol.Server;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.File;

namespace MCP_Rhino.Server.Tools.File;

[McpServerToolType]
public sealed class InspectFileMutationReadinessTool
{
    private readonly FileOpenStateCheckSkill _fileOpenStateCheckSkill;

    public InspectFileMutationReadinessTool(FileOpenStateCheckSkill fileOpenStateCheckSkill)
    {
        _fileOpenStateCheckSkill = fileOpenStateCheckSkill;
    }

    [McpServerTool]
    [Description("检查 Rhino 文件是否处于可安全修改状态，包括占用情况与 .rhl 锁文件痕迹。")]
    public OperationResponse<FileMutationReadinessResponse> InspectFileMutationReadiness(string filePath)
    {
        return _fileOpenStateCheckSkill.Check(filePath);
    }
}