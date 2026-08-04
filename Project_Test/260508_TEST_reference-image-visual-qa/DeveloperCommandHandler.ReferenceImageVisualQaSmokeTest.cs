using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Infrastructure.Plugin;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string ReferenceImageVisualQaSlug = "reference-image-visual-qa-smoke-test";

    partial void RegisterReferenceImageVisualQaHandlers()
    {
        _extensionHandlers[ReferenceImageVisualQaSlug] = HandleReferenceImageVisualQaSmokeTest;
    }

    private bool HandleReferenceImageVisualQaSmokeTest(string[] args)
    {
        try
        {
            if (McpRhinoPlugin.Instance is null)
            {
                RunReferenceImageVisualQaCliFallbackSmoke();
            }
            else
            {
                string filePath = args.Length > 1 ? args[1] : string.Empty;
                RunReferenceImageVisualQaLiveSmoke(filePath);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Reference image visual QA smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunReferenceImageVisualQaCliFallbackSmoke()
    {
        OperationResponse<ReferenceImageVisualQaCaptureResponse> response =
            _referenceImageVisualQaCaptureService.Capture(new CaptureReferenceImageModelingQaViewsRequest
            {
                FilePath = "C:/mcp-rhino/reference-image-visual-qa-smoke.3dm",
                CheckpointKind = ReferenceImageVisualQaCheckpointKind.InitialMassing,
                ImageSizePx = new ImageSizePxRequest { Width = 64, Height = 64 }
            });

        if (response.Success || !response.Message.Contains("LIVE_RHINO_REQUIRED", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Reference image visual QA should return LIVE_RHINO_REQUIRED in CLI fallback mode; got Success={response.Success}, Message={response.Message}");
        }

        Console.WriteLine("[OK] reference-image-visual-qa CLI fallback returned LIVE_RHINO_REQUIRED for live-only capture.");
    }

    private void RunReferenceImageVisualQaLiveSmoke(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("Live reference image visual QA smoke requires a saved active document path.");
        }

        string suffix = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        string smokeLayer = $"MCP::REFERENCE_IMAGE_VISUAL_QA_SMOKE_{suffix}";

        RequireReferenceImageVisualQaSuccess(_layerManagementService.Create(new CreateLayersRequest
        {
            FilePath = filePath,
            Entries = new List<LayerCreationEntryRequest>
            {
                new() { FullPath = smokeLayer }
            }
        }), "Create visual QA smoke layer");

        GeometryCreationResponse created = RequireReferenceImageVisualQaSuccess(_geometryCreationSkill.Create(new CreatePointsRequest
        {
            FilePath = filePath,
            Items = new List<PointItemRequest>
            {
                new() { X = 0, Y = 0, Z = 0 },
                new() { X = 2, Y = 0, Z = 0 },
                new() { X = 1, Y = 1, Z = 0 }
            },
            Common = new GeometryCreationCommonOptions
            {
                LayerFullPath = smokeLayer,
                Name = "reference image visual QA smoke point",
                UserText = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["mcp.capability"] = "reference-image-visual-qa",
                    ["mcp.modeling.stage"] = "massing"
                }
            }
        }), "Create visual QA smoke target points");

        List<Guid> createdObjectIds = created.CreatedObjects.Select(item => item.ObjectId).ToList();
        if (createdObjectIds.Count != 3)
        {
            throw new InvalidOperationException($"Expected 3 created target objects, got {createdObjectIds.Count}.");
        }

        ReferenceImageVisualQaCaptureResponse qa = RequireReferenceImageVisualQaSuccess(
            _referenceImageVisualQaCaptureService.Capture(new CaptureReferenceImageModelingQaViewsRequest
            {
                FilePath = filePath,
                ReferenceImageLabel = "smoke-reference",
                CheckpointKind = ReferenceImageVisualQaCheckpointKind.InitialMassing,
                ObjectIds = createdObjectIds,
                ImageSizePx = new ImageSizePxRequest { Width = 240, Height = 160 },
                MaxObjectSummaries = 5
            }),
            "Capture reference image visual QA views");

        if (qa.TargetObjectCount != 3)
        {
            throw new InvalidOperationException($"Expected target object count 3, got {qa.TargetObjectCount}.");
        }

        if (qa.Captures.Count != 1)
        {
            throw new InvalidOperationException($"Expected one QA capture, got {qa.Captures.Count}.");
        }

        ReferenceImageVisualQaViewCaptureResponse capture = qa.Captures[0];
        if (capture.Width != 240 || capture.Height != 160 || capture.ByteCount <= 0 || string.IsNullOrWhiteSpace(capture.DataBase64))
        {
            throw new InvalidOperationException("QA capture did not return a non-empty 240x160 base64 PNG payload.");
        }

        if (!qa.Checklist.Any(item => item.Contains("silhouette", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Initial massing QA checklist did not include silhouette guidance.");
        }

        Console.WriteLine("[OK] reference-image-visual-qa live smoke completed.");
        Console.WriteLine($"[OK] targetObjects={qa.TargetObjectCount}; captureBytes={capture.ByteCount}; checklistItems={qa.Checklist.Count}.");
    }

    private static T RequireReferenceImageVisualQaSuccess<T>(OperationResponse<T> response, string label)
        where T : class
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{label} failed: {response.Message}");
        }

        return response.Data;
    }
}
