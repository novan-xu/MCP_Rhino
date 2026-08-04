using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using MCP_Rhino.Server.Domain.Rules;
using MCP_Rhino.Server.Skills.Modeling;

namespace MCP_Rhino.Server.Agents.Modeling;

public sealed class ReferenceImageObjectModelingAgent
{
    private readonly ReferenceImageModelBriefSkill _briefSkill;
    private readonly ReferenceImagePrimitiveDecompositionSkill _decompositionSkill;
    private readonly ReferenceImageProductGeometryPlanningSkill _productGeometryPlanningSkill;
    private readonly ReferenceImageInitialMassingSkill _initialMassingSkill;
    private readonly ReferenceImageDetailRefinementSkill _detailRefinementSkill;
    private readonly ReferenceImageMaterialPlanningSkill _materialPlanningSkill;
    private readonly ReferenceImageIterationDecisionSkill _iterationDecisionSkill;
    private readonly ReferenceImageVisualQaCaptureService _visualQaCaptureService;
    private readonly RhinoMaterialService _materialService;
    private readonly GeometryCreationSkill _geometryCreationSkill;

    public ReferenceImageObjectModelingAgent(
        ReferenceImageModelBriefSkill briefSkill,
        ReferenceImagePrimitiveDecompositionSkill decompositionSkill,
        ReferenceImageProductGeometryPlanningSkill productGeometryPlanningSkill,
        ReferenceImageInitialMassingSkill initialMassingSkill,
        ReferenceImageDetailRefinementSkill detailRefinementSkill,
        ReferenceImageMaterialPlanningSkill materialPlanningSkill,
        ReferenceImageIterationDecisionSkill iterationDecisionSkill,
        ReferenceImageVisualQaCaptureService visualQaCaptureService,
        RhinoMaterialService materialService,
        GeometryCreationSkill geometryCreationSkill)
    {
        _briefSkill = briefSkill;
        _decompositionSkill = decompositionSkill;
        _productGeometryPlanningSkill = productGeometryPlanningSkill;
        _initialMassingSkill = initialMassingSkill;
        _detailRefinementSkill = detailRefinementSkill;
        _materialPlanningSkill = materialPlanningSkill;
        _iterationDecisionSkill = iterationDecisionSkill;
        _visualQaCaptureService = visualQaCaptureService;
        _materialService = materialService;
        _geometryCreationSkill = geometryCreationSkill;
    }

