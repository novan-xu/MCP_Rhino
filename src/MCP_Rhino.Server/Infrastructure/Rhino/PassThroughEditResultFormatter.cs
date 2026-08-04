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
        builder.AppendLine($"- File: {response.FilePath}");
        builder.AppendLine($"- Matched objects: {response.MatchedObjectCount}");
        builder.AppendLine($"- Previewed objects: {response.PreviewObjectCount}");
        builder.AppendLine($"- Operation count: {response.OperationCount}");
        builder.AppendLine($"- Criteria summary: {response.CriteriaSummary}");
        AppendWarnings(builder, response.Warnings);
        AppendResults(builder, response.ObjectResults);
        return builder.ToString();
    }

    public string FormatExecution(ObjectEditExecutionResponse response)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Rhino Object Edit Execution");
        builder.AppendLine($"- File: {response.FilePath}");
        builder.AppendLine($"- Matched objects: {response.MatchedObjectCount}");
        builder.AppendLine($"- Successful edits: {response.UpdatedObjectCount}");
        builder.AppendLine($"- Failed edits: {response.FailedObjectCount}");
        builder.AppendLine($"- Operation count: {response.OperationCount}");
        builder.AppendLine($"- Criteria summary: {response.CriteriaSummary}");
        AppendWarnings(builder, response.Warnings);
        AppendResults(builder, response.ObjectResults);
        return builder.ToString();
    }

    public string FormatGeometryCreation(GeometryCreationResponse response)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Rhino Geometry Creation");
        builder.AppendLine($"- File: {response.FilePath}");
        builder.AppendLine($"- Requested count: {response.RequestedCount}");
        builder.AppendLine($"- Created count: {response.CreatedCount}");
        AppendWarnings(builder, response.Warnings);

        if (response.CreatedObjects.Count == 0)
        {
            builder.AppendLine();
            builder.AppendLine("No created objects to display.");
            return builder.ToString();
        }

        builder.AppendLine();
        builder.AppendLine("Created objects:");
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
        builder.AppendLine($"- File: {response.FilePath}");
        builder.AppendLine($"- Matched objects: {response.MatchedObjectCount}");
        builder.AppendLine($"- Successful edits: {response.UpdatedObjectCount}");
        builder.AppendLine($"- Failed edits: {response.FailedObjectCount}");
        builder.AppendLine($"- Operation count: {response.OperationCount}");
        builder.AppendLine($"- Criteria summary: {response.CriteriaSummary}");
        AppendWarnings(builder, response.Warnings);
        AppendResults(builder, response.ObjectResults);
        return builder.ToString();
    }

    public string FormatGeometryModificationPreview(GeometryModificationPreviewResponse response)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Rhino Geometry Modification Preview");
        builder.AppendLine($"- File: {response.FilePath}");
        builder.AppendLine($"- Matched objects: {response.MatchedObjectCount}");
        builder.AppendLine($"- Previewed objects: {response.PreviewObjectCount}");
        builder.AppendLine($"- Operation count: {response.OperationCount}");
        builder.AppendLine($"- Criteria summary: {response.CriteriaSummary}");
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
            builder.AppendLine("No object results to display.");
            return;
        }

        builder.AppendLine();
        builder.AppendLine("Object result preview:");
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
