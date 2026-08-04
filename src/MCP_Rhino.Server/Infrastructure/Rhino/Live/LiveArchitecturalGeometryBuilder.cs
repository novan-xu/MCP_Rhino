extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using BoundingBox = rhinocommon::Rhino.Geometry.BoundingBox;
using Box = rhinocommon::Rhino.Geometry.Box;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using Circle = rhinocommon::Rhino.Geometry.Circle;
using Cylinder = rhinocommon::Rhino.Geometry.Cylinder;
using Extrusion = rhinocommon::Rhino.Geometry.Extrusion;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;
using Interval = rhinocommon::Rhino.Geometry.Interval;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using ObjectColorSource = rhinocommon::Rhino.DocObjects.ObjectColorSource;
using Plane = rhinocommon::Rhino.Geometry.Plane;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using Polyline = rhinocommon::Rhino.Geometry.Polyline;
using PolylineCurve = rhinocommon::Rhino.Geometry.PolylineCurve;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using Transform = rhinocommon::Rhino.Geometry.Transform;
using Vector3d = rhinocommon::Rhino.Geometry.Vector3d;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveArchitecturalGeometryBuilder : ILiveArchitecturalGeometryBuilder
{
    private const string CapabilityName = "architectural-modeling-primitives";

    private readonly ILiveRhinoDocumentAccessor _documentAccessor;

    public LiveArchitecturalGeometryBuilder(ILiveRhinoDocumentAccessor documentAccessor)
    {
        _documentAccessor = documentAccessor;
    }

    public OperationResponse<ArchitecturalCreationResponse> Create(
        string filePath,
        IReadOnlyList<ArchitecturalPrimitiveSpec> specs,
        ArchitecturalObjectAttributesSpec attributes,
        string undoRecordName)
    {
        return _documentAccessor.ExecuteWithUndo(filePath, undoRecordName, document =>
        {
            OperationResponse<List<PreparedPrimitive>> prepared = PreparePrimitives(document, specs, attributes, requireLayer: true);
            if (!prepared.Success || prepared.Data is null)
            {
                return OperationResponse<(bool Mutated, ArchitecturalCreationResponse Result)>.Fail(prepared.Message);
            }

            var createdObjects = AddPreparedGeometry(document, filePath, prepared.Data);
            return OperationResponse<(bool Mutated, ArchitecturalCreationResponse Result)>.Ok(
                (createdObjects.CreatedObjects.Count > 0, createdObjects),
                "Architectural primitive creation completed.");
        });
    }

    internal static GeometryBase BuildOpeningCutter(ArchitecturalOpeningOperationSpec spec)
    {
        return spec.OpeningKind == OpeningKind.Circular
            ? BuildCircularOpeningCutter(spec)
            : BuildRectangularOpeningCutter(spec);
    }

    internal static ArchitecturalBoundingBoxResponse ToResponse(BoundingBox box)
    {
        return new ArchitecturalBoundingBoxResponse
        {
            MinX = box.Min.X,
            MinY = box.Min.Y,
            MinZ = box.Min.Z,
            MaxX = box.Max.X,
            MaxY = box.Max.Y,
            MaxZ = box.Max.Z
        };
    }

    internal static ObjectAttributes CreateAttributes(int layerIndex, ArchitecturalObjectAttributesSpec attributesSpec, ArchitecturalPrimitiveSpec spec)
    {
        var attributes = new ObjectAttributes
        {
            LayerIndex = layerIndex,
            Name = string.IsNullOrWhiteSpace(spec.Name) ? attributesSpec.Name : spec.Name
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

        ArchitecturalMetadataSpec metadata = MergeMetadata(attributesSpec.Metadata, spec.Metadata, DefaultCategory(spec.Kind));
        SetUserTextIfMissing(attributes, "mcp.category", metadata.Category);
        SetUserTextIfMissing(attributes, "mcp.level", metadata.LevelName);
        SetUserTextIfMissing(attributes, "mcp.system", metadata.SystemName);
        SetUserTextIfMissing(attributes, "mcp.source", metadata.SourceTag);
        SetUserTextIfMissing(attributes, "mcp.capability", CapabilityName);
        return attributes;
    }

    internal static string DefaultCategory(ArchitecturalPrimitiveKind kind)
    {
        return kind switch
        {
            ArchitecturalPrimitiveKind.Box => "massing",
            ArchitecturalPrimitiveKind.PlanarBrep => "planar-brep",
            ArchitecturalPrimitiveKind.OpeningCutter => "opening-cutter",
            _ => kind.ToString().ToLowerInvariant()
        };
    }

    private OperationResponse<List<PreparedPrimitive>> PreparePrimitives(
        RhinoDoc document,
        IReadOnlyList<ArchitecturalPrimitiveSpec> specs,
        ArchitecturalObjectAttributesSpec attributes,
        bool requireLayer)
    {
        var prepared = new List<PreparedPrimitive>(specs.Count);
        var warnings = new List<ObjectEditWarning>();

        foreach (ArchitecturalPrimitiveSpec spec in specs)
        {
            OperationResponse validation = Validate(spec);
            if (!validation.Success)
            {
                return OperationResponse<List<PreparedPrimitive>>.Fail(validation.Message);
            }

            GeometryBase geometry;
            try
            {
                geometry = BuildGeometry(spec, document.ModelAbsoluteTolerance);
            }
            catch (Exception ex)
            {
                return OperationResponse<List<PreparedPrimitive>>.Fail($"Failed to build {spec.Kind}: {ex.Message}");
            }

            string layerPath = ResolveLayerPath(attributes, spec, requireLayer);
            int layerIndex = ResolveLayerIndex(document, layerPath, attributes.AutoCreateLayer || !requireLayer);
            if (layerIndex < 0)
            {
                return OperationResponse<List<PreparedPrimitive>>.Fail($"Target layer was not found: {layerPath}");
            }

            ObjectAttributes objectAttributes = CreateAttributes(layerIndex, attributes, spec);
            prepared.Add(new PreparedPrimitive(spec, geometry, objectAttributes, layerPath));

            BoundingBox box = geometry.GetBoundingBox(true);
            if (box.IsValid && box.Min.DistanceTo(box.Max) > 1e12)
            {
                warnings.Add(new ObjectEditWarning
                {
                    Code = "LARGE_BBOX",
                    Message = $"Geometry bounding box diagonal exceeds 1e12 for {spec.Kind}."
                });
            }
        }

        foreach (PreparedPrimitive item in prepared)
        {
            item.Warnings.AddRange(warnings);
        }

        return OperationResponse<List<PreparedPrimitive>>.Ok(prepared);
    }

    private static ArchitecturalCreationResponse AddPreparedGeometry(
        RhinoDoc document,
        string filePath,
        IReadOnlyList<PreparedPrimitive> prepared)
    {
        var createdObjects = new List<ArchitecturalCreatedObjectResponse>(prepared.Count);
        var warnings = new List<ObjectEditWarning>();

        foreach (PreparedPrimitive item in prepared)
        {
            Guid objectId = document.Objects.Add(item.Geometry, item.Attributes);
            if (objectId == Guid.Empty)
            {
                throw new InvalidOperationException($"Failed to add architectural geometry: {item.Spec.Kind}");
            }

            ArchitecturalMetadataSpec metadata = MergeMetadata(new ArchitecturalMetadataSpec(), item.Spec.Metadata, DefaultCategory(item.Spec.Kind));
            BoundingBox box = item.Geometry.GetBoundingBox(true);
            createdObjects.Add(new ArchitecturalCreatedObjectResponse
            {
                ObjectId = objectId,
                Kind = item.Spec.Kind,
                Category = metadata.Category,
                LevelName = metadata.LevelName,
                LayerFullPath = item.LayerFullPath,
                BoundingBox = box.IsValid ? ToResponse(box) : null
            });
            warnings.AddRange(item.Warnings);
        }

        if (createdObjects.Count > 0)
        {
            document.Views.Redraw();
        }

        return new ArchitecturalCreationResponse
        {
            FilePath = filePath,
            RequestedCount = prepared.Count,
            CreatedCount = createdObjects.Count,
            FailedCount = prepared.Count - createdObjects.Count,
            CreatedObjects = createdObjects,
            Warnings = warnings
        };
    }

    private static OperationResponse Validate(ArchitecturalPrimitiveSpec spec)
    {
        switch (spec.Kind)
        {
            case ArchitecturalPrimitiveKind.Box:
                return RequirePositive(spec.SizeX, "SizeX")
                    ?? RequirePositive(spec.SizeY, "SizeY")
                    ?? RequirePositive(spec.SizeZ, "SizeZ")
                    ?? OperationResponse.Ok();
            case ArchitecturalPrimitiveKind.Extrusion:
                return RequireClosedPointList(spec.Points, "Extrusion profile")
                    ?? RequirePositive(spec.Height, "Height")
                    ?? OperationResponse.Ok();
            case ArchitecturalPrimitiveKind.PlanarBrep:
                if (spec.Loops.Count == 0)
                {
                    return OperationResponse.Fail("At least one planar Brep loop is required.");
                }

                foreach (IReadOnlyList<ArchitecturalPointSpec> loop in spec.Loops)
                {
                    OperationResponse? loopValidation = RequireClosedPointList(loop, "Planar Brep loop");
                    if (loopValidation is not null)
                    {
                        return loopValidation;
                    }
                }

                return OperationResponse.Ok();
            case ArchitecturalPrimitiveKind.Slab:
                return RequireClosedPointList(spec.Points, "Slab footprint")
                    ?? RequirePositive(spec.Thickness, "Thickness")
                    ?? OperationResponse.Ok();
            case ArchitecturalPrimitiveKind.Wall:
                if (spec.Points.Count < 2)
                {
                    return OperationResponse.Fail("Wall baseline requires at least two points.");
                }

                return RequirePositive(spec.Height, "Height")
                    ?? RequirePositive(spec.Thickness, "Thickness")
                    ?? OperationResponse.Ok();
            case ArchitecturalPrimitiveKind.Column:
                return RequirePositive(spec.Height, "Height")
                    ?? (spec.ProfileShape == ProfileShapeKind.Circular
                        ? RequirePositive(spec.Radius, "Radius")
                        : RequirePositive(spec.Width, "Width") ?? RequirePositive(spec.Depth, "Depth"))
                    ?? OperationResponse.Ok();
            case ArchitecturalPrimitiveKind.Beam:
                return RequirePositive(spec.ProfileShape == ProfileShapeKind.Circular ? spec.Radius : spec.Width, spec.ProfileShape == ProfileShapeKind.Circular ? "Radius" : "Width")
                    ?? (spec.ProfileShape == ProfileShapeKind.Rectangular ? RequirePositive(spec.Depth, "Depth") : null)
                    ?? OperationResponse.Ok();
            default:
                return OperationResponse.Fail($"Unsupported architectural primitive kind: {spec.Kind}");
        }
    }

    private static OperationResponse? RequirePositive(double value, string fieldName)
    {
        return value > 0d ? null : OperationResponse.Fail($"{fieldName} must be greater than zero.");
    }

    private static OperationResponse? RequireClosedPointList(IReadOnlyList<ArchitecturalPointSpec> points, string label)
    {
        if (points.Count < 3)
        {
            return OperationResponse.Fail($"{label} requires at least three points.");
        }

        ArchitecturalPointSpec first = points[0];
        ArchitecturalPointSpec last = points[^1];
        bool closed = Distance(first, last) < 1e-9;
        return closed ? null : OperationResponse.Fail($"{label} must be closed; repeat the first point as the last point.");
    }

    private static double Distance(ArchitecturalPointSpec left, ArchitecturalPointSpec right)
    {
        double dx = left.X - right.X;
        double dy = left.Y - right.Y;
        double dz = left.Z - right.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static GeometryBase BuildGeometry(ArchitecturalPrimitiveSpec spec, double tolerance)
    {
        return spec.Kind switch
        {
            ArchitecturalPrimitiveKind.Box => BuildBox(spec),
            ArchitecturalPrimitiveKind.Extrusion => BuildExtrusion(spec),
            ArchitecturalPrimitiveKind.PlanarBrep => BuildPlanarBrep(spec, tolerance),
            ArchitecturalPrimitiveKind.Slab => BuildSlab(spec),
            ArchitecturalPrimitiveKind.Wall => BuildWall(spec),
            ArchitecturalPrimitiveKind.Column => BuildColumn(spec),
            ArchitecturalPrimitiveKind.Beam => BuildBeam(spec),
            _ => throw new InvalidOperationException($"Unsupported architectural primitive kind: {spec.Kind}")
        };
    }

    private static GeometryBase BuildBox(ArchitecturalPrimitiveSpec spec)
    {
        var box = new Box(
            Plane.WorldXY,
            new Interval(spec.OriginX, spec.OriginX + spec.SizeX),
            new Interval(spec.OriginY, spec.OriginY + spec.SizeY),
            new Interval(spec.OriginZ, spec.OriginZ + spec.SizeZ));

        if (spec.Representation == ArchitecturalRepresentationKind.Extrusion)
        {
            Extrusion? extrusion = Extrusion.CreateBoxExtrusion(box, true);
            return extrusion ?? throw new InvalidOperationException("Rhino failed to create box extrusion.");
        }

        return Brep.CreateFromBox(box);
    }

    private static GeometryBase BuildExtrusion(ArchitecturalPrimitiveSpec spec)
    {
        PolylineCurve profile = CreatePolylineCurve(spec.Points);
        Extrusion? extrusion = Extrusion.Create(profile, spec.Height, spec.Cap);
        if (extrusion is null)
        {
            throw new InvalidOperationException("Rhino failed to create extrusion.");
        }

        return spec.Representation == ArchitecturalRepresentationKind.Brep
            ? extrusion.ToBrep()
            : extrusion;
    }

    private static GeometryBase BuildPlanarBrep(ArchitecturalPrimitiveSpec spec, double tolerance)
    {
        PolylineCurve[] curves = spec.Loops.Select(CreatePolylineCurve).ToArray();
        Brep[] breps = Brep.CreatePlanarBreps(curves, tolerance);
        if (breps.Length == 0)
        {
            throw new InvalidOperationException("Rhino failed to create planar Brep from supplied loops.");
        }

        return breps[0];
    }

    private static GeometryBase BuildSlab(ArchitecturalPrimitiveSpec spec)
    {
        List<ArchitecturalPointSpec> elevated = spec.Points
            .Select(point => new ArchitecturalPointSpec
            {
                X = point.X,
                Y = point.Y,
                Z = spec.Elevation
            })
            .ToList();

        Extrusion? extrusion = Extrusion.Create(CreatePolylineCurve(elevated), spec.Thickness, true);
        if (extrusion is null)
        {
            throw new InvalidOperationException("Rhino failed to create slab extrusion.");
        }

        return spec.Representation == ArchitecturalRepresentationKind.Brep
            ? extrusion.ToBrep()
            : extrusion;
    }

    private static GeometryBase BuildWall(ArchitecturalPrimitiveSpec spec)
    {
        ArchitecturalPointSpec start = spec.Points[0];
        ArchitecturalPointSpec end = spec.Points[1];
        var direction = new Vector3d(end.X - start.X, end.Y - start.Y, 0d);
        if (!direction.Unitize())
        {
            throw new InvalidOperationException("Wall baseline length must be greater than zero.");
        }

        var offset = new Vector3d(-direction.Y, direction.X, 0d);
        double left = spec.WallAlignment switch
        {
            WallAlignmentKind.Left => 0d,
            WallAlignmentKind.Right => -spec.Thickness,
            _ => -spec.Thickness / 2d
        };

        double right = left + spec.Thickness;
        var points = new List<ArchitecturalPointSpec>
        {
            ToPoint(start, spec.BaseElevation, offset, left),
            ToPoint(end, spec.BaseElevation, offset, left),
            ToPoint(end, spec.BaseElevation, offset, right),
            ToPoint(start, spec.BaseElevation, offset, right),
            ToPoint(start, spec.BaseElevation, offset, left)
        };

        Extrusion? extrusion = Extrusion.Create(CreatePolylineCurve(points), spec.Height, true);
        return extrusion?.ToBrep() ?? throw new InvalidOperationException("Rhino failed to create wall extrusion.");
    }

    private static GeometryBase BuildColumn(ArchitecturalPrimitiveSpec spec)
    {
        double baseZ = Math.Abs(spec.BaseElevation) > double.Epsilon ? spec.BaseElevation : spec.CenterZ;
        if (spec.ProfileShape == ProfileShapeKind.Circular)
        {
            var circle = new Circle(new Plane(new Point3d(spec.CenterX, spec.CenterY, baseZ), Vector3d.ZAxis), spec.Radius);
            var cylinder = new Cylinder(circle, spec.Height);
            Extrusion? extrusion = Extrusion.CreateCylinderExtrusion(cylinder, true, true);
            if (extrusion is null)
            {
                throw new InvalidOperationException("Rhino failed to create circular column extrusion.");
            }

            return spec.Representation == ArchitecturalRepresentationKind.Brep ? extrusion.ToBrep() : extrusion;
        }

        var boxSpec = new ArchitecturalPrimitiveSpec
        {
            Kind = ArchitecturalPrimitiveKind.Box,
            Representation = spec.Representation,
            OriginX = spec.CenterX - spec.Width / 2d,
            OriginY = spec.CenterY - spec.Depth / 2d,
            OriginZ = baseZ,
            SizeX = spec.Width,
            SizeY = spec.Depth,
            SizeZ = spec.Height
        };
        return BuildBox(boxSpec);
    }

    private static GeometryBase BuildBeam(ArchitecturalPrimitiveSpec spec)
    {
        var start = new Point3d(spec.StartX, spec.StartY, spec.StartZ);
        var end = new Point3d(spec.EndX, spec.EndY, spec.EndZ);
        var xAxis = end - start;
        double length = xAxis.Length;
        if (!xAxis.Unitize())
        {
            throw new InvalidOperationException("Beam baseline length must be greater than zero.");
        }

        if (spec.ProfileShape == ProfileShapeKind.Circular)
        {
            var circle = new Circle(new Plane(start, xAxis), spec.Radius);
            var cylinder = new Cylinder(circle, length);
            Extrusion? extrusion = Extrusion.CreateCylinderExtrusion(cylinder, true, true);
            if (extrusion is null)
            {
                throw new InvalidOperationException("Rhino failed to create circular beam extrusion.");
            }

            return extrusion.ToBrep();
        }

        var up = Vector3d.ZAxis;
        if (Math.Abs(Vector3d.Multiply(xAxis, up)) > 0.98)
        {
            up = Vector3d.YAxis;
        }

        Vector3d yAxis = Vector3d.CrossProduct(up, xAxis);
        yAxis.Unitize();
        var plane = new Plane(start, xAxis, yAxis);
        var box = new Box(
            plane,
            new Interval(0d, length),
            new Interval(-spec.Width / 2d, spec.Width / 2d),
            new Interval(-spec.Depth / 2d, spec.Depth / 2d));
        return Brep.CreateFromBox(box);
    }

    private static GeometryBase BuildRectangularOpeningCutter(ArchitecturalOpeningOperationSpec spec)
    {
        double depth = spec.Depth > 0d ? spec.Depth : Math.Max(spec.Width, spec.Height);
        var box = new Box(
            Plane.WorldXY,
            new Interval(spec.CenterX - spec.Width / 2d, spec.CenterX + spec.Width / 2d),
            new Interval(spec.CenterY - depth / 2d, spec.CenterY + depth / 2d),
            new Interval(spec.CenterZ - spec.Height / 2d, spec.CenterZ + spec.Height / 2d));
        return Brep.CreateFromBox(box);
    }

    private static GeometryBase BuildCircularOpeningCutter(ArchitecturalOpeningOperationSpec spec)
    {
        double depth = spec.Depth > 0d ? spec.Depth : Math.Max(spec.Radius * 4d, 1d);
        var plane = new Plane(new Point3d(spec.CenterX, spec.CenterY - depth / 2d, spec.CenterZ), Vector3d.YAxis);
        var circle = new Circle(plane, spec.Radius);
        var cylinder = new Cylinder(circle, depth);
        Extrusion? extrusion = Extrusion.CreateCylinderExtrusion(cylinder, true, true);
        return extrusion?.ToBrep() ?? throw new InvalidOperationException("Rhino failed to create circular opening cutter.");
    }

    private static PolylineCurve CreatePolylineCurve(IReadOnlyList<ArchitecturalPointSpec> points)
    {
        var polyline = new Polyline(points.Select(point => new Point3d(point.X, point.Y, point.Z)));
        return new PolylineCurve(polyline);
    }

    private static ArchitecturalPointSpec ToPoint(ArchitecturalPointSpec source, double z, Vector3d offset, double distance)
    {
        return new ArchitecturalPointSpec
        {
            X = source.X + offset.X * distance,
            Y = source.Y + offset.Y * distance,
            Z = z
        };
    }

    private static int ResolveLayerIndex(RhinoDoc document, string layerFullPath, bool autoCreate)
    {
        int layerIndex = document.Layers.FindByFullPath(layerFullPath, -1);
        if (layerIndex >= 0 || !autoCreate)
        {
            return layerIndex;
        }

        int createdIndex = document.Layers.AddPath(layerFullPath);
        return createdIndex >= 0 ? document.Layers.FindByFullPath(layerFullPath, -1) : -1;
    }

    private static string ResolveLayerPath(
        ArchitecturalObjectAttributesSpec attributes,
        ArchitecturalPrimitiveSpec spec,
        bool requireLayer)
    {
        if (!string.IsNullOrWhiteSpace(attributes.LayerFullPath))
        {
            return attributes.LayerFullPath.Trim();
        }

        if (requireLayer && !attributes.AutoCreateLayer)
        {
            return string.Empty;
        }

        string category = MergeMetadata(attributes.Metadata, spec.Metadata, DefaultCategory(spec.Kind)).Category;
        return $"A-ARCH::{category.ToUpperInvariant()}";
    }

    private static ArchitecturalMetadataSpec MergeMetadata(
        ArchitecturalMetadataSpec common,
        ArchitecturalMetadataSpec item,
        string defaultCategory)
    {
        return new ArchitecturalMetadataSpec
        {
            Category = FirstNonEmpty(item.Category, common.Category, defaultCategory),
            LevelName = FirstNonEmpty(item.LevelName, common.LevelName, string.Empty),
            SystemName = FirstNonEmpty(item.SystemName, common.SystemName, string.Empty),
            SourceTag = FirstNonEmpty(item.SourceTag, common.SourceTag, string.Empty)
        };
    }

    private static string FirstNonEmpty(string? first, string? second, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(first))
        {
            return first.Trim();
        }

        if (!string.IsNullOrWhiteSpace(second))
        {
            return second.Trim();
        }

        return fallback;
    }

    private static void SetUserTextIfMissing(ObjectAttributes attributes, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !string.IsNullOrEmpty(attributes.GetUserString(key)))
        {
            return;
        }

        attributes.SetUserString(key, value);
    }

    private sealed class PreparedPrimitive
    {
        public PreparedPrimitive(
            ArchitecturalPrimitiveSpec spec,
            GeometryBase geometry,
            ObjectAttributes attributes,
            string layerFullPath)
        {
            Spec = spec;
            Geometry = geometry;
            Attributes = attributes;
            LayerFullPath = layerFullPath;
        }

        public ArchitecturalPrimitiveSpec Spec { get; }
        public GeometryBase Geometry { get; }
        public ObjectAttributes Attributes { get; }
        public string LayerFullPath { get; }
        public List<ObjectEditWarning> Warnings { get; } = new();
    }
}