    public OperationResponse<ReferenceImageObjectModelingAgentResponse> Run(
        ReferenceImageObjectModelingAgentRequest request)
    {
        OperationResponse validation = ValidateRequest(request);
        if (!validation.Success)
        {
            return OperationResponse<ReferenceImageObjectModelingAgentResponse>.Fail(validation.Message);
        }

        var response = new ReferenceImageObjectModelingAgentResponse
        {
            FilePath = request.FilePath,
            TargetLayerFullPath = ResolveTargetLayer(request),
            Status = ReferenceImageObjectModelingStatus.NotStarted,
            StatusReason = "Reference image object modeling started."
        };
        var warnings = new List<ObjectEditWarning>();
        var gaps = new List<ReferenceImageCapabilityGapResponse>();
        var qaCheckpoints = new List<ReferenceImageVisualQaCheckpointResponse>();
        var detailObjects = new List<ReferenceImageCreatedPartObjectResponse>();

        OperationResponse<ReferenceImageModelBriefResponse> briefResult = _briefSkill.Build(request.BriefRequest);
        if (!briefResult.Success || briefResult.Data is null)
        {
            return StopWithGap(response, gaps, warnings, ReferenceImageCapabilityGapLayer.Skill, "BRIEF_FAILED", briefResult.Message);
        }

        response.Plan.Brief = briefResult.Data.Brief;
        warnings.AddRange(briefResult.Data.Warnings);

        OperationResponse<ReferenceImagePrimitiveDecompositionResponse> decompositionResult =
            _decompositionSkill.Decompose(new DecomposeReferenceImagePrimitivesRequest
            {
                Brief = response.Plan.Brief,
                Scale = request.Scale
            });
        if (!decompositionResult.Success || decompositionResult.Data is null)
        {
            return StopWithGap(response, gaps, warnings, ReferenceImageCapabilityGapLayer.Skill, "DECOMPOSITION_FAILED", decompositionResult.Message);
        }

        response.Plan.Decomposition = decompositionResult.Data.Decomposition;
        warnings.AddRange(decompositionResult.Data.Warnings);

        OperationResponse<ReferenceImageProductGeometryPlanResponse> productGeometryPlanResult =
            _productGeometryPlanningSkill.Plan(new PlanReferenceImageProductGeometryRequest
            {
                Brief = response.Plan.Brief,
                Scale = request.Scale
            });
        if (!productGeometryPlanResult.Success || productGeometryPlanResult.Data is null)
        {
            return StopWithGap(response, gaps, warnings, ReferenceImageCapabilityGapLayer.Skill, "PRODUCT_GEOMETRY_PLAN_FAILED", productGeometryPlanResult.Message);
        }

        response.Plan.ProductGeometryPlan = productGeometryPlanResult.Data.Plan;
        warnings.AddRange(productGeometryPlanResult.Data.Warnings);

        OperationResponse<ReferenceImageInitialMassingResponse> massingResult =
            _initialMassingSkill.Create(new CreateReferenceImageInitialMassingRequest
            {
                FilePath = request.FilePath,
                LayerFullPath = response.TargetLayerFullPath,
                Decomposition = response.Plan.Decomposition,
                CreateLayer = request.CreateTargetLayer,
                Common = new GeometryCreationCommonOptions
                {
                    LayerFullPath = response.TargetLayerFullPath,
                    Name = response.Plan.Brief.ObjectType,
                    UserText = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["mcp.agent"] = "ReferenceImageObjectModelingAgent",
                        ["mcp.source.image"] = response.Plan.Brief.ReferenceImageLabel
                    }
                }
            });
        if (!massingResult.Success || massingResult.Data is null)
        {
            return StopWithGap(response, gaps, warnings, ResolveGapLayer(massingResult.Message), "INITIAL_MASSING_FAILED", massingResult.Message);
        }

        response.InitialMassing = massingResult.Data;
        warnings.AddRange(massingResult.Data.Warnings);

        List<Guid> massingObjectIds = massingResult.Data.CreatedObjects.Select(item => item.ObjectId).ToList();
        if (request.RunVisualQa)
        {
            OperationResponse<ReferenceImageVisualQaCaptureResponse> qaResult = CaptureQa(
                request,
                response.Plan.Brief,
                ReferenceImageVisualQaCheckpointKind.InitialMassing,
                massingObjectIds);
            if (!qaResult.Success || qaResult.Data is null)
            {
                return StopWithGap(response, gaps, warnings, ResolveGapLayer(qaResult.Message), "MASSING_QA_FAILED", qaResult.Message);
            }

            qaCheckpoints.Add(new ReferenceImageVisualQaCheckpointResponse
            {
                Name = "initial-massing",
                CheckpointKind = ReferenceImageVisualQaCheckpointKind.InitialMassing,
                Capture = qaResult.Data
            });
        }

        OperationResponse<ReferenceImageDetailRefinementPlanResponse> detailPlanResult =
            _detailRefinementSkill.Plan(new PlanReferenceImageDetailRefinementRequest
            {
                Brief = response.Plan.Brief,
                Decomposition = response.Plan.Decomposition,
                VisualQa = qaCheckpoints.LastOrDefault()?.Capture,
                QaFindings = request.QaFindings
            });
        if (!detailPlanResult.Success || detailPlanResult.Data is null)
        {
            return StopWithGap(response, gaps, warnings, ReferenceImageCapabilityGapLayer.Skill, "DETAIL_PLAN_FAILED", detailPlanResult.Message);
        }

