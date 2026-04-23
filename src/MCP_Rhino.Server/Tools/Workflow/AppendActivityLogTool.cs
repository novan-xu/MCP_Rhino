using System.ComponentModel;
using System.Text.Json;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Workflow;

[McpServerToolType]
public sealed class AppendActivityLogTool
{
    [McpServerTool]
    [Description("Append a single JSONL activity record ({ts, task, tools}) to {logRoot}/YYMM.json. Per MCP_Rhino Workflow this is the unified writer for activity logs; business tools must not write logs themselves. The YYMM file is chosen from the current local date; the directory is created if missing.")]
    public OperationResponse<AppendActivityLogResponse> AppendActivityLog(
        string logRoot,
        string task,
        List<string> tools)
    {
        if (string.IsNullOrWhiteSpace(logRoot))
        {
            return OperationResponse<AppendActivityLogResponse>.Fail("logRoot is required.");
        }
        if (string.IsNullOrWhiteSpace(task))
        {
            return OperationResponse<AppendActivityLogResponse>.Fail("task is required.");
        }

        var now = DateTime.Now;
        var ts = now.ToString("yyyy-MM-ddTHH:mm");
        var fileName = now.ToString("yyMM") + ".json";
        var fullPath = Path.Combine(logRoot, fileName);

        Directory.CreateDirectory(logRoot);

        var record = new Dictionary<string, object?>
        {
            ["ts"] = ts,
            ["task"] = task,
            ["tools"] = tools ?? new List<string>()
        };
        var json = JsonSerializer.Serialize(record);
        System.IO.File.AppendAllText(fullPath, json + "\n");

        return OperationResponse<AppendActivityLogResponse>.Ok(
            new AppendActivityLogResponse
            {
                FilePath = fullPath,
                Timestamp = ts
            },
            "Activity log appended.");
    }
}

public sealed class AppendActivityLogResponse
{
    public string FilePath { get; init; } = string.Empty;
    public string Timestamp { get; init; } = string.Empty;
}
