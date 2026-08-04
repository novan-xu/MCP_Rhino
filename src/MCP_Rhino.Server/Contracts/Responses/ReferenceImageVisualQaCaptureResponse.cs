using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class ReferenceImageVisualQaCaptureResponse
{
    public string FilePath { get; set; } = string.Empty;
    public string ReferenceImagePath { get; set; } = string.Empty;
    public string ReferenceImageLabel { get; set; } = string.Empty;
    public ReferenceImageVisualQaCheckpointKind CheckpointKind { get; set; }
    public int TargetObjectCount { get; set; }
    public string TargetCriteriaSummary { get; set; } = string.Empty;
    public IReadOnlyList<RhinoObjectInfo> ObjectSummaries { get; set; } = Array.Empty<RhinoObjectInfo>();
    public IReadOnlyList<ReferenceImageVisualQaViewCaptureResponse> Captures { get; set; } = Array.Empty<ReferenceImageVisualQaViewCaptureResponse>();
    public IReadOnlyList<string> Checklist { get; set; } = Array.Empty<string>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class ReferenceImageVisualQaViewCaptureResponse
{
    public string RequestedViewName { get; set; } = string.Empty;
    public string CapturedViewName { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public string ContentType { get; set; } = "image/png";
    public string DataBase64 { get; set; } = string.Empty;
    public int ByteCount { get; set; }
}
