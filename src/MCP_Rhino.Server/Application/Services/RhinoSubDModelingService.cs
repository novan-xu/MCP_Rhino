using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoSubDModelingService
{
    private const int MaxVertices = 2000;
    private const int MaxFaces = 4000;
    private const double NearZeroTolerance = 1e-12;

    private readonly ILiveSubDModelingOperator _operator;

    public RhinoSubDModelingService(ILiveSubDModelingOperator subDOperator)
    {
        _operator = subDOperator;
    }

    public OperationResponse<SubDCagePreviewResponse> PreviewCage(PreviewSubDCageRequest request)
    {
        return Preview(MapCage(string.Empty, request.Vertices, request.Faces));
    }

    public OperationResponse<SubDCreationResponse> CreateCage(CreateSubDCageRequest request)
    {
        OperationResponse validation = ValidateCreateRequest(request.FilePath, request.Items.Count, "CreateSubDCage");
        return validation.Success
            ? _operator.Create(
                request.FilePath,
                request.Items.Select(item => MapCage(item.Name, item.Vertices, item.Faces)).ToList(),
                MapAttributes(request.Common),
                SubDModelingOperationKind.ExplicitCage)
            : OperationResponse<SubDCreationResponse>.Fail(validation.Message);
    }

    public OperationResponse<SubDCreationResponse> CreateBox(CreateSubDBoxRequest request)
    {
        OperationResponse validation = ValidateCreateRequest(request.FilePath, request.Items.Count, "CreateSubDBox");
        return validation.Success
            ? _operator.Create(
                request.FilePath,
                request.Items.Select(BuildBoxCage).ToList(),
                MapAttributes(request.Common),
                SubDModelingOperationKind.Box)
            : OperationResponse<SubDCreationResponse>.Fail(validation.Message);
    }

    public OperationResponse<SubDCreationResponse> CreateCushions(CreateSubDCushionsRequest request)
    {
        OperationResponse validation = ValidateCreateRequest(request.FilePath, request.Items.Count, "CreateSubDCushions");
        return validation.Success
            ? _operator.Create(
                request.FilePath,
                request.Items.Select(BuildCushionCage).ToList(),
                MapAttributes(request.Common),
                SubDModelingOperationKind.Cushion)
            : OperationResponse<SubDCreationResponse>.Fail(validation.Message);
    }

    public OperationResponse<SubDInspectionResponse> Inspect(InspectSubDObjectsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return OperationResponse<SubDInspectionResponse>.Fail("FilePath is required.");
        }

        if (request.ObjectIds.Count == 0)
        {
            return OperationResponse<SubDInspectionResponse>.Fail("At least one SubD object id is required.");
        }

        return _operator.Inspect(request.FilePath, request.ObjectIds);
    }

    public static OperationResponse<SubDCagePreviewResponse> Preview(SubDCageSpec cage)
    {
        OperationResponse validation = ValidateCage(cage);
        if (!validation.Success)
        {
            return OperationResponse<SubDCagePreviewResponse>.Fail(validation.Message);
        }

        Dictionary<(int A, int B), int> edgeUse = CountEdges(cage);
        int boundaryEdges = edgeUse.Count(item => item.Value == 1);
        int nonManifoldEdges = edgeUse.Count(item => item.Value > 2);
        var warnings = new List<ObjectEditWarning>();
        if (boundaryEdges > 0)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "SUBD_BOUNDARY_EDGES",
                Message = $"Cage has {boundaryEdges} boundary edges; open SubD surfaces may be expected."
            });
        }

        if (nonManifoldEdges > 0)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "SUBD_NON_MANIFOLD_EDGES",
                Message = $"Cage has {nonManifoldEdges} edges used by more than two faces."
            });
        }

        return OperationResponse<SubDCagePreviewResponse>.Ok(new SubDCagePreviewResponse
        {
            VertexCount = cage.Vertices.Count,
            FaceCount = cage.Faces.Count,
            BoundaryEdgeCount = boundaryEdges,
            NonManifoldEdgeCount = nonManifoldEdges,
            BoundingBox = ToResponseBoundingBox(cage),
            Warnings = warnings
        }, "SubD cage preview completed.");
    }

    public static OperationResponse ValidateCage(SubDCageSpec cage)
    {
        if (cage.Vertices.Count is < 4 or > MaxVertices)
        {
            return OperationResponse.Fail($"SubD cage vertex count must be between 4 and {MaxVertices}.");
        }

        if (cage.Faces.Count is < 1 or > MaxFaces)
        {
            return OperationResponse.Fail($"SubD cage face count must be between 1 and {MaxFaces}.");
        }

        for (int i = 0; i < cage.Vertices.Count; i++)
        {
            SubDPointSpec vertex = cage.Vertices[i];
            if (!AllFinite(vertex.X, vertex.Y, vertex.Z))
            {
                return OperationResponse.Fail($"SubD cage vertex {i} must contain finite coordinates.");
            }
        }

        for (int i = 0; i < cage.Faces.Count; i++)
        {
            IReadOnlyList<int> indices = cage.Faces[i].VertexIndices;
            if (indices.Count is < 3 or > 4)
            {
                return OperationResponse.Fail($"SubD cage face {i} must have 3 or 4 vertex indices.");
            }

            if (indices.Distinct().Count() != indices.Count)
            {
                return OperationResponse.Fail($"SubD cage face {i} cannot repeat vertex indices.");
            }

            foreach (int index in indices)
            {
                if (index < 0 || index >= cage.Vertices.Count)
                {
                    return OperationResponse.Fail($"SubD cage face {i} references out-of-range vertex index {index}.");
                }
            }
        }

        return OperationResponse.Ok();
    }

    private static OperationResponse ValidateCreateRequest(string filePath, int itemCount, string operation)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return OperationResponse.Fail("FilePath is required.");
        }

        return itemCount == 0
            ? OperationResponse.Fail($"At least one {operation} item is required.")
            : OperationResponse.Ok();
    }

    private static SubDCageSpec MapCage(
        string name,
        IReadOnlyList<SubDVertexRequest> vertices,
        IReadOnlyList<SubDFaceRequest> faces)
    {
        return new SubDCageSpec
        {
            Name = name,
            Vertices = vertices.Select(vertex => new SubDPointSpec
            {
                X = vertex.X,
                Y = vertex.Y,
                Z = vertex.Z
            }).ToList(),
            Faces = faces.Select(face => new SubDFaceSpec
            {
                VertexIndices = face.VertexIndices.ToList()
            }).ToList()
        };
    }

    private static SubDCageSpec BuildBoxCage(SubDBoxItemRequest item)
    {
        SimpleFrame frame = BuildFrame(
            item.CenterX,
            item.CenterY,
            item.CenterZ,
            item.NormalX,
            item.NormalY,
            item.NormalZ,
            item.XAxisX,
            item.XAxisY,
            item.XAxisZ);
        double width = PositiveOrDefault(item.Width, 1d);
        double depth = PositiveOrDefault(item.Depth, 1d);
        double height = PositiveOrDefault(item.Height, 1d);
        var local = new[]
        {
            new SimpleVector(-width * 0.5d, -depth * 0.5d, -height * 0.5d),
            new SimpleVector(width * 0.5d, -depth * 0.5d, -height * 0.5d),
            new SimpleVector(width * 0.5d, depth * 0.5d, -height * 0.5d),
            new SimpleVector(-width * 0.5d, depth * 0.5d, -height * 0.5d),
            new SimpleVector(-width * 0.5d, -depth * 0.5d, height * 0.5d),
            new SimpleVector(width * 0.5d, -depth * 0.5d, height * 0.5d),
            new SimpleVector(width * 0.5d, depth * 0.5d, height * 0.5d),
            new SimpleVector(-width * 0.5d, depth * 0.5d, height * 0.5d)
        };

        return new SubDCageSpec
        {
            Name = item.Name,
            Vertices = local.Select(frame.ToWorldPoint).ToList(),
            Faces = BoxFaces()
        };
    }

    private static SubDCageSpec BuildCushionCage(SubDCushionItemRequest item)
    {
        SimpleFrame frame = BuildFrame(
            item.CenterX,
            item.CenterY,
            item.CenterZ,
            item.NormalX,
            item.NormalY,
            item.NormalZ,
            item.XAxisX,
            item.XAxisY,
            item.XAxisZ);

        double width = PositiveOrDefault(item.Width, 1d);
        double depth = PositiveOrDefault(item.Depth, 1d);
        double height = PositiveOrDefault(item.Height, 0.25d);
        double crown = Math.Clamp(item.Crown, 0d, height);
        double bulge = Math.Clamp(item.SideBulge, 0d, Math.Min(width, depth) * 0.2d);
        double edgeCompression = Math.Clamp(item.EdgeCompression, 0d, height * 0.4d);

        var vertices = new List<SubDPointSpec>();
        foreach (double zSign in new[] { -1d, 1d })
        {
            for (int yIndex = 0; yIndex < 3; yIndex++)
            {
                double yFactor = yIndex - 1d;
                for (int xIndex = 0; xIndex < 3; xIndex++)
                {
                    double xFactor = xIndex - 1d;
                    double edgeFactor = Math.Max(Math.Abs(xFactor), Math.Abs(yFactor));
                    double z = zSign * height * 0.5d;
                    if (zSign > 0d)
                    {
                        z += crown * (1d - edgeFactor);
                        z -= edgeCompression * edgeFactor;
                    }

                    double outward = edgeFactor > 0d ? bulge : 0d;
                    double x = xFactor * (width * 0.5d + outward);
                    double y = yFactor * (depth * 0.5d + outward);
                    vertices.Add(frame.ToWorldPoint(new SimpleVector(x, y, z)));
                }
            }
        }

        var faces = new List<SubDFaceSpec>();
        AddGridFaces(faces, 0, reverse: true);
        AddGridFaces(faces, 9, reverse: false);
        AddSideFaces(faces);

        return new SubDCageSpec
        {
            Name = item.Name,
            Vertices = vertices,
            Faces = faces
        };
    }

    private static List<SubDFaceSpec> BoxFaces()
    {
        return new List<SubDFaceSpec>
        {
            Face(0, 3, 2, 1),
            Face(4, 5, 6, 7),
            Face(0, 1, 5, 4),
            Face(1, 2, 6, 5),
            Face(2, 3, 7, 6),
            Face(3, 0, 4, 7)
        };
    }

    private static void AddGridFaces(List<SubDFaceSpec> faces, int offset, bool reverse)
    {
        for (int y = 0; y < 2; y++)
        {
            for (int x = 0; x < 2; x++)
            {
                int a = offset + y * 3 + x;
                int b = a + 1;
                int c = a + 4;
                int d = a + 3;
                faces.Add(reverse ? Face(a, d, c, b) : Face(a, b, c, d));
            }
        }
    }

    private static void AddSideFaces(List<SubDFaceSpec> faces)
    {
        int[] bottomLoop = { 0, 1, 2, 5, 8, 7, 6, 3 };
        int[] topLoop = bottomLoop.Select(index => index + 9).ToArray();
        for (int i = 0; i < bottomLoop.Length; i++)
        {
            int next = (i + 1) % bottomLoop.Length;
            faces.Add(Face(bottomLoop[i], bottomLoop[next], topLoop[next], topLoop[i]));
        }
    }

    private static SubDFaceSpec Face(params int[] indices)
    {
        return new SubDFaceSpec { VertexIndices = indices };
    }

    private static Dictionary<(int A, int B), int> CountEdges(SubDCageSpec cage)
    {
        var edgeUse = new Dictionary<(int A, int B), int>();
        foreach (SubDFaceSpec face in cage.Faces)
        {
            IReadOnlyList<int> indices = face.VertexIndices;
            for (int i = 0; i < indices.Count; i++)
            {
                int a = indices[i];
                int b = indices[(i + 1) % indices.Count];
                (int A, int B) key = a < b ? (a, b) : (b, a);
                edgeUse[key] = edgeUse.TryGetValue(key, out int count) ? count + 1 : 1;
            }
        }

        return edgeUse;
    }

    private static GeneralPrimitiveBoundingBoxResponse ToResponseBoundingBox(SubDCageSpec cage)
    {
        return new GeneralPrimitiveBoundingBoxResponse
        {
            MinX = cage.Vertices.Min(vertex => vertex.X),
            MinY = cage.Vertices.Min(vertex => vertex.Y),
            MinZ = cage.Vertices.Min(vertex => vertex.Z),
            MaxX = cage.Vertices.Max(vertex => vertex.X),
            MaxY = cage.Vertices.Max(vertex => vertex.Y),
            MaxZ = cage.Vertices.Max(vertex => vertex.Z)
        };
    }

    private static GeometryObjectAttributesSpec MapAttributes(GeometryCreationCommonOptions? options)
    {
        GeometryCreationCommonOptions common = options ?? new GeometryCreationCommonOptions();
        return new GeometryObjectAttributesSpec
        {
            LayerFullPath = common.LayerFullPath,
            Color = common.Color is null
                ? null
                : new RhinoDisplayColor
                {
                    R = common.Color.R,
                    G = common.Color.G,
                    B = common.Color.B
                },
            Name = common.Name,
            UserText = new Dictionary<string, string>(common.UserText, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static SimpleFrame BuildFrame(
        double centerX,
        double centerY,
        double centerZ,
        double normalX,
        double normalY,
        double normalZ,
        double xAxisX,
        double xAxisY,
        double xAxisZ)
    {
        SimpleVector zAxis = Normalize(normalX, normalY, normalZ, new SimpleVector(0d, 0d, 1d));
        SimpleVector xAxis = Normalize(xAxisX, xAxisY, xAxisZ, new SimpleVector(1d, 0d, 0d));
        SimpleVector yAxis = Normalize(Cross(zAxis, xAxis), new SimpleVector(0d, 1d, 0d));
        xAxis = Normalize(Cross(yAxis, zAxis), new SimpleVector(1d, 0d, 0d));
        return new SimpleFrame(new SimpleVector(centerX, centerY, centerZ), xAxis, yAxis, zAxis);
    }

    private static SimpleVector Normalize(SimpleVector vector, SimpleVector fallback)
    {
        return Normalize(vector.X, vector.Y, vector.Z, fallback);
    }

    private static SimpleVector Normalize(double x, double y, double z, SimpleVector fallback)
    {
        double length = Math.Sqrt(x * x + y * y + z * z);
        return length <= NearZeroTolerance || double.IsNaN(length) || double.IsInfinity(length)
            ? fallback
            : new SimpleVector(x / length, y / length, z / length);
    }

    private static SimpleVector Cross(SimpleVector left, SimpleVector right)
    {
        return new SimpleVector(
            left.Y * right.Z - left.Z * right.Y,
            left.Z * right.X - left.X * right.Z,
            left.X * right.Y - left.Y * right.X);
    }

    private static double PositiveOrDefault(double value, double fallback)
    {
        return value > NearZeroTolerance && !double.IsNaN(value) && !double.IsInfinity(value)
            ? value
            : fallback;
    }

    private static bool AllFinite(params double[] values)
    {
        return values.All(value => !double.IsNaN(value) && !double.IsInfinity(value));
    }

    private readonly record struct SimpleVector(double X, double Y, double Z);

    private readonly record struct SimpleFrame(
        SimpleVector Origin,
        SimpleVector XAxis,
        SimpleVector YAxis,
        SimpleVector ZAxis)
    {
        public SubDPointSpec ToWorldPoint(SimpleVector local)
        {
            return new SubDPointSpec
            {
                X = Origin.X + XAxis.X * local.X + YAxis.X * local.Y + ZAxis.X * local.Z,
                Y = Origin.Y + XAxis.Y * local.X + YAxis.Y * local.Y + ZAxis.Y * local.Z,
                Z = Origin.Z + XAxis.Z * local.X + YAxis.Z * local.Y + ZAxis.Z * local.Z
            };
        }
    }
}
