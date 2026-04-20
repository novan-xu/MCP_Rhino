using System.Text;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Infrastructure.Rhino;

public sealed class PassThroughEditResultFormatter : IEditResultFormatter
{
    public string FormatPreview(ObjectEditPreviewResponse response)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Rhino Object Edit Preview");
        builder.AppendLine($"- 文件: {response.FilePath}");
        builder.AppendLine($"- 匹配对象数: {response.MatchedObjectCount}");
        builder.AppendLine($"- 预览对象数: {response.PreviewObjectCount}");
        builder.AppendLine($"- 操作数: {response.OperationCount}");
        builder.AppendLine($"- 条件摘要: {response.CriteriaSummary}");
        AppendWarnings(builder, response.Warnings);
        AppendResults(builder, response.ObjectResults);
        return builder.ToString();
    }

    public string FormatExecution(ObjectEditExecutionResponse response)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Rhino Object Edit Execution");
        builder.AppendLine($"- 文件: {response.FilePath}");
        builder.AppendLine($"- 匹配对象数: {response.MatchedObjectCount}");
        builder.AppendLine($"- 成功修改数: {response.UpdatedObjectCount}");
        builder.AppendLine($"- 失败数: {response.FailedObjectCount}");
        builder.AppendLine($"- 操作数: {response.OperationCount}");
        builder.AppendLine($"- 条件摘要: {response.CriteriaSummary}");
        AppendWarnings(builder, response.Warnings);
        AppendResults(builder, response.ObjectResults);
        return builder.ToString();
    }

    public string FormatGeometryCreation(GeometryCreationResponse response)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Rhino Geometry Creation");
        builder.AppendLine($"- 文件: {response.FilePath}");
        builder.AppendLine($"- 请求数量: {response.RequestedCount}");
        builder.AppendLine($"- 创建数量: {response.CreatedCount}");
        AppendWarnings(builder, response.Warnings);

        if (response.CreatedObjects.Count == 0)
        {
            builder.AppendLine();
            builder.AppendLine("没有创建对象可展示。");
            return builder.ToString();
        }

        builder.AppendLine();
        builder.AppendLine("创建结果:");
        foreach (GeometryCreatedObjectResponse createdObject in response.CreatedObjects)
        {
            builder.AppendLine($"- {createdObject.ObjectId} | Primitive={createdObject.Primitive} | Layer={createdObject.LayerFullPath}");
        }

        return builder.ToString();
    }

    public string FormatGeometryModification(GeometryModificationResponse response)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Rhino Geometry Modification");
        builder.AppendLine($"- 文件: {response.FilePath}");
        builder.AppendLine($"- 匹配对象数: {response.MatchedObjectCount}");
        builder.AppendLine($"- 成功修改数: {response.UpdatedObjectCount}");
        builder.AppendLine($"- 失败数: {response.FailedObjectCount}");
        builder.AppendLine($"- 操作数: {response.OperationCount}");
        builder.AppendLine($"- 条件摘要: {response.CriteriaSummary}");
        AppendWarnings(builder, response.Warnings);
        AppendResults(builder, response.ObjectResults);
        return builder.ToString();
    }

    public string FormatGeometryModificationPreview(GeometryModificationPreviewResponse response)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Rhino Geometry Modification Preview");
        builder.AppendLine($"- 文件: {response.FilePath}");
        builder.AppendLine($"- 匹配对象数: {response.MatchedObjectCount}");
        builder.AppendLine($"- 预览对象数: {response.PreviewObjectCount}");
        builder.AppendLine($"- 操作数: {response.OperationCount}");
        builder.AppendLine($"- 条件摘要: {response.CriteriaSummary}");
        AppendWarnings(builder, response.Warnings);
        AppendResults(builder, response.ObjectResults);
        return builder.ToString();
    }

    private static void AppendWarnings(StringBuilder builder, IReadOnlyList<ObjectEditWarning> warnings)
    {
        if (warnings.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("Warnings:");
        foreach (ObjectEditWarning warning in warnings)
        {
            builder.AppendLine($"- [{warning.Code}] {warning.Message}");
        }
    }

    private static void AppendResults(StringBuilder builder, IReadOnlyList<ObjectEditOperationResult> results)
    {
        if (results.Count == 0)
        {
            builder.AppendLine();
            builder.AppendLine("没有对象结果可展示。");
            return;
        }

        builder.AppendLine();
        builder.AppendLine("对象结果预览:");
        foreach (ObjectEditOperationResult result in results)
        {
            builder.AppendLine($"- {result.ObjectId} | Success={result.Success} | Layer={result.LayerFullPath}");
            foreach (string message in result.Messages)
            {
                builder.AppendLine($"  - {message}");
            }
        }
    }
}