        response.Plan.DetailPlan = detailPlanResult.Data.Plan;
        warnings.AddRange(detailPlanResult.Data.Warnings);
        warnings.AddRange(BuildNonGeometricVisualCueWarnings(response.Plan.DetailPlan));
        if (request.ExecuteSimpleRaisedStripDetails)
        {
            OperationResponse<IReadOnlyList<ReferenceImageCreatedPartObjectResponse>> detailExecution =
                ExecuteSimpleRaisedStripDetails(request, response.Plan.Decomposition, response.Plan.DetailPlan, response.TargetLayerFullPath);
            if (detailExecution.Success && detailExecution.Data is not null)
            {
                detailObjects.AddRange(detailExecution.Data);
            }
            else if (!string.IsNullOrWhiteSpace(detailExecution.Message))
            {
                gaps.Add(new ReferenceImageCapabilityGapResponse
                {
                    Layer = ResolveGapLayer(detailExecution.Message),
                    Code = "DETAIL_EXECUTION_PARTIAL",
                    Message = detailExecution.Message
                });
            }
        }

        Dictionary<string, List<Guid>> objectIdsByPartName = BuildObjectIdsByPartName(massingResult.Data, detailObjects);
        OperationResponse<ReferenceImageMaterialPlanningResponse> materialPlanResult =
            _materialPlanningSkill.Plan(new PlanReferenceImageMaterialsRequest
            {
                Brief = response.Plan.Brief,
                Decomposition = response.Plan.Decomposition,
                ObjectIdsByPartName = objectIdsByPartName
            });
        if (!materialPlanResult.Success || materialPlanResult.Data is null)
        {
            return StopWithGap(response, gaps, warnings, ReferenceImageCapabilityGapLayer.Skill, "MATERIAL_PLAN_FAILED", materialPlanResult.Message);
        }

        response.Plan.MaterialPlan = materialPlanResult.Data.Plan;
        warnings.AddRange(materialPlanResult.Data.Warnings);

        ObjectMaterialAssignmentResponse? materialAssignment = null;
        if (request.ApplyMaterials)
        {
            OperationResponse<RenderMaterialCreationResponse> materialCreation = CreateMaterials(request.FilePath, response.Plan.MaterialPlan);
            if (materialCreation.Success && materialCreation.Data is not null)
            {
                response.MaterialCreation = materialCreation.Data;
                OperationResponse<ObjectMaterialAssignmentResponse> assignment = ApplyMaterials(request.FilePath, response.Plan.MaterialPlan);
                if (assignment.Success && assignment.Data is not null)
                {
                    materialAssignment = assignment.Data;
                    response.MaterialAssignment = assignment.Data;
                }
                else
                {
                    gaps.Add(new ReferenceImageCapabilityGapResponse
                    {
                        Layer = ResolveGapLayer(assignment.Message),
                        Code = "MATERIAL_ASSIGNMENT_FAILED",
                        Message = assignment.Message
                    });
                }
            }
            else
            {
                gaps.Add(new ReferenceImageCapabilityGapResponse
                {
                    Layer = ResolveGapLayer(materialCreation.Message),
                    Code = "MATERIAL_CREATION_FAILED",
                    Message = materialCreation.Message
                });
            }
        }

        List<Guid> finalObjectIds = massingObjectIds.Concat(detailObjects.Select(item => item.ObjectId)).Distinct().ToList();
        if (request.RunVisualQa && finalObjectIds.Count > 0)
        {
            OperationResponse<ReferenceImageVisualQaCaptureResponse> finalQa = CaptureQa(
                request,
                response.Plan.Brief,
                ReferenceImageVisualQaCheckpointKind.FinalAcceptance,
                finalObjectIds);
            if (finalQa.Success && finalQa.Data is not null)
            {
                qaCheckpoints.Add(new ReferenceImageVisualQaCheckpointResponse
                {
                    Name = "final-review",
                    CheckpointKind = ReferenceImageVisualQaCheckpointKind.FinalAcceptance,
                    Capture = finalQa.Data
                });
            }
            else
            {
                gaps.Add(new ReferenceImageCapabilityGapResponse
                {
                    Layer = ResolveGapLayer(finalQa.Message),
                    Code = "FINAL_QA_FAILED",
                    Message = finalQa.Message
                });
            }
        }

