using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Infrastructure.Plugin;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string ReferenceImageObjectModelingAgentSlug = "reference-image-object-modeling-agent-smoke-test";

    partial void RegisterReferenceImageObjectModelingAgentHandlers()
    {
        _extensionHandlers[ReferenceImageObjectModelingAgentSlug] = HandleReferenceImageObjectModelingAgentSmokeTest;
    }

    private bool HandleReferenceImageObjectModelingAgentSmokeTest(string[] args)
    {
        try
        {
            if (McpRhinoPlugin.Instance is null)
            {
                RunReferenceImageObjectModelingAgentCliFallbackSmoke();
            }
            else
            {
                string filePath = args.Length > 1 ? args[1] : string.Empty;
                RunReferenceImageObjectModelingAgentLiveSmoke(filePath);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Reference image object modeling agent smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunReferenceImageObjectModelingAgentCliFallbackSmoke()
    {
        OperationResponse<ReferenceImageObjectModelingAgentResponse> rawImageOnly =
            _referenceImageObjectModelingAgent.Run(new ReferenceImageObjectModelingAgentRequest
            {
                FilePath = "C:/mcp-rhino/reference-image-object-modeling-agent-smoke.3dm",
                BriefRequest = new BuildReferenceImageModelBriefRequest
                {
                    ReferenceImagePath = "C:/reference/object.png"
                }
            });
        if (rawImageOnly.Success)
        {
            throw new InvalidOperationException("Raw image-only agent request should fail validation because no image inference connector is implemented.");
        }

        OperationResponse<ReferenceImageObjectModelingAgentResponse> response =
            _referenceImageObjectModelingAgent.Run(new ReferenceImageObjectModelingAgentRequest
            {
                FilePath = "C:/mcp-rhino/reference-image-object-modeling-agent-smoke.3dm",
                BriefRequest = ReferenceImageAgentSofaBrief("agent-cli-smoke"),
                TargetLayerFullPath = "MCP::REFERENCE_IMAGE_OBJECT_MODELING_AGENT_SMOKE",
                Scale = 2d,
                RunVisualQa = true,
                QaImageSizePx = new ImageSizePxRequest { Width = 64, Height = 64 }
            });

        ReferenceImageObjectModelingAgentResponse data = RequireReferenceImageAgentSuccess(response, "Run agent CLI fallback");
        if (data.Status != ReferenceImageObjectModelingStatus.CapabilityGap)
        {
            throw new InvalidOperationException($"Expected CLI fallback status CapabilityGap, got {data.Status}.");
        }

        if (!data.CapabilityGaps.Any(gap => gap.Layer == ReferenceImageCapabilityGapLayer.LiveRhino
                && gap.Code == "INITIAL_MASSING_FAILED"
                && gap.Message.Contains("LIVE_RHINO_REQUIRED", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Expected live Rhino gap from initial massing in CLI fallback mode.");
        }

        if (data.Plan.Brief.ObjectType != "sofa" || data.Plan.Decomposition.Parts.Count < 2)
        {
            throw new InvalidOperationException("Agent did not produce brief/decomposition before live gap reporting.");
        }

        Console.WriteLine("[OK] reference-image-object-modeling-agent CLI fallback produced structured plan then live-Rhino gap.");
        Console.WriteLine("[OK] raw image-only request failed validation until a multimodal image connector exists.");
    }

    private void RunReferenceImageObjectModelingAgentLiveSmoke(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("Live reference image object modeling agent smoke requires a saved active document path.");
        }

        string suffix = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        ReferenceImageObjectModelingAgentResponse result = RequireReferenceImageAgentSuccess(
            _referenceImageObjectModelingAgent.Run(new ReferenceImageObjectModelingAgentRequest
            {
                FilePath = filePath,
                BriefRequest = ReferenceImageAgentSofaBrief($"agent-live-smoke-{suffix}"),
                TargetLayerFullPath = $"MCP::REFERENCE_IMAGE_OBJECT_MODELING_AGENT_SMOKE_{suffix}",
                Scale = 2d,
                RunVisualQa = true,
                ApplyMaterials = true,
                ExecuteSimpleRaisedStripDetails = true,
                QaImageSizePx = new ImageSizePxRequest { Width = 240, Height = 160 }
            }),
            "Run live agent");

        if (result.InitialMassing is null || result.InitialMassing.CreatedObjectCount < 2)
        {
            throw new InvalidOperationException("Expected live agent to create at least two massing objects.");
        }

        if (!result.QaCheckpoints.Any(item => item.CheckpointKind == ReferenceImageVisualQaCheckpointKind.InitialMassing))
        {
            throw new InvalidOperationException("Expected initial massing visual QA checkpoint.");
        }

        if (result.MaterialAssignment is null || result.MaterialAssignment.AssignedCount == 0)
        {
            throw new InvalidOperationException("Expected live agent to assign at least one material.");
        }

        if (result.Status is ReferenceImageObjectModelingStatus.CapabilityGap or ReferenceImageObjectModelingStatus.Failed)
        {
            throw new InvalidOperationException($"Expected live agent completion or partial completion, got {result.Status}: {result.StatusReason}");
        }

        Console.WriteLine("[OK] reference-image-object-modeling-agent live smoke completed.");
        Console.WriteLine($"[OK] status={result.Status}; massingObjects={result.InitialMassing.CreatedObjectCount}; detailObjects={result.DetailObjects.Count}; qaCheckpoints={result.QaCheckpoints.Count}; materialAssignments={result.MaterialAssignment.AssignedCount}.");
    }

    private static BuildReferenceImageModelBriefRequest ReferenceImageAgentSofaBrief(string label)
    {
        return new BuildReferenceImageModelBriefRequest
        {
            ReferenceImageLabel = label,
            ObjectType = "sofa",
            ObjectTypeConfidence = 0.9d,
            PrimaryTargetObject = "sofa",
            WidthRatio = 3,
            DepthRatio = 1.1,
            HeightRatio = 1,
            OverallEdgeCharacter = ReferenceImageObjectEdgeCharacter.Soft,
            Parts = new List<ReferenceImageBriefPartRequest>
            {
                new() { Name = "seat cushion", Role = ReferenceImagePartRole.PrimaryMass, RelativeWidth = 3, RelativeDepth = 1, RelativeHeight = 0.35, RelativeCenterZ = 0.4, MaterialKey = "fabric" },
                new() { Name = "back cushion", Role = ReferenceImagePartRole.StructuralMass, RelativeWidth = 3, RelativeDepth = 0.25, RelativeHeight = 0.9, RelativeCenterY = 0.55, RelativeCenterZ = 0.95, MaterialKey = "fabric" },
                new() { Name = "bolster roll", Role = ReferenceImagePartRole.StructuralMass, RelativeWidth = 3, RelativeDepth = 0.22, RelativeHeight = 0.22, RelativeCenterY = -0.55, RelativeCenterZ = 0.75, Notes = new List<string> { "long soft roll" }, MaterialKey = "fabric" }
            },
            DetailCues = new List<ReferenceImageVisibleDetailCueRequest>
            {
                new() { PartName = "seat cushion", Kind = "seam", Description = "front horizontal raised seam", Role = ReferenceImagePartRole.AdditiveDetail }
            },
            MaterialCues = new List<ReferenceImageMaterialCueRequest>
            {
                new()
                {
                    Key = "fabric",
                    PartName = "seat cushion",
                    Description = "warm brown woven fabric",
                    BaseColor = new ObjectColorRequest { R = 142, G = 92, B = 73 },
                    Intent = ReferenceImageMaterialIntent.Fabric,
                    Roughness = 0.9,
                    TextureLikely = true
                }
            }
        };
    }

    private static ReferenceImageObjectModelingAgentResponse RequireReferenceImageAgentSuccess(
        OperationResponse<ReferenceImageObjectModelingAgentResponse> response,
        string label)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{label} failed: {response.Message}");
        }

        return response.Data;
    }
}
