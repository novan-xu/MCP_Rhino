extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using BoundingBox = rhinocommon::Rhino.Geometry.BoundingBox;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using ObjectColorSource = rhinocommon::Rhino.DocObjects.ObjectColorSource;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoGeneralPrimitiveCreationService
{
    private const double NearZeroTolerance = 1e-12;
    private const double LargeBBoxDiagonalThreshold = 1e12;

    private readonly ILiveRhinoDocumentAccessor _documentAccessor;
    private readonly ILiveGeneralPrimitiveBuilder _builder;

    public RhinoGeneralPrimitiveCreationService(
        ILiveRhinoDocumentAccessor documentAccessor,
        ILiveGeneralPrimitiveBuilder builder)
    {
        _documentAccessor = documentAccessor;
        _builder = builder;
    }

    public OperationResponse<GeneralPrimitiveCreationResponse> Create(
        string filePath,
        IReadOnlyList<GeneralPrimitiveCreationSpec> specs,
        GeometryObjectAttributesSpec attributes)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return OperationResponse<GeneralPrimitiveCreationResponse>.Fail("FilePath is required.");
        }

        if (specs.Count == 0)
        {
            return OperationResponse<GeneralPrimitiveCreationResponse>.Fail("At least one general primitive item is required.");
        }

        return _documentAccessor.ExecuteWithUndo(
            filePath,
            "MCP: CreateGeneralPrimitives",
            document =>
            {
                OperationResponse<List<PreparedGeneralPrimitive>> prepared = Prepare(document, specs, attributes);
                if (!prepared.Success || prepared.Data is null)
                {
                    return OperationResponse<(bool Mutated, GeneralPrimitiveCreationResponse Result)>.Fail(prepared.Message);
                }

                var createdObjects = new List<GeneralPrimitiveCreatedObjectResponse>(prepared.Data.Count);
                var warnings = new List<ObjectEditWarning>();

                foreach (PreparedGeneralPrimitive item in prepared.Data)
                {
                    Guid objectId = document.Objects.Add(item.Geometry, item.Attributes);
                    if (objectId == Guid.Empty)
                    {
                        return OperationResponse<(bool Mutated, GeneralPrimitiveCreationResponse Result)>.Fail(
                            $"Failed to add {item.Spec.Kind} geometry to the active Rhino document.");
                    }

                    BoundingBox box = item.Geometry.GetBoundingBox(true);
                    createdObjects.Add(new GeneralPrimitiveCreatedObjectResponse
                    {
                        ObjectId = objectId,
                        Kind = item.Spec.Kind,
                        GeometryTypeName = item.Geometry.GetType().Name,
                        LayerFullPath = attributes.LayerFullPath,
                        BoundingBox = box.IsValid ? ToResponse(box) : null
                    });
                    warnings.AddRange(item.Warnings);
                }

                if (createdObjects.Count > 0)
                {
                    document.Views.Redraw();
                }

                var response = new GeneralPrimitiveCreationResponse
                {
                    FilePath = filePath,
                    RequestedCount = specs.Count,
                    CreatedCount = createdObjects.Count,
                    FailedCount = specs.Count - createdObjects.Count,
                    CreatedObjects = createdObjects,
                    Warnings = warnings
                };

                return OperationResponse<(bool Mutated, GeneralPrimitiveCreationResponse Result)>.Ok(
                    (createdObjects.Count > 0, response),
                    "General primitive creation completed.");
            });
    }

    private OperationResponse<List<PreparedGeneralPrimitive>> Prepare(
        rhinocommon::Rhino.RhinoDoc document,
        IReadOnlyList<GeneralPrimitiveCreationSpec> specs,
        GeometryObjectAttributesSpec attributes)
    {
        OperationResponse attributeValidation = ValidateAttributes(attributes);
        if (!attributeValidation.Success)
        {
            return OperationResponse<List<PreparedGeneralPrimitive>>.Fail(attributeValidation.Message);
        }

        int layerIndex = document.Layers.FindByFullPath(attributes.LayerFullPath, -1);
        if (layerIndex < 0)
        {
            return OperationResponse<List<PreparedGeneralPrimitive>>.Fail(
                $"Target layer was not found: {attributes.LayerFullPath}");
        }

        var prepared = new List<PreparedGeneralPrimitive>(specs.Count);
        var batchWarnings = new List<ObjectEditWarning>();
        if (specs.Count > 10000)
        {
            batchWarnings.Add(new ObjectEditWarning
            {
                Code = "LARGE_BATCH",
                Message = "Batch size exceeds 10000 items."
            });
        }

        for (int i = 0; i < specs.Count; i++)
        {
            GeneralPrimitiveCreationSpec spec = specs[i];
            OperationResponse validation = ValidateSpec(spec);
            if (!validation.Success)
            {
                return OperationResponse<List<PreparedGeneralPrimitive>>.Fail(validation.Message);
            }

            OperationResponse<GeometryBase> buildResult = _builder.Build(spec);
            if (!buildResult.Success || buildResult.Data is null)
            {
                return OperationResponse<List<PreparedGeneralPrimitive>>.Fail(buildResult.Message);
            }

            var warnings = new List<ObjectEditWarning>();
            if (i == 0)
            {
                warnings.AddRange(batchWarnings);
            }

            AddLargeBBoxWarning(buildResult.Data, spec.Kind, warnings);
            ObjectAttributes objectAttributes = CreateAttributes(layerIndex, attributes, spec, i, specs.Count);
            prepared.Add(new PreparedGeneralPrimitive(spec, buildResult.Data, objectAttributes, warnings));
        }

        return OperationResponse<List<PreparedGeneralPrimitive>>.Ok(prepared);
    }

    private static OperationResponse ValidateAttributes(GeometryObjectAttributesSpec attributes)
    {
        if (string.IsNullOrWhiteSpace(attributes.LayerFullPath))
        {
            return OperationResponse.Fail("LayerFullPath is required for general primitive creation.");
        }

        if (attributes.Color is not null
            && (attributes.Color.R is < 0 or > 255
                || attributes.Color.G is < 0 or > 255
                || attributes.Color.B is < 0 or > 255))
        {
            return OperationResponse.Fail("Object color values must be in the 0-255 range.");
        }

        foreach ((string key, _) in attributes.UserText)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return OperationResponse.Fail("UserText keys cannot be empty.");
            }
        }

        return OperationResponse.Ok();
    }

    private static OperationResponse ValidateSpec(GeneralPrimitiveCreationSpec spec)
    {
        return spec.Kind switch
        {
            GeneralPrimitiveKind.Circle => ValidateCircle(spec),
            GeneralPrimitiveKind.Ellipse => ValidateEllipse(spec),
            GeneralPrimitiveKind.Polyline => ValidatePolyline(spec),
            GeneralPrimitiveKind.NurbsCurve => ValidateNurbsCurve(spec),
            GeneralPrimitiveKind.Sphere => ValidateSphere(spec),
            GeneralPrimitiveKind.Cone => ValidateCone(spec),
            GeneralPrimitiveKind.Cylinder => ValidateCylinder(spec),
            GeneralPrimitiveKind.Ellipsoid => ValidateEllipsoid(spec),
            GeneralPrimitiveKind.Capsule => ValidateCapsule(spec),
            GeneralPrimitiveKind.Torus => ValidateTorus(spec),
            GeneralPrimitiveKind.RoundedBox => ValidateRoundedBox(spec),
            GeneralPrimitiveKind.RaisedStrip => ValidateRaisedStrip(spec),
            GeneralPrimitiveKind.TaperedBox => ValidateTaperedBox(spec),
            _ => OperationResponse.Fail($"Unsupported general primitive kind: {spec.Kind}")
        };
    }

    private static OperationResponse ValidateCircle(GeneralPrimitiveCreationSpec spec)
    {
        if (!AllFinite(spec.CenterX, spec.CenterY, spec.CenterZ, spec.Radius, spec.NormalX, spec.NormalY, spec.NormalZ))
        {
            return OperationResponse.Fail("Circle center, radius, and normal must be finite numbers.");
        }

        return RequirePositiveRadius(spec.Radius)
            ?? RequireNonZeroVector(spec.NormalX, spec.NormalY, spec.NormalZ, "Circle normal")
            ?? OperationResponse.Ok();
    }

    private static OperationResponse ValidateEllipse(GeneralPrimitiveCreationSpec spec)
    {
        if (!AllFinite(
                spec.CenterX, spec.CenterY, spec.CenterZ,
                spec.RadiusX, spec.RadiusY,
                spec.NormalX, spec.NormalY, spec.NormalZ,
                spec.XAxisX, spec.XAxisY, spec.XAxisZ))
        {
            return OperationResponse.Fail("Ellipse center, radii, normal, and X axis must be finite numbers.");
        }

        OperationResponse? required =
            RequirePositive(spec.RadiusX, "RadiusX")
            ?? RequirePositive(spec.RadiusY, "RadiusY")
            ?? RequireNonZeroVector(spec.NormalX, spec.NormalY, spec.NormalZ, "Ellipse normal")
            ?? RequireNonZeroVector(spec.XAxisX, spec.XAxisY, spec.XAxisZ, "Ellipse X axis");

        if (required is not null)
        {
            return required;
        }

        double crossLength = CrossLength(
            spec.NormalX, spec.NormalY, spec.NormalZ,
            spec.XAxisX, spec.XAxisY, spec.XAxisZ);
        return crossLength <= NearZeroTolerance
            ? OperationResponse.Fail("Ellipse X axis cannot be parallel to the normal vector.")
            : OperationResponse.Ok();
    }

    private static OperationResponse ValidatePolyline(GeneralPrimitiveCreationSpec spec)
    {
        int minimum = spec.Closed ? 3 : 2;
        if (spec.Points.Count < minimum)
        {
            return OperationResponse.Fail(spec.Closed
                ? "Closed polyline requires at least three points."
                : "Polyline requires at least two points.");
        }

        return ValidatePointList(spec.Points, "Polyline points", spec.Closed ? 3 : 2);
    }

    private static OperationResponse ValidateNurbsCurve(GeneralPrimitiveCreationSpec spec)
    {
        if (spec.Degree is < 1 or > 11)
        {
            return OperationResponse.Fail("NurbsCurve degree must be between 1 and 11.");
        }

        if (spec.Points.Count < spec.Degree + 1)
        {
            return OperationResponse.Fail("NurbsCurve requires at least Degree + 1 control points.");
        }

        return ValidatePointList(spec.Points, "NurbsCurve control points", 2);
    }

    private static OperationResponse ValidateSphere(GeneralPrimitiveCreationSpec spec)
    {
        if (!AllFinite(spec.CenterX, spec.CenterY, spec.CenterZ, spec.Radius))
        {
            return OperationResponse.Fail("Sphere center and radius must be finite numbers.");
        }

        return RequirePositiveRadius(spec.Radius) ?? OperationResponse.Ok();
    }

    private static OperationResponse ValidateCone(GeneralPrimitiveCreationSpec spec)
    {
        if (!AllFinite(spec.BaseX, spec.BaseY, spec.BaseZ, spec.Radius, spec.Height, spec.AxisX, spec.AxisY, spec.AxisZ))
        {
            return OperationResponse.Fail("Cone base point, radius, height, and axis must be finite numbers.");
        }

        return RequirePositiveRadius(spec.Radius)
            ?? RequirePositive(spec.Height, "Height")
            ?? RequireNonZeroVector(spec.AxisX, spec.AxisY, spec.AxisZ, "Cone axis")
            ?? OperationResponse.Ok();
    }

    private static OperationResponse ValidateCylinder(GeneralPrimitiveCreationSpec spec)
    {
        if (!AllFinite(spec.BaseX, spec.BaseY, spec.BaseZ, spec.Radius, spec.Height, spec.AxisX, spec.AxisY, spec.AxisZ))
        {
            return OperationResponse.Fail("Cylinder base point, radius, height, and axis must be finite numbers.");
        }

        return RequirePositiveRadius(spec.Radius)
            ?? RequirePositive(spec.Height, "Height")
            ?? RequireNonZeroVector(spec.AxisX, spec.AxisY, spec.AxisZ, "Cylinder axis")
            ?? OperationResponse.Ok();
    }

    private static OperationResponse ValidateEllipsoid(GeneralPrimitiveCreationSpec spec)
    {
        if (!AllFinite(
                spec.CenterX, spec.CenterY, spec.CenterZ,
                spec.RadiusX, spec.RadiusY, spec.RadiusZ,
                spec.NormalX, spec.NormalY, spec.NormalZ,
                spec.XAxisX, spec.XAxisY, spec.XAxisZ))
        {
            return OperationResponse.Fail("Ellipsoid center, radii, normal, and X axis must be finite numbers.");
        }

        OperationResponse? required =
            RequirePositive(spec.RadiusX, "RadiusX")
            ?? RequirePositive(spec.RadiusY, "RadiusY")
            ?? RequirePositive(spec.RadiusZ, "RadiusZ")
            ?? RequireNonZeroVector(spec.NormalX, spec.NormalY, spec.NormalZ, "Ellipsoid normal")
            ?? RequireNonZeroVector(spec.XAxisX, spec.XAxisY, spec.XAxisZ, "Ellipsoid X axis");

        if (required is not null)
        {
            return required;
        }

        double crossLength = CrossLength(
            spec.NormalX, spec.NormalY, spec.NormalZ,
            spec.XAxisX, spec.XAxisY, spec.XAxisZ);
        return crossLength <= NearZeroTolerance
            ? OperationResponse.Fail("Ellipsoid X axis cannot be parallel to the normal vector.")
            : OperationResponse.Ok();
    }

    private static OperationResponse ValidateCapsule(GeneralPrimitiveCreationSpec spec)
    {
        if (!AllFinite(
                spec.BaseX, spec.BaseY, spec.BaseZ,
                spec.EndX, spec.EndY, spec.EndZ,
                spec.Radius, spec.Tolerance))
        {
            return OperationResponse.Fail("Capsule start, end, radius, and tolerance must be finite numbers.");
        }

        return RequirePositiveRadius(spec.Radius)
            ?? RequirePositive(spec.Tolerance, "Tolerance")
            ?? RequireDistinctPoints(spec.BaseX, spec.BaseY, spec.BaseZ, spec.EndX, spec.EndY, spec.EndZ, "Capsule start and end")
            ?? OperationResponse.Ok();
    }

    private static OperationResponse ValidateTorus(GeneralPrimitiveCreationSpec spec)
    {
        if (!AllFinite(
                spec.CenterX, spec.CenterY, spec.CenterZ,
                spec.MajorRadius, spec.MinorRadius,
                spec.NormalX, spec.NormalY, spec.NormalZ,
                spec.XAxisX, spec.XAxisY, spec.XAxisZ))
        {
            return OperationResponse.Fail("Torus center, radii, normal, and X axis must be finite numbers.");
        }

        OperationResponse? required =
            RequirePositive(spec.MajorRadius, "MajorRadius")
            ?? RequirePositive(spec.MinorRadius, "MinorRadius")
            ?? RequireNonZeroVector(spec.NormalX, spec.NormalY, spec.NormalZ, "Torus normal")
            ?? RequireNonZeroVector(spec.XAxisX, spec.XAxisY, spec.XAxisZ, "Torus X axis");

        if (required is not null)
        {
            return required;
        }

        if (spec.MinorRadius >= spec.MajorRadius)
        {
            return OperationResponse.Fail("Torus MinorRadius must be smaller than MajorRadius.");
        }

        double crossLength = CrossLength(
            spec.NormalX, spec.NormalY, spec.NormalZ,
            spec.XAxisX, spec.XAxisY, spec.XAxisZ);
        return crossLength <= NearZeroTolerance
            ? OperationResponse.Fail("Torus X axis cannot be parallel to the normal vector.")
            : OperationResponse.Ok();
    }

    private static OperationResponse ValidateRoundedBox(GeneralPrimitiveCreationSpec spec)
    {
        if (!AllFinite(
                spec.CenterX, spec.CenterY, spec.CenterZ,
                spec.Width, spec.Depth, spec.Height, spec.Radius,
                spec.NormalX, spec.NormalY, spec.NormalZ,
                spec.XAxisX, spec.XAxisY, spec.XAxisZ,
                spec.Tolerance))
        {
            return OperationResponse.Fail("RoundedBox center, dimensions, radius, axes, and tolerance must be finite numbers.");
        }

        OperationResponse? required =
            RequirePositive(spec.Width, "Width")
            ?? RequirePositive(spec.Depth, "Depth")
            ?? RequirePositive(spec.Height, "Height")
            ?? RequirePositiveRadius(spec.Radius)
            ?? RequirePositive(spec.Tolerance, "Tolerance")
            ?? RequireNonZeroVector(spec.NormalX, spec.NormalY, spec.NormalZ, "RoundedBox normal")
            ?? RequireNonZeroVector(spec.XAxisX, spec.XAxisY, spec.XAxisZ, "RoundedBox X axis");

        if (required is not null)
        {
            return required;
        }

        double smallestHalfDimension = Math.Min(spec.Width, Math.Min(spec.Depth, spec.Height)) * 0.5d;
        if (spec.Radius >= smallestHalfDimension)
        {
            return OperationResponse.Fail("RoundedBox radius must be smaller than half of the smallest dimension.");
        }

        double crossLength = CrossLength(
            spec.NormalX, spec.NormalY, spec.NormalZ,
            spec.XAxisX, spec.XAxisY, spec.XAxisZ);
        return crossLength <= NearZeroTolerance
            ? OperationResponse.Fail("RoundedBox X axis cannot be parallel to the normal vector.")
            : OperationResponse.Ok();
    }

    private static OperationResponse ValidateRaisedStrip(GeneralPrimitiveCreationSpec spec)
    {
        if (!AllFinite(
                spec.BaseX, spec.BaseY, spec.BaseZ,
                spec.EndX, spec.EndY, spec.EndZ,
                spec.Width, spec.Height,
                spec.UpX, spec.UpY, spec.UpZ))
        {
            return OperationResponse.Fail("RaisedStrip start, end, width, height, and up vector must be finite numbers.");
        }

        OperationResponse? required =
            RequirePositive(spec.Width, "Width")
            ?? RequirePositive(spec.Height, "Height")
            ?? RequireDistinctPoints(spec.BaseX, spec.BaseY, spec.BaseZ, spec.EndX, spec.EndY, spec.EndZ, "RaisedStrip start and end")
            ?? RequireNonZeroVector(spec.UpX, spec.UpY, spec.UpZ, "RaisedStrip up");

        if (required is not null)
        {
            return required;
        }

        double lineUpCrossLength = CrossLength(
            spec.EndX - spec.BaseX,
            spec.EndY - spec.BaseY,
            spec.EndZ - spec.BaseZ,
            spec.UpX,
            spec.UpY,
            spec.UpZ);
        return lineUpCrossLength <= NearZeroTolerance
            ? OperationResponse.Fail("RaisedStrip up vector cannot be parallel to its start-end direction.")
            : OperationResponse.Ok();
    }

    private static OperationResponse ValidateTaperedBox(GeneralPrimitiveCreationSpec spec)
    {
        if (!AllFinite(
                spec.BaseX, spec.BaseY, spec.BaseZ,
                spec.EndX, spec.EndY, spec.EndZ,
                spec.StartWidth, spec.StartDepth, spec.EndWidth, spec.EndDepth,
                spec.UpX, spec.UpY, spec.UpZ))
        {
            return OperationResponse.Fail("TaperedBox start, end, section sizes, and up vector must be finite numbers.");
        }

        OperationResponse? required =
            RequirePositive(spec.StartWidth, "StartWidth")
            ?? RequirePositive(spec.StartDepth, "StartDepth")
            ?? RequirePositive(spec.EndWidth, "EndWidth")
            ?? RequirePositive(spec.EndDepth, "EndDepth")
            ?? RequireDistinctPoints(spec.BaseX, spec.BaseY, spec.BaseZ, spec.EndX, spec.EndY, spec.EndZ, "TaperedBox start and end")
            ?? RequireNonZeroVector(spec.UpX, spec.UpY, spec.UpZ, "TaperedBox up");

        if (required is not null)
        {
            return required;
        }

        double axisUpCrossLength = CrossLength(
            spec.EndX - spec.BaseX,
            spec.EndY - spec.BaseY,
            spec.EndZ - spec.BaseZ,
            spec.UpX,
            spec.UpY,
            spec.UpZ);
        return axisUpCrossLength <= NearZeroTolerance
            ? OperationResponse.Fail("TaperedBox up vector cannot be parallel to its start-end direction.")
            : OperationResponse.Ok();
    }

    private static OperationResponse ValidatePointList(
        IReadOnlyList<GeneralPrimitivePointSpec> points,
        string label,
        int minimumDistinctCount)
    {
        bool hasFinitePoints = points.All(point => AllFinite(point.X, point.Y, point.Z));
        if (!hasFinitePoints)
        {
            return OperationResponse.Fail($"{label} must all be finite numbers.");
        }

        if (CountDistinct(points) < minimumDistinctCount)
        {
            return OperationResponse.Fail($"{label} must contain at least {minimumDistinctCount} distinct points.");
        }

        return OperationResponse.Ok();
    }

    private static int CountDistinct(IReadOnlyList<GeneralPrimitivePointSpec> points)
    {
        var distinct = new List<GeneralPrimitivePointSpec>();
        foreach (GeneralPrimitivePointSpec point in points)
        {
            if (!distinct.Any(existing => Distance(existing, point) <= NearZeroTolerance))
            {
                distinct.Add(point);
            }
        }

        return distinct.Count;
    }

    private static double Distance(GeneralPrimitivePointSpec left, GeneralPrimitivePointSpec right)
    {
        double dx = left.X - right.X;
        double dy = left.Y - right.Y;
        double dz = left.Z - right.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static OperationResponse? RequirePositiveRadius(double radius)
    {
        return RequirePositive(radius, "Radius");
    }

    private static OperationResponse? RequirePositive(double value, string fieldName)
    {
        return value > NearZeroTolerance ? null : OperationResponse.Fail($"{fieldName} must be greater than zero.");
    }

    private static OperationResponse? RequireNonZeroVector(double x, double y, double z, string fieldName)
    {
        return IsZeroVector(x, y, z) ? OperationResponse.Fail($"{fieldName} vector cannot be zero.") : null;
    }

    private static OperationResponse? RequireDistinctPoints(
        double x0,
        double y0,
        double z0,
        double x1,
        double y1,
        double z1,
        string fieldName)
    {
        double dx = x1 - x0;
        double dy = y1 - y0;
        double dz = z1 - z0;
        double distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        return distance <= NearZeroTolerance ? OperationResponse.Fail($"{fieldName} points must be distinct.") : null;
    }

    private static ObjectAttributes CreateAttributes(
        int layerIndex,
        GeometryObjectAttributesSpec attributesSpec,
        GeneralPrimitiveCreationSpec spec,
        int index,
        int count)
    {
        var attributes = new ObjectAttributes
        {
            LayerIndex = layerIndex,
            Name = ResolveName(attributesSpec.Name, spec.Name, index, count)
        };

        if (attributesSpec.Color is not null)
        {
            attributes.ObjectColor = attributesSpec.Color.ToColor();
            attributes.ColorSource = ObjectColorSource.ColorFromObject;
        }

        foreach ((string key, string value) in attributesSpec.UserText)
        {
            attributes.SetUserString(key, value);
        }

        return attributes;
    }

    private static string ResolveName(string commonName, string itemName, int index, int count)
    {
        if (!string.IsNullOrWhiteSpace(itemName))
        {
            return itemName.Trim();
        }

        if (string.IsNullOrWhiteSpace(commonName))
        {
            return string.Empty;
        }

        string name = commonName.Trim();
        return count == 1 ? name : $"{name} {index + 1:D3}";
    }

    private static void AddLargeBBoxWarning(
        GeometryBase geometry,
        GeneralPrimitiveKind kind,
        ICollection<ObjectEditWarning> warnings)
    {
        BoundingBox boundingBox = geometry.GetBoundingBox(true);
        if (boundingBox.IsValid && boundingBox.Min.DistanceTo(boundingBox.Max) > LargeBBoxDiagonalThreshold)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "LARGE_BBOX",
                Message = $"Geometry bounding box diagonal exceeds 1e12 for {kind}."
            });
        }
    }

    private static GeneralPrimitiveBoundingBoxResponse ToResponse(BoundingBox box)
    {
        return new GeneralPrimitiveBoundingBoxResponse
        {
            MinX = box.Min.X,
            MinY = box.Min.Y,
            MinZ = box.Min.Z,
            MaxX = box.Max.X,
            MaxY = box.Max.Y,
            MaxZ = box.Max.Z
        };
    }

    private static bool AllFinite(params double[] values)
    {
        return values.All(IsFinite);
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static bool IsZeroVector(double x, double y, double z)
    {
        return Math.Abs(x) <= NearZeroTolerance
            && Math.Abs(y) <= NearZeroTolerance
            && Math.Abs(z) <= NearZeroTolerance;
    }

    private static double CrossLength(
        double ax,
        double ay,
        double az,
        double bx,
        double by,
        double bz)
    {
        double x = ay * bz - az * by;
        double y = az * bx - ax * bz;
        double z = ax * by - ay * bx;
        return Math.Sqrt(x * x + y * y + z * z);
    }

    private sealed class PreparedGeneralPrimitive
    {
        public PreparedGeneralPrimitive(
            GeneralPrimitiveCreationSpec spec,
            GeometryBase geometry,
            ObjectAttributes attributes,
            IReadOnlyList<ObjectEditWarning> warnings)
        {
            Spec = spec;
            Geometry = geometry;
            Attributes = attributes;
            Warnings = warnings;
        }

        public GeneralPrimitiveCreationSpec Spec { get; }
        public GeometryBase Geometry { get; }
        public ObjectAttributes Attributes { get; }
        public IReadOnlyList<ObjectEditWarning> Warnings { get; }
    }
}