        response.Plan.Trace = BuildTrace(response.Plan.Brief, massingResult.Data, detailObjects, materialAssignment, gaps);
        OperationResponse<ReferenceImageIterationDecisionResponse> decisionResult =
            _iterationDecisionSkill.Decide(new DecideReferenceImageIterationRequest
            {
                Trace = response.Plan.Trace,
                VisualQa = qaCheckpoints.LastOrDefault()?.Capture,
                QaFindings = request.QaFindings,
                MaxIterations = Math.Max(1, 1 + request.MaxMassingCorrectionLoops + request.MaxDetailMaterialCorrectionLoops)
            });
        if (decisionResult.Success && decisionResult.Data is not null)
        {
            response.IterationDecision = decisionResult.Data.Decision;
        }
        else
        {
            gaps.Add(new ReferenceImageCapabilityGapResponse
            {
                Layer = ReferenceImageCapabilityGapLayer.Skill,
                Code = "ITERATION_DECISION_FAILED",
                Message = decisionResult.Message
            });
        }

        response.DetailObjects = detailObjects;
        response.QaCheckpoints = qaCheckpoints;
        response.CapabilityGaps = gaps;
        response.Warnings = warnings;
        response.Status = ResolveStatus(response.IterationDecision, gaps, detailObjects, response.Plan.DetailPlan);
        response.StatusReason = ResolveStatusReason(response.Status, response.IterationDecision, gaps);

