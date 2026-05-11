extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using Arc = rhinocommon::Rhino.Geometry.Arc;
using ArcCurve = rhinocommon::Rhino.Geometry.ArcCurve;
using BoundingBox = rhinocommon::Rhino.Geometry.BoundingBox;
using Circle = rhinocommon::Rhino.Geometry.Circle;
using Interval = rhinocommon::Rhino.Geometry.Interval;
using Line = rhinocommon::Rhino.Geometry.Line;
using LineCurve = rhinocommon::Rhino.Geometry.LineCurve;
using NurbsCurve = rhinocommon::Rhino.Geometry.NurbsCurve;
using NurbsSurface = rhinocommon::Rhino.Geometry.NurbsSurface;
using Plane = rhinocommon::Rhino.Geometry.Plane;
using PlaneSurface = rhinocommon::Rhino.Geometry.PlaneSurface;
using Point = rhinocommon::Rhino.Geometry.Point;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using Vector3d = rhinocommon::Rhino.Geometry.Vector3d;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveRhinoGeometryValidator : ILiveGeometryValidator
{
    private const double NearZeroTolerance = 1e-12;
    private const double LargeBBoxDiagonalThreshold = 1e12;

    public OperationResponse<IReadOnlyList<ObjectEditWarning>> Validate(
        GeometryCreationSpec spec,
        GeometryObjectAttributesSpec attributes)
    {
        if (string.IsNullOrWhiteSpace(attributes.LayerFullPath))
        {
            return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("Geometry creation requires LayerFullPath.");
        }

        OperationResponse<IReadOnlyList<ObjectEditWarning>> attributeValidation = ValidateAttributes(attributes);
        if (!attributeValidation.Success)
        {
            return attributeValidation;
        }

        OperationResponse<IReadOnlyList<ObjectEditWarning>> geometryValidation = ValidateGeometrySpec(spec);
        if (!geometryValidation.Success)
        {
            return geometryValidation;
        }

        return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Ok(
            attributeValidation.Data!.Concat(geometryValidation.Data!).ToList());
    }

    public OperationResponse<IReadOnlyList<ObjectEditWarning>> Validate(
        GeometryTransformSpec spec,
        IReadOnlyList<RhinoObjectInfo> targets)
    {
        if (!targets.Any())
        {
            return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Ok(Array.Empty<ObjectEditWarning>());
        }

        switch (spec.Kind)
        {
            case GeometryTransformKind.Translate:
                if (!AllFinite(spec.VectorX, spec.VectorY, spec.VectorZ))
                {
                    return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("Translate requires finite VectorX, VectorY, and VectorZ values.");
                }
                break;

            case GeometryTransformKind.Rotate:
                if (!AllFinite(
                        spec.CenterX, spec.CenterY, spec.CenterZ,
                        spec.AxisX, spec.AxisY, spec.AxisZ,
                        spec.AngleRadians))
                {
                    return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("Rotate requires finite center point, axis vector, and angle values.");
                }

                if (IsZeroVector(spec.AxisX, spec.AxisY, spec.AxisZ))
                {
                    return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("Rotate axis vector cannot be zero.");
                }
                break;

            case GeometryTransformKind.UniformScale:
                if (!AllFinite(spec.CenterX, spec.CenterY, spec.CenterZ, spec.ScaleFactor))
                {
                    return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("UniformScale requires finite center point and scale factor values.");
                }

                if (Math.Abs(spec.ScaleFactor) <= NearZeroTolerance)
                {
                    return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("Scale factor cannot be near zero.");
                }
                break;

            default:
                return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail($"Unsupported transform kind: {spec.Kind}.");
        }

        return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Ok(Array.Empty<ObjectEditWarning>());
    }

    public OperationResponse<IReadOnlyList<ObjectEditWarning>> Validate(
        GeometryReplacementSpec spec,
        RhinoObjectInfo target)
    {
        if (spec.ObjectId == Guid.Empty)
        {
            return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("ReplaceGeometry entry ObjectId cannot be empty.");
        }

        OperationResponse<IReadOnlyList<ObjectEditWarning>> geometryValidation = ValidateGeometrySpec(spec.Geometry);
        if (!geometryValidation.Success)
        {
            return geometryValidation;
        }

        bool compatible = target.NormalizedObjectType switch
        {
            RhinoObjectType.Point => spec.Geometry.Primitive == GeometryPrimitiveKind.Point,
            RhinoObjectType.Curve => spec.Geometry.Primitive is GeometryPrimitiveKind.Line or GeometryPrimitiveKind.Arc,
            RhinoObjectType.Surface => spec.Geometry.Primitive == GeometryPrimitiveKind.Surface,
            _ => false
        };

        if (!compatible)
        {
            return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail(
                $"Target object [{target.ObjectId}] type [{target.NormalizedObjectType}] is incompatible with new geometry [{spec.Geometry.Primitive}].");
        }

        return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Ok(geometryValidation.Data!);
    }

    public OperationResponse<IReadOnlyList<ObjectEditWarning>> Validate(
        ControlPointEditSpec spec,
        RhinoObjectInfo target)
    {
        if (spec.ObjectId == Guid.Empty)
        {
            return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("EditControlPoints entry ObjectId cannot be empty.");
        }

        if (!AllFinite(spec.X, spec.Y, spec.Z))
        {
            return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("Control point coordinates must be finite values.");
        }

        if (spec.Weight.HasValue)
        {
            if (!IsFinite(spec.Weight.Value))
            {
                return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("Control point weight must be a finite value.");
            }

            if (spec.Weight.Value <= NearZeroTolerance)
            {
                return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("Control point weight must be greater than zero.");
            }
        }

        switch (spec.TargetMode)
        {
            case ControlPointTargetMode.CurveIndex:
                if (spec.PointIndex is null || spec.PointIndex < 0)
                {
                    return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("CurveIndex mode requires a non-negative PointIndex.");
                }

                if (!string.Equals(target.GeometryTypeName, "NurbsCurve", StringComparison.OrdinalIgnoreCase))
                {
                    return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail(
                        $"Object [{target.ObjectId}] is not a NurbsCurve and cannot use CurveIndex control point editing.");
                }
                break;

            case ControlPointTargetMode.SurfaceUV:
                if (spec.UIndex is null || spec.UIndex < 0 || spec.VIndex is null || spec.VIndex < 0)
                {
                    return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("SurfaceUV mode requires non-negative UIndex and VIndex values.");
                }

                if (!string.Equals(target.GeometryTypeName, "NurbsSurface", StringComparison.OrdinalIgnoreCase))
                {
                    return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail(
                        $"Object [{target.ObjectId}] is not a NurbsSurface and cannot use SurfaceUV control point editing.");
                }
                break;

            default:
                return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail($"Unsupported control point target mode: {spec.TargetMode}.");
        }

        return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Ok(Array.Empty<ObjectEditWarning>());
    }

    public OperationResponse ValidateAgainstDocument(RhinoDoc document, ControlPointEditSpec spec)
    {
        RhinoObject? currentObject = document.Objects.FindId(spec.ObjectId);
        if (currentObject?.Geometry is null)
        {
            return OperationResponse.Fail($"Object does not exist or has empty geometry: {spec.ObjectId}");
        }

        switch (spec.TargetMode)
        {
            case Domain.Enums.ControlPointTargetMode.CurveIndex:
                if (currentObject.Geometry is not NurbsCurve curve)
                {
                    return OperationResponse.Fail($"Object is not NurbsCurve: {spec.ObjectId}");
                }

                if (spec.PointIndex is null || spec.PointIndex < 0 || spec.PointIndex >= curve.Points.Count)
                {
                    return OperationResponse.Fail($"PointIndex out of range. ObjectId={spec.ObjectId}, PointIndex={spec.PointIndex}");
                }
                break;

            case Domain.Enums.ControlPointTargetMode.SurfaceUV:
                if (currentObject.Geometry is not NurbsSurface surface)
                {
                    return OperationResponse.Fail($"Object is not NurbsSurface: {spec.ObjectId}");
                }

                if (spec.UIndex is null || spec.VIndex is null
                    || spec.UIndex < 0 || spec.VIndex < 0
                    || spec.UIndex >= surface.Points.CountU || spec.VIndex >= surface.Points.CountV)
                {
                    return OperationResponse.Fail(
                        $"UIndex / VIndex out of range. ObjectId={spec.ObjectId}, UIndex={spec.UIndex}, VIndex={spec.VIndex}");
                }
                break;
        }

        return OperationResponse.Ok();
    }

    private static OperationResponse<IReadOnlyList<ObjectEditWarning>> ValidateAttributes(GeometryObjectAttributesSpec attributes)
    {
        if (attributes.Color is not null
            && (attributes.Color.R is < 0 or > 255
                || attributes.Color.G is < 0 or > 255
                || attributes.Color.B is < 0 or > 255))
        {
            return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("Color values must be in the 0-255 range.");
        }

        foreach ((string key, _) in attributes.UserText)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("UserText key cannot be empty.");
            }
        }

        return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Ok(Array.Empty<ObjectEditWarning>());
    }

    private static OperationResponse<IReadOnlyList<ObjectEditWarning>> ValidateGeometrySpec(GeometryCreationSpec spec)
    {
        return spec.Primitive switch
        {
            GeometryPrimitiveKind.Point => ValidatePoint(spec),
            GeometryPrimitiveKind.Line => ValidateLine(spec),
            GeometryPrimitiveKind.Arc => ValidateArc(spec),
            GeometryPrimitiveKind.Surface => ValidateSurface(spec),
            _ => OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail($"Unsupported geometry type: {spec.Primitive}.")
        };
    }

    private static OperationResponse<IReadOnlyList<ObjectEditWarning>> ValidatePoint(GeometryCreationSpec spec)
    {
        if (!AllFinite(spec.X, spec.Y, spec.Z))
        {
            return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("Point coordinates must be finite values.");
        }

        return ValidateBBoxWarning(new Point(new Point3d(spec.X, spec.Y, spec.Z)));
    }

    private static OperationResponse<IReadOnlyList<ObjectEditWarning>> ValidateLine(GeometryCreationSpec spec)
    {
        if (!AllFinite(spec.StartX, spec.StartY, spec.StartZ, spec.EndX, spec.EndY, spec.EndZ))
        {
            return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("Line start and end points must be finite values.");
        }

        var line = new Line(
            new Point3d(spec.StartX, spec.StartY, spec.StartZ),
            new Point3d(spec.EndX, spec.EndY, spec.EndZ));

        if (line.Length <= NearZeroTolerance)
        {
            return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("Line cannot have zero length.");
        }

        return ValidateBBoxWarning(new LineCurve(line));
    }

    private static OperationResponse<IReadOnlyList<ObjectEditWarning>> ValidateArc(GeometryCreationSpec spec)
    {
        switch (spec.ArcMode)
        {
            case ArcConstructionMode.ThreePoint:
                if (!AllFinite(
                        spec.StartX, spec.StartY, spec.StartZ,
                        spec.MidX, spec.MidY, spec.MidZ,
                        spec.EndX, spec.EndY, spec.EndZ))
                {
                    return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("ThreePoint arc construction requires three finite point coordinates.");
                }

                var threePointArc = new Arc(
                    new Point3d(spec.StartX, spec.StartY, spec.StartZ),
                    new Point3d(spec.MidX, spec.MidY, spec.MidZ),
                    new Point3d(spec.EndX, spec.EndY, spec.EndZ));

                if (!threePointArc.IsValid)
                {
                    return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("Arc three-point construction failed; points may be collinear or degenerate.");
                }

                return ValidateBBoxWarning(new ArcCurve(threePointArc));

            case ArcConstructionMode.CenterRadius:
                if (!AllFinite(
                        spec.CenterX, spec.CenterY, spec.CenterZ,
                        spec.NormalX, spec.NormalY, spec.NormalZ,
                        spec.Radius, spec.StartAngleRadians, spec.EndAngleRadians))
                {
                    return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("CenterRadius arc construction requires finite center, normal, radius, and angle values.");
                }

                if (spec.Radius <= NearZeroTolerance)
                {
                    return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("Arc radius must be greater than zero.");
                }

                if (IsZeroVector(spec.NormalX, spec.NormalY, spec.NormalZ))
                {
                    return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("Arc normal vector cannot be zero.");
                }

                var plane = new Plane(
                    new Point3d(spec.CenterX, spec.CenterY, spec.CenterZ),
                    new Vector3d(spec.NormalX, spec.NormalY, spec.NormalZ));
                var circle = new Circle(plane, spec.Radius);
                var arc = new Arc(circle, new Interval(spec.StartAngleRadians, spec.EndAngleRadians));
                if (!arc.IsValid)
                {
                    return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("Arc center-radius construction failed.");
                }

                return ValidateBBoxWarning(new ArcCurve(arc));

            default:
                return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail($"Unsupported arc construction mode: {spec.ArcMode}.");
        }
    }

    private static OperationResponse<IReadOnlyList<ObjectEditWarning>> ValidateSurface(GeometryCreationSpec spec)
    {
        switch (spec.SurfaceMode)
        {
            case SurfaceConstructionMode.FourCorners:
                if (!AllFinite(
                        spec.Corner0X, spec.Corner0Y, spec.Corner0Z,
                        spec.Corner1X, spec.Corner1Y, spec.Corner1Z,
                        spec.Corner2X, spec.Corner2Y, spec.Corner2Z,
                        spec.Corner3X, spec.Corner3Y, spec.Corner3Z))
                {
                    return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("FourCorners surface construction requires four finite corner point coordinates.");
                }

                NurbsSurface cornerSurface = NurbsSurface.Create(3, false, 2, 2, 2, 2);
                cornerSurface.Points.SetPoint(0, 0, new Point3d(spec.Corner0X, spec.Corner0Y, spec.Corner0Z));
                cornerSurface.Points.SetPoint(1, 0, new Point3d(spec.Corner1X, spec.Corner1Y, spec.Corner1Z));
                cornerSurface.Points.SetPoint(1, 1, new Point3d(spec.Corner2X, spec.Corner2Y, spec.Corner2Z));
                cornerSurface.Points.SetPoint(0, 1, new Point3d(spec.Corner3X, spec.Corner3Y, spec.Corner3Z));
                cornerSurface.KnotsU[0] = 0d;
                cornerSurface.KnotsU[1] = 1d;
                cornerSurface.KnotsV[0] = 0d;
                cornerSurface.KnotsV[1] = 1d;
                cornerSurface.SetDomain(0, new Interval(0d, 1d));
                cornerSurface.SetDomain(1, new Interval(0d, 1d));

                if (!cornerSurface.IsValid)
                {
                    return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("FourCorners surface construction failed; corner points may be degenerate.");
                }

                return ValidateBBoxWarning(cornerSurface);

            case SurfaceConstructionMode.Plane:
                if (!AllFinite(
                        spec.OriginX, spec.OriginY, spec.OriginZ,
                        spec.NormalX, spec.NormalY, spec.NormalZ,
                        spec.ULength, spec.VLength))
                {
                    return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("Plane surface construction requires finite origin, normal, and dimension values.");
                }

                if (IsZeroVector(spec.NormalX, spec.NormalY, spec.NormalZ))
                {
                    return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("Plane surface normal vector cannot be zero.");
                }

                if (spec.ULength <= NearZeroTolerance || spec.VLength <= NearZeroTolerance)
                {
                    return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("Plane surface U and V dimensions must be greater than zero.");
                }

                var planeSurface = new PlaneSurface(
                    new Plane(
                        new Point3d(spec.OriginX, spec.OriginY, spec.OriginZ),
                        new Vector3d(spec.NormalX, spec.NormalY, spec.NormalZ)),
                    new Interval(0d, spec.ULength),
                    new Interval(0d, spec.VLength));

                if (!planeSurface.IsValid)
                {
                    return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail("Plane surface construction failed.");
                }

                return ValidateBBoxWarning(planeSurface);

            default:
                return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Fail($"Unsupported surface construction mode: {spec.SurfaceMode}.");
        }
    }

    private static OperationResponse<IReadOnlyList<ObjectEditWarning>> ValidateBBoxWarning(rhinocommon::Rhino.Geometry.GeometryBase geometry)
    {
        var warnings = new List<ObjectEditWarning>();
        BoundingBox boundingBox = geometry.GetBoundingBox(true);
        if (boundingBox.IsValid && boundingBox.Min.DistanceTo(boundingBox.Max) > LargeBBoxDiagonalThreshold)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "LARGE_BBOX",
                Message = "Geometry bounding box diagonal exceeds 1e12; units may be mismatched."
            });
        }

        return OperationResponse<IReadOnlyList<ObjectEditWarning>>.Ok(warnings);
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
}
