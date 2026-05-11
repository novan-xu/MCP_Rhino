using MCP_Rhino.Server.Agents.Modeling;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string MaterialTextureCapabilitySlug = "material-texture-capability-smoke-test";

    partial void RegisterMaterialTextureCapabilityHandlers()
    {
        _extensionHandlers[MaterialTextureCapabilitySlug] = HandleMaterialTextureCapabilitySmokeTest;
    }

    private bool HandleMaterialTextureCapabilitySmokeTest(string[] args)
    {
        try
        {
            RunMaterialTextureCapabilitySmoke();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Material texture capability smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunMaterialTextureCapabilitySmoke()
    {
        ReferenceImageModelBrief brief = RequireReferenceImageSkillSuccess(
            _referenceImageModelBriefSkill.Build(MaterialTextureCapabilityBrief()),
            "Build material texture policy brief").Brief;

        ReferenceImagePrimitiveDecomposition decomposition = RequireReferenceImageSkillSuccess(
            _referenceImagePrimitiveDecompositionSkill.Decompose(new DecomposeReferenceImagePrimitivesRequest
            {
                Brief = brief,
                Scale = 2d
            }),
            "Decompose material texture policy brief").Decomposition;

        ReferenceImageRefinementPlan refinement = RequireReferenceImageSkillSuccess(
            _referenceImageDetailRefinementSkill.Plan(new PlanReferenceImageDetailRefinementRequest
            {
                Brief = brief,
                Decomposition = decomposition,
                QaFindings = new List<string>
                {
                    "missing cast shadow under the cushion should stay lighting context"
                }
            }),
            "Plan material texture policy refinement").Plan;

        RequireSuppressedNonGeometryCue(refinement, "woven strip texture");
        RequireSuppressedNonGeometryCue(refinement, "cast shadow line");
        RequireSuppressedNonGeometryCue(refinement, "specular highlight");
        RequireNoGeometryAction(refinement, "woven strip texture");
        RequireNoGeometryAction(refinement, "cast shadow line");
        RequireNoGeometryAction(refinement, "specular highlight");

        if (!refinement.Actions.Any(action => action.Kind == ReferenceImageRefinementActionKind.AddRaisedStrip
                && action.Description.Contains("front horizontal raised seam", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Expected a real raised seam to remain a raised-strip geometry action.");
        }

        RequireAgentRaisedStripPolicy(
            new ReferenceImageRefinementAction
            {
                Kind = ReferenceImageRefinementActionKind.AddRaisedStrip,
                PartName = "seat cushion",
                Description = "woven strip texture across the cushion face"
            },
            expectedExecutable: false);
        RequireAgentRaisedStripPolicy(
            new ReferenceImageRefinementAction
            {
                Kind = ReferenceImageRefinementActionKind.AddRaisedStrip,
                PartName = "seat cushion",
                Description = "cast shadow line below the cushion"
            },
            expectedExecutable: false);
        RequireAgentRaisedStripPolicy(
            new ReferenceImageRefinementAction
            {
                Kind = ReferenceImageRefinementActionKind.AddRaisedStrip,
                PartName = "seat cushion",
                Description = "front horizontal raised seam"
            },
            expectedExecutable: true);

        string texturePath = Path.Combine(
            FindMaterialTextureCapabilityRepositoryRoot(),
            ".validation",
            "material-texture-smoke",
            "woven-smoke.png");
        OperationResponse<ProceduralTextureImageResponse> texture = new ProceduralTextureImageService().Generate(
            new ProceduralTextureImageSpec
            {
                PatternKind = ProceduralTexturePatternKind.WovenFabric,
                OutputPath = texturePath,
                WidthPx = 64,
                HeightPx = 64,
                BaseColor = new RhinoDisplayColor { R = 142, G = 92, B = 73 },
                WarpColor = new RhinoDisplayColor { R = 170, G = 125, B = 95 },
                WeftColor = new RhinoDisplayColor { R = 110, G = 72, B = 58 },
                ThreadSpacingPx = 8,
                ThreadThicknessPx = 3,
                NoiseAmount = 0.05d,
                Overwrite = true,
                Seed = 260509
            });
        if (!texture.Success || texture.Data is null || !File.Exists(texture.Data.OutputPath) || texture.Data.BytesWritten <= 0)
        {
            throw new InvalidOperationException($"Expected generated woven texture image; got {texture.Message}");
        }

        OperationResponse<TexturedRenderMaterialCreationResponse> texturedFallback = _rhinoMaterialService.CreateTextured(
            new CreateTexturedRenderMaterialsRequest
            {
                FilePath = "C:/mcp-rhino/material-texture-smoke.3dm",
                Items = new List<TexturedRenderMaterialItemRequest>
                {
                    new()
                    {
                        Name = "woven-smoke-material",
                        BaseColor = new ObjectColorRequest { R = 142, G = 92, B = 73 },
                        Roughness = 0.9d,
                        DiffuseTextureImagePath = texture.Data.OutputPath,
                        MappingChannel = 1
                    }
                }
            });
        if (texturedFallback.Success || !texturedFallback.Message.Contains("LIVE_RHINO_REQUIRED", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Textured material creation should reach the live-Rhino boundary in CLI fallback mode.");
        }

        OperationResponse<TextureMappingPreviewResponse> mappingFallback = _rhinoMaterialService.PreviewTextureMapping(
            new PreviewTextureMappingRequest
            {
                FilePath = "C:/mcp-rhino/material-texture-smoke.3dm",
                Items = new List<TextureMappingItemRequest>
                {
                    new()
                    {
                        ObjectId = Guid.NewGuid(),
                        MappingKind = TextureMappingKind.Box,
                        MappingChannel = 1,
                        Width = 1,
                        Depth = 1,
                        Height = 1
                    }
                }
            });
        if (mappingFallback.Success || !mappingFallback.Message.Contains("LIVE_RHINO_REQUIRED", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Texture mapping preview should reach the live-Rhino boundary in CLI fallback mode.");
        }

        Console.WriteLine("[OK] material texture capability smoke kept material, texture, lighting, and shadow cues out of geometry.");
        Console.WriteLine("[OK] ReferenceImageObjectModelingAgent raised-strip policy rejects non-geometric visual cues.");
        Console.WriteLine($"[OK] generated procedural woven texture image: {texture.Data.OutputPath} ({texture.Data.BytesWritten} bytes).");
        Console.WriteLine("[OK] textured material creation and texture mapping route to live Rhino after validation.");
    }

    private static void RequireSuppressedNonGeometryCue(
        ReferenceImageRefinementPlan refinement,
        string expectedText)
    {
        if (!refinement.SuppressedTextureLikeDetails.Any(item =>
                item.Contains(expectedText, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Expected suppressed non-geometry cue containing '{expectedText}'.");
        }
    }

    private static void RequireNoGeometryAction(
        ReferenceImageRefinementPlan refinement,
        string forbiddenText)
    {
        if (refinement.Actions.Any(action =>
                action.Description.Contains(forbiddenText, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Non-geometric visual cue became a geometry action: {forbiddenText}");
        }
    }

    private static void RequireAgentRaisedStripPolicy(
        ReferenceImageRefinementAction action,
        bool expectedExecutable)
    {
        bool actual = ReferenceImageObjectModelingAgent.ShouldExecuteAsSimpleRaisedStrip(action);
        if (actual != expectedExecutable)
        {
            throw new InvalidOperationException(
                $"Expected raised-strip executable={expectedExecutable} for '{action.Description}', got {actual}.");
        }
    }

    private static string FindMaterialTextureCapabilityRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MCP_Rhino.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MCP_Rhino.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }

    private static BuildReferenceImageModelBriefRequest MaterialTextureCapabilityBrief()
    {
        return new BuildReferenceImageModelBriefRequest
        {
            ReferenceImageLabel = "material-texture-policy-sofa",
            ObjectType = "sofa",
            ObjectTypeConfidence = 0.9d,
            PrimaryTargetObject = "sofa",
            WidthRatio = 3,
            DepthRatio = 1.1,
            HeightRatio = 1,
            OverallEdgeCharacter = ReferenceImageObjectEdgeCharacter.Soft,
            Parts = new List<ReferenceImageBriefPartRequest>
            {
                new()
                {
                    Name = "seat cushion",
                    Role = ReferenceImagePartRole.PrimaryMass,
                    RelativeWidth = 3,
                    RelativeDepth = 1,
                    RelativeHeight = 0.35,
                    RelativeCenterZ = 0.4,
                    MaterialKey = "fabric"
                },
                new()
                {
                    Name = "back cushion",
                    Role = ReferenceImagePartRole.StructuralMass,
                    RelativeWidth = 3,
                    RelativeDepth = 0.25,
                    RelativeHeight = 0.9,
                    RelativeCenterY = 0.55,
                    RelativeCenterZ = 0.95,
                    MaterialKey = "fabric"
                }
            },
            DetailCues = new List<ReferenceImageVisibleDetailCueRequest>
            {
                new()
                {
                    PartName = "seat cushion",
                    Kind = "seam",
                    Description = "front horizontal raised seam",
                    Role = ReferenceImagePartRole.AdditiveDetail
                },
                new()
                {
                    PartName = "seat cushion",
                    Kind = "texture",
                    Description = "woven strip texture across the cushion face",
                    Role = ReferenceImagePartRole.MaterialOnly
                },
                new()
                {
                    PartName = "seat cushion",
                    Kind = "shadow",
                    Description = "cast shadow line below the cushion",
                    Role = ReferenceImagePartRole.Reference
                },
                new()
                {
                    PartName = "seat cushion",
                    Kind = "highlight",
                    Description = "specular highlight on cushion center",
                    Role = ReferenceImagePartRole.Reference
                }
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
}
