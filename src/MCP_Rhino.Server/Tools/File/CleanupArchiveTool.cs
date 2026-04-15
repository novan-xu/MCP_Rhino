using System.ComponentModel;
using ModelContextProtocol.Server;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.File;

namespace MCP_Rhino.Server.Tools.File;

[McpServerToolType]
public sealed class CleanupArchiveTool
{
    private readonly ArchiveRetentionSkill _archiveRetentionSkill;

    public CleanupArchiveTool(ArchiveRetentionSkill archiveRetentionSkill)
    {
        _archiveRetentionSkill = archiveRetentionSkill;
    }

    [McpServerTool]
    [Description("清理 archive 目录：保留当天所有备份，非当天每一天仅保留最后修改的一份。")]
    public OperationResponse<ArchiveCleanupResponse> CleanupArchive(string filePath)
    {
        return _archiveRetentionSkill.Cleanup(filePath);
    }
}