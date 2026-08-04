using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Skills.Modeling;

public sealed class ReferenceImageInitialMassingSkill
{
    private readonly RhinoLayerManagementService _layerManagementService;
    private readonly GeometryCreationSkill _geometryCreationSkill;

    public ReferenceImageInitialMassingSkill(
        RhinoLayerManagementService layerManagementService,
        GeometryCreationSkill geometryCreationSkill)
    {
        _layerManagementService = layerManagementService;
        _geometryCreationSkill = geometryCreationSkill;
    }

    public OperationResponse<ReferenceImageInitialMassingResponse> Create(
        CreateReferenceImageInitialMassingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return OperationResponse<ReferenceImageInitialMassingResponse>.Fail("FilePath is required.");
        }

        if (request.Decomposition.Parts.Count == 0)
        {
            return OperationResponse<ReferenceImageInitialMassingResponse>.Fail(
                "Decomposition.Parts must contain at least one part.");
        }

        string layerFullPath = ResolveLayerPath(request);
        if (request.CreateLayer)
        {
            OperationResponse<LayerMutationResponse> layerResponse = _layerManagementService.Create(new CreateLayersRequest
            {
                FilePath = request.FilePath,
                Entries = new List<LayerCreationEntryRequest>
                {
                    new() { FullPath = layerFullPath }
                }
            });

            if (!layerResponse.Success || layerResponse.Data is null)
            {
                return OperationResponse<ReferenceImageInitialMassingResponse>.Fail(layerResponse.Message);
            }

            if (layerResponse.Data.FailedCount > 0 && layerResponse.Data.ChangedCount == 0)
            {
                return OperationResponse<ReferenceImageInitialMassingResponse>.Fail(
                    $"Failed to create initial massing layer: {layerFullPath}");
            }
        }

        var created = new List<ReferenceImageCreatedPartObjectResponse>();
        var warnings = new List<ObjectEditWarning>();
        List<ReferenceImagePrimitivePart> massingParts = request.Decomposition.Parts
            .Where(IsMassingPart)
            .ToList();

        foreach (ReferenceImagePrimitivePart part in massingParts)
        {
            OperationResponse<GeneralPrimitiveCreationResponse> response = CreatePart(
                request.FilePath,
                layerFullPath,
                request.Decomposition,
                part,
                request.Common);

            if (!response.Success || response.Data is null)
            {
                return OperationResponse<ReferenceImageInitialMassingResponse>.Fail(
                    $"Initial massing failed for part '{part.PartName}': {response.Message}");
            }

            foreach (GeneralPrimitiveCreatedObjectResponse item in response.Data.CreatedObjects)
            {
                created.Add(new ReferenceImageCreatedPartObjectResponse
                {
                    PartName = part.PartName,
                    ObjectId = item.ObjectId,
                    PrimitiveKind = item.Kind.ToString(),
                    GeometryTypeName = item.GeometryTypeName
                });
            }

            warnings.AddRange(response.Data.Warnings);
        }