        return OperationResponse<ReferenceImageObjectModelingAgentResponse>.Ok(
            response,
            response.StatusReason);
    }

    private OperationResponse<ReferenceImageVisualQaCaptureResponse> CaptureQa(
        ReferenceImageObjectModelingAgentRequest request,
        ReferenceImageModelBrief brief,
        ReferenceImageVisualQaCheckpointKind checkpointKind,
        List<Guid> objectIds)
    {
        return _visualQaCaptureService.Capture(new CaptureReferenceImageModelingQaViewsRequest
        {
            FilePath = request.FilePath,
            ReferenceImagePath = brief.ReferenceImagePath,
            ReferenceImageLabel = brief.ReferenceImageLabel,
            CheckpointKind = checkpointKind,
            ObjectIds = objectIds,
            ImageSizePx = request.QaImageSizePx ?? new ImageSizePxRequest { Width = 240, Height = 160 },
            MaxObjectSummaries = 20
        });
    }

    private OperationResponse<IReadOnlyList<ReferenceImageCreatedPartObjectResponse>> ExecuteSimpleRaisedStripDetails(
        ReferenceImageObjectModelingAgentRequest request,
        ReferenceImagePrimitiveDecomposition decomposition,
        ReferenceImageRefinementPlan detailPlan,
        string layerFullPath)
    {
        var created = new List<ReferenceImageCreatedPartObjectResponse>();
        List<ReferenceImageRefinementAction> raisedStripActions = detailPlan.Actions
            .Where(ShouldExecuteAsSimpleRaisedStrip)
            .Take(4)
            .ToList();
        if (raisedStripActions.Count == 0)
        {
            return OperationResponse<IReadOnlyList<ReferenceImageCreatedPartObjectResponse>>.Ok(created);
        }

        foreach (ReferenceImageRefinementAction action in raisedStripActions)
        {
            ReferenceImagePrimitivePart? part = ResolveDetailPart(decomposition, action);
            if (part is null)
            {
                continue;
            }

            double stripHeight = Math.Max(part.SizeZ * 0.05d, 0.02d);
            double stripWidth = Math.Max(Math.Min(part.SizeY, part.SizeZ) * 0.08d, 0.03d);
            double y = part.CenterY - (part.SizeY * 0.5d);
            double z = part.CenterZ + (part.SizeZ * 0.5d) + (stripHeight * 0.5d);
            OperationResponse<GeneralPrimitiveCreationResponse> result = _geometryCreationSkill.Create(new CreateRaisedStripsRequest
            {
                FilePath = request.FilePath,
                Items = new List<RaisedStripItemRequest>
                {
                    new()
                    {
                        StartX = part.CenterX - (part.SizeX * 0.45d),
                        StartY = y,
                        StartZ = z,
                        EndX = part.CenterX + (part.SizeX * 0.45d),
                        EndY = y,
                        EndZ = z,
                        Width = stripWidth,
                        Height = stripHeight,
                        Name = $"{part.PartName} raised strip"
                    }
                },
                Common = new GeometryCreationCommonOptions
                {
                    LayerFullPath = layerFullPath,
                    Name = $"{part.PartName} raised strip",
                    UserText = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["mcp.agent"] = "ReferenceImageObjectModelingAgent",
                        ["mcp.modeling.stage"] = "detail",
                        ["mcp.part.name"] = part.PartName,
                        ["mcp.detail.kind"] = "raised-strip"
                    }
                }
            });

            if (!result.Success || result.Data is null)
            {
                return OperationResponse<IReadOnlyList<ReferenceImageCreatedPartObjectResponse>>.Fail(result.Message);
            }

            created.AddRange(result.Data.CreatedObjects.Select(item => new ReferenceImageCreatedPartObjectResponse
            {
                PartName = part.PartName,
                ObjectId = item.ObjectId,
                PrimitiveKind = item.Kind.ToString(),
                GeometryTypeName = item.GeometryTypeName
            }));
        }

        return OperationResponse<IReadOnlyList<ReferenceImageCreatedPartObjectResponse>>.Ok(created);
    }

    internal static bool ShouldExecuteAsSimpleRaisedStrip(ReferenceImageRefinementAction action)
    {
        return action.Kind == ReferenceImageRefinementActionKind.AddRaisedStrip
            && !ReferenceImageVisualCueGeometryPolicy.IsNonGeometricVisualCue(action);
    }

    private OperationResponse<RenderMaterialCreationResponse> CreateMaterials(
        string filePath,
        ReferenceImageMaterialPlan materialPlan)
    {
        return _materialService.Create(new CreateRenderMaterialsRequest
        {
            FilePath = filePath,
            Items = materialPlan.Materials.Select(material => new RenderMaterialItemRequest
            {
                Name = material.Name,
                BaseColor = new ObjectColorRequest
                {
                    R = material.BaseColor.R,
                    G = material.BaseColor.G,
                    B = material.BaseColor.B
                },
                Roughness = material.Roughness,
                Transparency = material.Transparency
            }).ToList(),
            ReuseExistingByName = true
        });
    }

    private OperationResponse<ObjectMaterialAssignmentResponse> ApplyMaterials(
        string filePath,
        ReferenceImageMaterialPlan materialPlan)
    {
        return _materialService.Apply(new ApplyObjectMaterialsRequest
        {
            FilePath = filePath,
            Assignments = materialPlan.Assignments
                .Where(assignment => assignment.ObjectIds.Count > 0)
                .Select(assignment => new ObjectMaterialAssignmentRequest
                {
                    MaterialName = assignment.MaterialName,
                    ObjectIds = assignment.ObjectIds,
                    UserText = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["mcp.agent"] = "ReferenceImageObjectModelingAgent"
                    }
                })
                .ToList()
        });
    }

    private static ReferenceImageModelingTrace BuildTrace(
        ReferenceImageModelBrief brief,
        ReferenceImageInitialMassingResponse massing,
        IReadOnlyList<ReferenceImageCreatedPartObjectResponse> detailObjects,
        ObjectMaterialAssignmentResponse? materialAssignment,
        IReadOnlyList<ReferenceImageCapabilityGapResponse> gaps)
    {
        var completedStages = new List<string> { "brief", "decomposition", "initial-massing" };
        if (detailObjects.Count > 0)
        {
            completedStages.Add("detail");
        }

        if (materialAssignment?.AssignedCount > 0)
        {
            completedStages.Add("material");
        }

        return new ReferenceImageModelingTrace
        {
            ReferenceImageLabel = brief.ReferenceImageLabel,
            ObjectType = brief.ObjectType,
            IterationIndex = 0,
            MassingObjectCount = massing.CreatedObjectCount,
            DetailObjectCount = detailObjects.Count,
            MaterialAssignmentCount = materialAssignment?.AssignedCount ?? 0,
            CompletedStages = completedStages,
            KnownGaps = gaps.Select(gap => $"{gap.Code}: {gap.Message}").ToList()
        };
    }

    private static Dictionary<string, List<Guid>> BuildObjectIdsByPartName(
        ReferenceImageInitialMassingResponse massing,
        IEnumerable<ReferenceImageCreatedPartObjectResponse> detailObjects)
    {
        var result = new Dictionary<string, List<Guid>>(StringComparer.OrdinalIgnoreCase);
        foreach (ReferenceImageCreatedPartObjectResponse item in massing.CreatedObjects.Concat(detailObjects))
        {
            if (!result.TryGetValue(item.PartName, out List<Guid>? ids))
            {
                ids = new List<Guid>();
                result[item.PartName] = ids;
            }

            ids.Add(item.ObjectId);
        }

        return result;
    }

    private static IEnumerable<ObjectEditWarning> BuildNonGeometricVisualCueWarnings(
        ReferenceImageRefinementPlan detailPlan)
    {
        foreach (ReferenceImageRefinementAction action in detailPlan.Actions
                     .Where(ReferenceImageVisualCueGeometryPolicy.IsNonGeometricVisualCue))
        {
            string target = !string.IsNullOrWhiteSpace(action.PartName)
                ? action.PartName
                : "reference image";
            yield return new ObjectEditWarning
            {
                Code = "NON_GEOMETRIC_VISUAL_CUE_SKIPPED",
                Message = $"{target}: {action.Description} was treated as material, texture, lighting, or shadow context and was not modeled as geometry."
            };
        }
    }

    private static ReferenceImagePrimitivePart? ResolveDetailPart(
        ReferenceImagePrimitiveDecomposition decomposition,
        ReferenceImageRefinementAction action)
    {
        if (!string.IsNullOrWhiteSpace(action.PartName))
        {
            ReferenceImagePrimitivePart? direct = decomposition.Parts.FirstOrDefault(
                part => string.Equals(part.PartName, action.PartName, StringComparison.OrdinalIgnoreCase));
            if (direct is not null)
            {
                return direct;
            }
        }

        return decomposition.Parts.FirstOrDefault(
            part => part.Role is ReferenceImagePartRole.PrimaryMass or ReferenceImagePartRole.StructuralMass);
    }

    private static OperationResponse ValidateRequest(ReferenceImageObjectModelingAgentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return OperationResponse.Fail("FilePath is required.");
        }

        if (request.BriefRequest.Parts.Count == 0 && string.IsNullOrWhiteSpace(request.BriefRequest.ObjectType))
        {
            return OperationResponse.Fail(
                "Structured image observations are required: provide BriefRequest.ObjectType and visible parts. Raw image inference is not implemented in the Rhino server.");
        }

        if (request.Scale <= 0d || double.IsNaN(request.Scale) || double.IsInfinity(request.Scale))
        {
            return OperationResponse.Fail("Scale must be greater than zero.");
        }

        return OperationResponse.Ok();
    }

    private static string ResolveTargetLayer(ReferenceImageObjectModelingAgentRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.TargetLayerFullPath))
        {
            return request.TargetLayerFullPath.Trim();
        }

        string label = !string.IsNullOrWhiteSpace(request.BriefRequest.ReferenceImageLabel)
            ? request.BriefRequest.ReferenceImageLabel.Trim()
            : "reference-object";
        return $"MCP::REFERENCE_IMAGE_OBJECT_MODELING::{label}";
    }

    private static ReferenceImageObjectModelingStatus ResolveStatus(
        ReferenceImageIterationDecision? decision,
        IReadOnlyList<ReferenceImageCapabilityGapResponse> gaps,
        IReadOnlyList<ReferenceImageCreatedPartObjectResponse> detailObjects,
        ReferenceImageRefinementPlan detailPlan)
    {
        if (gaps.Any(gap => gap.Code is "INITIAL_MASSING_FAILED" or "MASSING_QA_FAILED"))
        {
            return ReferenceImageObjectModelingStatus.CapabilityGap;
        }

        if (decision?.Kind == ReferenceImageIterationDecisionKind.Accept && gaps.Count == 0)
        {
            return ReferenceImageObjectModelingStatus.Completed;
        }

        if (decision?.Kind is ReferenceImageIterationDecisionKind.ReviseMassing
            or ReferenceImageIterationDecisionKind.ReviseDetail
            or ReferenceImageIterationDecisionKind.ReviseMaterial)
        {
            return ReferenceImageObjectModelingStatus.NeedsIteration;
        }

        int executableDetailActionCount = detailPlan.Actions
            .Count(action => !ReferenceImageVisualCueGeometryPolicy.IsNonGeometricVisualCue(action));
        if (gaps.Count > 0 || executableDetailActionCount > detailObjects.Count)
        {
            return ReferenceImageObjectModelingStatus.PartialCompleted;
        }

        return ReferenceImageObjectModelingStatus.Completed;
    }

    private static string ResolveStatusReason(
        ReferenceImageObjectModelingStatus status,
        ReferenceImageIterationDecision? decision,
        IReadOnlyList<ReferenceImageCapabilityGapResponse> gaps)
    {
        if (status == ReferenceImageObjectModelingStatus.CapabilityGap)
        {
            return gaps.Count == 0
                ? "Reference image object modeling stopped because a required capability is unavailable."
                : gaps[0].Message;
        }

        if (decision is not null)
        {
            return $"{status}: {decision.Kind}.";
        }

        return status.ToString();
    }

    private static ReferenceImageCapabilityGapLayer ResolveGapLayer(string message)
    {
        if (message.Contains("LIVE_RHINO_REQUIRED", StringComparison.OrdinalIgnoreCase)
            || message.Contains("NO_ACTIVE_DOCUMENT", StringComparison.OrdinalIgnoreCase)
            || message.Contains("ACTIVE_DOC_UNSAVED", StringComparison.OrdinalIgnoreCase)
            || message.Contains("FILE_NOT_ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            return ReferenceImageCapabilityGapLayer.LiveRhino;
        }

        if (message.Contains("material", StringComparison.OrdinalIgnoreCase))
        {
            return ReferenceImageCapabilityGapLayer.Tool;
        }

        return ReferenceImageCapabilityGapLayer.Agent;
    }

    private static OperationResponse<ReferenceImageObjectModelingAgentResponse> StopWithGap(
        ReferenceImageObjectModelingAgentResponse response,
        List<ReferenceImageCapabilityGapResponse> gaps,
        List<ObjectEditWarning> warnings,
        ReferenceImageCapabilityGapLayer layer,
        string code,
        string message)
    {
        gaps.Add(new ReferenceImageCapabilityGapResponse
        {
            Layer = layer,
            Code = code,
            Message = message
        });
        response.Status = ReferenceImageObjectModelingStatus.CapabilityGap;
        response.StatusReason = message;
        response.CapabilityGaps = gaps;
        response.Warnings = warnings;
        return OperationResponse<ReferenceImageObjectModelingAgentResponse>.Ok(response, message);
    }
}
