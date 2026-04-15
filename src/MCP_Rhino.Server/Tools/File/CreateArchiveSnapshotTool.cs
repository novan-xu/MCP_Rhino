using System.ComponentModel;
using ModelContextProtocol.Server;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.File;

namespace MCP_Rhino.Server.Tools.File;

[McpServerToolType]
public sealed class CreateArchiveSnapshotTool
{
    private readonly ArchiveSnapshotSkill _archiveSnapshotSkill;

    public CreateArchiveSnapshotTool(ArchiveSnapshotSkill archiveSnapshotSkill)
    {
        _archiveSnapshotSkill = archiveSnapshotSkill;
    }

    [McpServerTool]
    [Description("在文件目录下的 archive 文件夹中创建一个按 YYMMDD_HHmm_FileName 命名的备份。")]
    public OperationResponse<ArchiveSnapshotResponse> CreateArchiveSnapshot(string filePath)
    {
        return _archiveSnapshotSkill.Create(filePath);
    }
}