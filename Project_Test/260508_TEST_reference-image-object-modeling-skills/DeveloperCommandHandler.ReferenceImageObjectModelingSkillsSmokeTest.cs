using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using MCP_Rhino.Server.Infrastructure.Plugin;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string ReferenceImageObjectModelingSkillsSlug = "reference-image-object-modeling-skills-smoke-test";

    partial void RegisterReferenceImageObjectModelingSkillsHandlers()
    {
        _extensionHandlers[ReferenceImageObjectModelingSkillsSlug] = HandleReferenceImageObjectModelingSkillsSmokeTest;
    }

    private bool HandleReferenceImageObjectModelingSkillsSmokeTest(string[] args)
    {
        try
        {
            if (McpRhinoPlugin.Instance is null)
            {
                RunReferenceImageObjectModelingSkillsCliFallbackSmoke();
            }
            else
            {
                string filePath = args.Length > 1 ? args[1] : string.Empty;
                RunReferenceImageObjectModelingSkillsLiveSmoke(filePath);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Reference image object modeling skills smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunReferenceImageObjectModelingSkillsCliFallbackSmoke()
    {
        ReferenceImagePrimitiveDecomposition sofa = SmokeBriefDecomposition(SofaBrief(), "sofa", 2d);
        RequireReferenceImageSkillPrimitive(sofa, "seat cushion", ReferenceImagePrimitiveVocabularyKind.RoundedBox);
        RequireReferenceImageSkillPrimitive(sofa, "bolster roll", ReferenceImagePrimitiveVocabularyKind.Capsule);

        ReferenceImagePrimitiveDecomposition bottle = SmokeBriefDecomposition(BottleBrief(), "bottle", 2d);
        RequireReferenceImageSkillPrimitive(bottle, "bottle body", ReferenceImagePrimitiveVocabularyKind.Cylinder);

        ReferenceImagePrimitiveDecomposition speaker = SmokeBriefDecomposition(SpeakerBrief(), "speaker", 2d);
        RequireReferenceImageSkillPrimitive(speaker, "driver rim", ReferenceImagePrimitiveVocabularyKind.Torus);

        ReferenceImageRefinementPlan sofaRefinement = RequireReferenceImageSkillSuccess(
            _referenceImageDetailRefinementSkill.Plan(new PlanReferenceImageDetailRefinementRequest
            {
                Brief = RequireReferenceImageSkillSuccess(_referenceImageModelBriefSkill.Build(SofaBrief()), "Build sofa brief").Brief,
                Decomposition = sofa
            }),
            "Plan sofa refinement").Plan;
        if (!sofaRefinement.Actions.Any(action => action.Kind == ReferenceImageRefinementActionKind.AddRaisedStrip))
        {
            throw new InvalidOperationException("Expected sofa refinement to include a raised strip action.");
        }

        ReferenceImageMaterialPlan materialPlan = RequireReferenceImageSkillSuccess(
            _referenceImageMaterialPlanningSkill.Plan(new PlanReferenceImageMaterialsRequest
            {
                Brief = RequireReferenceImageSkillSuccess(_referenceImageModelBriefSkill.Build(SofaBrief()), "Build material brief").Brief,
                Decomposition = sofa
            }),
            "Plan sofa materials").Plan;
        if (materialPlan.Materials.Count == 0 || materialPlan.Fallbacks.Count == 0)
        {
            throw new InvalidOperationException("Expected material plan with texture fallback.");
        }

        ReferenceImageIterationDecision massingDecision = RequireReferenceImageSkillSuccess(
            _referenceImageIterationDecisionSkill.Decide(new DecideReferenceImageIterationRequest
            {
                Trace = new ReferenceImageModelingTrace { ObjectType = "sofa", MassingObjectCount = 0 },
                MaxIterations = 3
            }),
            "Decide missing massing").Decision;
        if (massingDecision.Kind != ReferenceImageIterationDecisionKind.ReviseMassing)
        {
            throw new InvalidOperationException($"Expected ReviseMassing, got {massingDecision.Kind}.");
        }

        ReferenceImageIterationDecision acceptDecision = RequireReferenceImageSkillSuccess(
            _referenceImageIterationDecisionSkill.Decide(new DecideReferenceImageIterationRequest
            {
                Trace = new ReferenceImageModelingTrace
                {
                    ObjectType = "sofa",
                    MassingObjectCount = 3,
                    DetailObjectCount = 1,
                    MaterialAssignmentCount = 3
                },
                MaxIterations = 3
            }),
            "Decide accept").Decision;
        if (acceptDecision.Kind != ReferenceImageIterationDecisionKind.Accept)
        {
            throw new InvalidOperationException($"Expected Accept, got {acceptDecision.Kind}.");
        }

        OperationResponse<ReferenceImageInitialMassingResponse> massingFallback =
            _referenceImageInitialMassingSkill.Create(new CreateReferenceImageInitialMassingRequest
            {
                FilePath = "C:/mcp-rhino/reference-image-object-modeling-skills-smoke.3dm",
                LayerFullPath = "MCP::REFERENCE_IMAGE_OBJECT_MODELING_SKILLS_SMOKE",
                Decomposition = sofa
            });
        if (massingFallback.Success || !string.Equals(massingFallback.Message, "LIVE_RHINO_REQUIRED", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Initial massing should return LIVE_RHINO_REQUIRED in CLI fallback mode; got Success={massingFallback.Success}, Message={massingFallback.Message}");
        }

        Console.WriteLine("[OK] reference-image-object-modeling-skills deterministic planning passed for sofa, bottle, and speaker categories.");
        Console.WriteLine("[OK] initial massing skill returned LIVE_RHINO_REQUIRED in CLI fallback mode.");
    }

    private void RunReferenceImageObjectModelingSkillsLiveSmoke(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("Live reference image object modeling skills smoke requires a saved active document path.");
        }

        string suffix = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        ReferenceImagePrimitiveDecomposition sofa = SmokeBriefDecomposition(SofaBrief($"skills-smoke-{suffix}"), "sofa", 2d);
        ReferenceImageInitialMassingResponse massing = RequireReferenceImageSkillSuccess(
            _referenceImageInitialMassingSkill.Create(new CreateReferenceImageInitialMassingRequest
            {
                FilePath = filePath,
                LayerFullPath = $"MCP::REFERENCE_IMAGE_OBJECT_MODELING_SKILLS_SMOKE_{suffix}",
                Decomposition = sofa,
                Common = new GeometryCreationCommonOptions
                {
                    Color = new ObjectColorRequest { R = 132, G = 96, B = 84 },
                    Name = "reference image skills smoke"
                }
            }),
            "Create initial massing");

        if (massing.CreatedObjectCount < 2)
        {
            throw new InvalidOperationException($"Expected at least two massing objects, got {massing.CreatedObjectCount}.");
        }

        Console.WriteLine("[OK] reference-image-object-modeling-skills live smoke completed.");
        Console.WriteLine($"[OK] Created {massing.CreatedObjectCount} initial massing objects on layer {massing.LayerFullPath}.");
    }

    private ReferenceImagePrimitiveDecomposition SmokeBriefDecomposition(
        BuildReferenceImageModelBriefRequest request,
        string label,
        double scale)
    {
        ReferenceImageModelBrief brief = RequireReferenceImageSkillSuccess(
            _referenceImageModelBriefSkill.Build(request),
            $"Build {label} brief").Brief;
        return RequireReferenceImageSkillSuccess(
            _referenceImagePrimitiveDecompositionSkill.Decompose(new DecomposeReferenceImagePrimitivesRequest
            {
                Brief = brief,
                Scale = scale
            }),
            $"Decompose {label} primitives").Decomposition;
    }

    private static BuildReferenceImageModelBriefRequest SofaBrief(string label = "skills-sofa")
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

    private static BuildReferenceImageModelBriefRequest BottleBrief()
    {
        return new BuildReferenceImageModelBriefRequest
        {
            ReferenceImageLabel = "skills-bottle",
            ObjectType = "water bottle",
            ObjectTypeConfidence = 0.88d,
            WidthRatio = 0.45,
            DepthRatio = 0.45,
            HeightRatio = 2.2,
            OverallEdgeCharacter = ReferenceImageObjectEdgeCharacter.Mixed,
            Parts = new List<ReferenceImageBriefPartRequest>
            {
                new() { Name = "bottle body", Role = ReferenceImagePartRole.PrimaryMass, RelativeWidth = 0.45, RelativeDepth = 0.45, RelativeHeight = 2.0, RelativeCenterZ = 1.0, Notes = new List<string> { "tall cylinder" }, MaterialKey = "plastic" },
                new() { Name = "cap", Role = ReferenceImagePartRole.StructuralMass, RelativeWidth = 0.35, RelativeDepth = 0.35, RelativeHeight = 0.18, RelativeCenterZ = 2.1, PreferredPrimitiveHint = ReferenceImagePrimitiveVocabularyKind.Cylinder, MaterialKey = "cap" }
            },
            DetailCues = new List<ReferenceImageVisibleDetailCueRequest>
            {
                new() { PartName = "bottle body", Kind = "label", Description = "flat printed label", PreferredRepresentation = ReferenceImagePrimitiveVocabularyKind.DecalPlane }
            },
            MaterialCues = new List<ReferenceImageMaterialCueRequest>
            {
                new() { Key = "plastic", Description = "slightly transparent blue plastic", BaseColor = new ObjectColorRequest { R = 110, G = 170, B = 210 }, Intent = ReferenceImageMaterialIntent.Transparent, Transparency = 0.25 }
            }
        };
    }

    private static BuildReferenceImageModelBriefRequest SpeakerBrief()
    {
        return new BuildReferenceImageModelBriefRequest
        {
            ReferenceImageLabel = "skills-speaker",
            ObjectType = "speaker",
            ObjectTypeConfidence = 0.86d,
            WidthRatio = 1,
            DepthRatio = 0.6,
            HeightRatio = 1.5,
            OverallEdgeCharacter = ReferenceImageObjectEdgeCharacter.Hard,
            Parts = new List<ReferenceImageBriefPartRequest>
            {
                new() { Name = "speaker cabinet", Role = ReferenceImagePartRole.PrimaryMass, RelativeWidth = 1, RelativeDepth = 0.6, RelativeHeight = 1.5, RelativeCenterZ = 0.75, MaterialKey = "black-plastic" },
                new() { Name = "driver rim", Role = ReferenceImagePartRole.StructuralMass, RelativeWidth = 0.55, RelativeDepth = 0.08, RelativeHeight = 0.55, RelativeCenterY = -0.32, RelativeCenterZ = 0.85, Notes = new List<string> { "circular ring rim" }, MaterialKey = "black-plastic" }
            },
            DetailCues = new List<ReferenceImageVisibleDetailCueRequest>
            {
                new() { PartName = "speaker cabinet", Kind = "texture", Description = "fine grille perforation pattern", Role = ReferenceImagePartRole.MaterialOnly }
            },
            MaterialCues = new List<ReferenceImageMaterialCueRequest>
            {
                new() { Key = "black-plastic", Description = "dark satin plastic", BaseColor = new ObjectColorRequest { R = 25, G = 25, B = 28 }, Intent = ReferenceImageMaterialIntent.Plastic, Roughness = 0.45 }
            }
        };
    }

    private static void RequireReferenceImageSkillPrimitive(
        ReferenceImagePrimitiveDecomposition decomposition,
        string partName,
        ReferenceImagePrimitiveVocabularyKind expected)
    {
        ReferenceImagePrimitivePart? part = decomposition.Parts
            .FirstOrDefault(item => string.Equals(item.PartName, partName, StringComparison.OrdinalIgnoreCase));
        if (part is null)
        {
            throw new InvalidOperationException($"Missing decomposed part: {partName}");
        }

        if (part.PreferredPrimitive != expected)
        {
            throw new InvalidOperationException($"Expected {partName} primitive {expected}, got {part.PreferredPrimitive}.");
        }
    }

    private static T RequireReferenceImageSkillSuccess<T>(OperationResponse<T> response, string label)
        where T : class
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{label} failed: {response.Message}");
        }

        return response.Data;
    }
}