        return OperationResponse<ReferenceImageInitialMassingResponse>.Ok(
            new ReferenceImageInitialMassingResponse
            {
                FilePath = request.FilePath,
                LayerFullPath = layerFullPath,
                RequestedPartCount = massingParts.Count,
                CreatedObjectCount = created.Count,
                CreatedObjects = created,
                Warnings = warnings
            },
            "Reference image initial massing completed.");
    }

    private OperationResponse<GeneralPrimitiveCreationResponse> CreatePart(
        string filePath,
        string layerFullPath,
        ReferenceImagePrimitiveDecomposition decomposition,
        ReferenceImagePrimitivePart part,
        GeometryCreationCommonOptions common)
    {
        GeometryCreationCommonOptions partCommon = BuildCommon(layerFullPath, decomposition, part, common);
        ReferenceImagePrimitiveVocabularyKind primitive = part.PreferredPrimitive == ReferenceImagePrimitiveVocabularyKind.Box
            ? ReferenceImagePrimitiveVocabularyKind.RoundedBox
            : part.PreferredPrimitive;

        return primitive switch
        {
            ReferenceImagePrimitiveVocabularyKind.RoundedBox => _geometryCreationSkill.Create(new CreateRoundedBoxesRequest
            {
                FilePath = filePath,
                Items = new List<RoundedBoxItemRequest>
                {
                    new()
                    {
                        CenterX = part.CenterX,
                        CenterY = part.CenterY,
                        CenterZ = part.CenterZ,
                        Width = part.SizeX,
                        Depth = part.SizeY,
                        Height = part.SizeZ,
                        Radius = ResolveRoundedBoxRadius(part),
                        NormalX = ResolveNormal(part).X,
                        NormalY = ResolveNormal(part).Y,
                        NormalZ = ResolveNormal(part).Z,
                        XAxisX = ResolveXAxis(part).X,
                        XAxisY = ResolveXAxis(part).Y,
                        XAxisZ = ResolveXAxis(part).Z,
                        Name = part.PartName
                    }
                },
                Common = partCommon
            }),
            ReferenceImagePrimitiveVocabularyKind.Ellipsoid or ReferenceImagePrimitiveVocabularyKind.Sphere => _geometryCreationSkill.Create(new CreateEllipsoidsRequest
            {
                FilePath = filePath,
                Items = new List<EllipsoidItemRequest>
                {
                    new()
                    {
                        CenterX = part.CenterX,
                        CenterY = part.CenterY,
                        CenterZ = part.CenterZ,
                        RadiusX = part.SizeX * 0.5d,
                        RadiusY = part.SizeY * 0.5d,
                        RadiusZ = part.SizeZ * 0.5d,
                        NormalX = ResolveNormal(part).X,
                        NormalY = ResolveNormal(part).Y,
                        NormalZ = ResolveNormal(part).Z,
                        XAxisX = ResolveXAxis(part).X,
                        XAxisY = ResolveXAxis(part).Y,
                        XAxisZ = ResolveXAxis(part).Z,
                        Name = part.PartName
                    }
                },
                Common = partCommon
            }),
            ReferenceImagePrimitiveVocabularyKind.Cylinder => _geometryCreationSkill.Create(new CreateCylindersRequest
            {
                FilePath = filePath,
                Items = new List<CylinderItemRequest>
                {
                    new()
                    {
                        BaseX = Offset(part.CenterX, part.CenterY, part.CenterZ, ResolveNormal(part), -part.SizeZ * 0.5d).X,
                        BaseY = Offset(part.CenterX, part.CenterY, part.CenterZ, ResolveNormal(part), -part.SizeZ * 0.5d).Y,
                        BaseZ = Offset(part.CenterX, part.CenterY, part.CenterZ, ResolveNormal(part), -part.SizeZ * 0.5d).Z,
                        Radius = Math.Max(part.SizeX, part.SizeY) * 0.5d,
                        Height = part.SizeZ,
                        AxisX = ResolveNormal(part).X,
                        AxisY = ResolveNormal(part).Y,
                        AxisZ = ResolveNormal(part).Z,
                        Name = part.PartName
                    }
                },
                Common = partCommon
            }),
            ReferenceImagePrimitiveVocabularyKind.Cone => _geometryCreationSkill.Create(new CreateConesRequest
            {
                FilePath = filePath,
                Items = new List<ConeItemRequest>
                {
                    new()
                    {
                        BaseX = Offset(part.CenterX, part.CenterY, part.CenterZ, ResolveNormal(part), -part.SizeZ * 0.5d).X,
                        BaseY = Offset(part.CenterX, part.CenterY, part.CenterZ, ResolveNormal(part), -part.SizeZ * 0.5d).Y,
                        BaseZ = Offset(part.CenterX, part.CenterY, part.CenterZ, ResolveNormal(part), -part.SizeZ * 0.5d).Z,
                        Radius = Math.Max(part.SizeX, part.SizeY) * 0.5d,
                        Height = part.SizeZ,
                        AxisX = ResolveNormal(part).X,
                        AxisY = ResolveNormal(part).Y,
                        AxisZ = ResolveNormal(part).Z,
                        Name = part.PartName
                    }
                },
                Common = partCommon
            }),
            ReferenceImagePrimitiveVocabularyKind.Capsule or ReferenceImagePrimitiveVocabularyKind.Pipe => _geometryCreationSkill.Create(new CreateCapsulesRequest
            {
                FilePath = filePath,
                Items = new List<CapsuleItemRequest>
                {
                    new()
                    {
                        StartX = Offset(part.CenterX, part.CenterY, part.CenterZ, ResolveXAxis(part), -part.SizeX * 0.5d).X,
                        StartY = Offset(part.CenterX, part.CenterY, part.CenterZ, ResolveXAxis(part), -part.SizeX * 0.5d).Y,
                        StartZ = Offset(part.CenterX, part.CenterY, part.CenterZ, ResolveXAxis(part), -part.SizeX * 0.5d).Z,
                        EndX = Offset(part.CenterX, part.CenterY, part.CenterZ, ResolveXAxis(part), part.SizeX * 0.5d).X,
                        EndY = Offset(part.CenterX, part.CenterY, part.CenterZ, ResolveXAxis(part), part.SizeX * 0.5d).Y,
                        EndZ = Offset(part.CenterX, part.CenterY, part.CenterZ, ResolveXAxis(part), part.SizeX * 0.5d).Z,
                        Radius = Math.Max(Math.Min(part.SizeY, part.SizeZ) * 0.5d, 0.01d),
                        Name = part.PartName
                    }
                },
                Common = partCommon
            }),
            ReferenceImagePrimitiveVocabularyKind.Torus => _geometryCreationSkill.Create(new CreateToriRequest
            {
                FilePath = filePath,
                Items = new List<TorusItemRequest>
                {
                    new()
                    {
                        CenterX = part.CenterX,
                        CenterY = part.CenterY,
                        CenterZ = part.CenterZ,
                        MajorRadius = Math.Max(part.SizeX, part.SizeY) * 0.35d,
                        MinorRadius = Math.Max(Math.Min(part.SizeX, part.SizeY) * 0.08d, 0.01d),
                        NormalX = ResolveNormal(part).X,
                        NormalY = ResolveNormal(part).Y,
                        NormalZ = ResolveNormal(part).Z,
                        XAxisX = ResolveXAxis(part).X,
                        XAxisY = ResolveXAxis(part).Y,
                        XAxisZ = ResolveXAxis(part).Z,
                        Name = part.PartName
                    }
                },
                Common = partCommon
            }),
            ReferenceImagePrimitiveVocabularyKind.TaperedBox => _geometryCreationSkill.Create(new CreateTaperedBoxesRequest
            {
                FilePath = filePath,
                Items = new List<TaperedBoxItemRequest>
                {
                    BuildTaperedBoxItem(part)
                },
                Common = partCommon
            }),
            _ => OperationResponse<GeneralPrimitiveCreationResponse>.Fail(
                $"Primitive {part.PreferredPrimitive} is not part of initial massing execution.")
        };
    }

    private static GeometryCreationCommonOptions BuildCommon(
        string layerFullPath,
        ReferenceImagePrimitiveDecomposition decomposition,
        ReferenceImagePrimitivePart part,
        GeometryCreationCommonOptions common)
    {
        var userText = new Dictionary<string, string>(common.UserText, StringComparer.OrdinalIgnoreCase)
        {
            ["mcp.capability"] = "reference-image-object-modeling",
            ["mcp.modeling.stage"] = "massing",
            ["mcp.source.image"] = decomposition.ReferenceImageLabel,
            ["mcp.part.name"] = part.PartName,
            ["mcp.part.role"] = part.Role.ToString(),
            ["mcp.primitive.kind"] = part.PreferredPrimitive.ToString()
        };

        return new GeometryCreationCommonOptions
        {
            LayerFullPath = layerFullPath,
            Color = common.Color,
            Name = string.IsNullOrWhiteSpace(common.Name) ? part.PartName : $"{common.Name} {part.PartName}",
            UserText = userText
        };
    }

    private static bool IsMassingPart(ReferenceImagePrimitivePart part)
    {
        return part.Role is ReferenceImagePartRole.PrimaryMass or ReferenceImagePartRole.StructuralMass
            && part.PreferredPrimitive != ReferenceImagePrimitiveVocabularyKind.MaterialOnly;
    }

    private static double ResolveRoundedBoxRadius(ReferenceImagePrimitivePart part)
    {
        double limit = Math.Min(part.SizeX, Math.Min(part.SizeY, part.SizeZ)) * 0.45d;
        double radius = part.EdgeCharacter == ReferenceImageObjectEdgeCharacter.Hard
            ? limit * 0.12d
            : Math.Max(part.Radius, limit * 0.35d);
        return Math.Max(Math.Min(radius, limit), 0.01d);
    }

    private static TaperedBoxItemRequest BuildTaperedBoxItem(ReferenceImagePrimitivePart part)
    {
        (SimpleVector start, SimpleVector end) = ResolveTaperedEndpoints(part);
        SimpleVector up = ResolveNormal(part);
        return new TaperedBoxItemRequest
        {
            StartX = start.X,
            StartY = start.Y,
            StartZ = start.Z,
            EndX = end.X,
            EndY = end.Y,
            EndZ = end.Z,
            StartWidth = part.StartWidth > 0d ? part.StartWidth : part.SizeY,
            StartDepth = part.StartDepth > 0d ? part.StartDepth : part.SizeZ,
            EndWidth = part.EndWidth > 0d ? part.EndWidth : part.SizeY,
            EndDepth = part.EndDepth > 0d ? part.EndDepth : part.SizeZ,
            UpX = up.X,
            UpY = up.Y,
            UpZ = up.Z,
            Name = part.PartName
        };
    }

    private static (SimpleVector Start, SimpleVector End) ResolveTaperedEndpoints(
        ReferenceImagePrimitivePart part)
    {
        if (part.Anchors.Count >= 2)
        {
            ReferenceImageBriefAnchor start = part.Anchors.FirstOrDefault(anchor =>
                    anchor.Name.Contains("start", StringComparison.OrdinalIgnoreCase)
                    || anchor.Name.Contains("bottom", StringComparison.OrdinalIgnoreCase)
                    || anchor.Name.Contains("front", StringComparison.OrdinalIgnoreCase))
                ?? part.Anchors[0];
            ReferenceImageBriefAnchor end = part.Anchors.FirstOrDefault(anchor =>
                    !ReferenceEquals(anchor, start)
                    && (anchor.Name.Contains("end", StringComparison.OrdinalIgnoreCase)
                        || anchor.Name.Contains("top", StringComparison.OrdinalIgnoreCase)
                        || anchor.Name.Contains("rear", StringComparison.OrdinalIgnoreCase)))
                ?? part.Anchors.First(anchor => !ReferenceEquals(anchor, start));

            return (new SimpleVector(start.X, start.Y, start.Z), new SimpleVector(end.X, end.Y, end.Z));
        }

        SimpleVector axis = ResolveXAxis(part);
        double length = Math.Max(part.SizeX, Math.Max(part.SizeY, part.SizeZ));
        return (
            Offset(part.CenterX, part.CenterY, part.CenterZ, axis, -length * 0.5d),
            Offset(part.CenterX, part.CenterY, part.CenterZ, axis, length * 0.5d));
    }

    private static SimpleVector ResolveNormal(ReferenceImagePrimitivePart part)
    {
        return part.HasLocalFrame
            ? Normalize(part.NormalX, part.NormalY, part.NormalZ, new SimpleVector(0d, 0d, 1d))
            : new SimpleVector(0d, 0d, 1d);
    }

    private static SimpleVector ResolveXAxis(ReferenceImagePrimitivePart part)
    {
        return part.HasLocalFrame
            ? Normalize(part.XAxisX, part.XAxisY, part.XAxisZ, new SimpleVector(1d, 0d, 0d))
            : new SimpleVector(1d, 0d, 0d);
    }

    private static SimpleVector Offset(double x, double y, double z, SimpleVector axis, double distance)
    {
        return new SimpleVector(
            x + axis.X * distance,
            y + axis.Y * distance,
            z + axis.Z * distance);
    }

    private static SimpleVector Normalize(
        double x,
        double y,
        double z,
        SimpleVector fallback)
    {
        double length = Math.Sqrt(x * x + y * y + z * z);
        return length <= 1e-12d || double.IsNaN(length) || double.IsInfinity(length)
            ? fallback
            : new SimpleVector(x / length, y / length, z / length);
    }

    private readonly record struct SimpleVector(double X, double Y, double Z);

    private static string ResolveLayerPath(CreateReferenceImageInitialMassingRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.LayerFullPath))
        {
            return request.LayerFullPath.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Common.LayerFullPath))
        {
            return request.Common.LayerFullPath.Trim();
        }

        string suffix = string.IsNullOrWhiteSpace(request.Decomposition.ReferenceImageLabel)
            ? "reference-object"
            : request.Decomposition.ReferenceImageLabel;
        return $"MCP::REFERENCE_IMAGE_OBJECT_MODELING::{suffix}";
    }
}
