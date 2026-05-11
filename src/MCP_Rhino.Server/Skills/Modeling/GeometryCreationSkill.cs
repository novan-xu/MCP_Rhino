using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Skills.Modeling;

public sealed class GeometryCreationSkill
{
    private readonly RhinoGeometryCreationService _creationService;
    private readonly RhinoGeneralPrimitiveCreationService _generalPrimitiveService;

    public GeometryCreationSkill(
        RhinoGeometryCreationService creationService,
        RhinoGeneralPrimitiveCreationService generalPrimitiveService)
    {
        _creationService = creationService;
        _generalPrimitiveService = generalPrimitiveService;
    }

    public OperationResponse<GeometryCreationResponse> Create(CreatePointsRequest request)
    {
        return _creationService.Create(
            request.FilePath,
            request.Items.Select(MapPoint).ToList(),
            MapAttributes(request.Common));
    }

    public OperationResponse<GeometryCreationResponse> Create(CreateLinesRequest request)
    {
        return _creationService.Create(
            request.FilePath,
            request.Items.Select(MapLine).ToList(),
            MapAttributes(request.Common));
    }

    public OperationResponse<GeometryCreationResponse> Create(CreateArcsRequest request)
    {
        return _creationService.Create(
            request.FilePath,
            request.Items.Select(MapArc).ToList(),
            MapAttributes(request.Common));
    }

    public OperationResponse<GeometryCreationResponse> Create(CreateSurfacesRequest request)
    {
        return _creationService.Create(
            request.FilePath,
            request.Items.Select(MapSurface).ToList(),
            MapAttributes(request.Common));
    }

    public OperationResponse<GeneralPrimitiveCreationResponse> Create(CreateCirclesRequest request)
    {
        return _generalPrimitiveService.Create(
            request.FilePath,
            request.Items.Select(MapCircle).ToList(),
            MapAttributes(request.Common));
    }

    public OperationResponse<GeneralPrimitiveCreationResponse> Create(CreateEllipsesRequest request)
    {
        return _generalPrimitiveService.Create(
            request.FilePath,
            request.Items.Select(MapEllipse).ToList(),
            MapAttributes(request.Common));
    }

    public OperationResponse<GeneralPrimitiveCreationResponse> Create(CreatePolylinesRequest request)
    {
        return _generalPrimitiveService.Create(
            request.FilePath,
            request.Items.Select(MapPolyline).ToList(),
            MapAttributes(request.Common));
    }

    public OperationResponse<GeneralPrimitiveCreationResponse> Create(CreateNurbsCurvesRequest request)
    {
        return _generalPrimitiveService.Create(
            request.FilePath,
            request.Items.Select(MapNurbsCurve).ToList(),
            MapAttributes(request.Common));
    }

    public OperationResponse<GeneralPrimitiveCreationResponse> Create(CreateSpheresRequest request)
    {
        return _generalPrimitiveService.Create(
            request.FilePath,
            request.Items.Select(MapSphere).ToList(),
            MapAttributes(request.Common));
    }

    public OperationResponse<GeneralPrimitiveCreationResponse> Create(CreateConesRequest request)
    {
        return _generalPrimitiveService.Create(
            request.FilePath,
            request.Items.Select(MapCone).ToList(),
            MapAttributes(request.Common));
    }

    public OperationResponse<GeneralPrimitiveCreationResponse> Create(CreateCylindersRequest request)
    {
        return _generalPrimitiveService.Create(
            request.FilePath,
            request.Items.Select(MapCylinder).ToList(),
            MapAttributes(request.Common));
    }

    public OperationResponse<GeneralPrimitiveCreationResponse> Create(CreateEllipsoidsRequest request)
    {
        return _generalPrimitiveService.Create(
            request.FilePath,
            request.Items.Select(MapEllipsoid).ToList(),
            MapReferenceImageObjectModelingAttributes(request.Common, "ellipsoid", "massing", "primary"));
    }

    public OperationResponse<GeneralPrimitiveCreationResponse> Create(CreateCapsulesRequest request)
    {
        return _generalPrimitiveService.Create(
            request.FilePath,
            request.Items.Select(MapCapsule).ToList(),
            MapReferenceImageObjectModelingAttributes(request.Common, "capsule", "massing", "primary"));
    }

    public OperationResponse<GeneralPrimitiveCreationResponse> Create(CreateToriRequest request)
    {
        return _generalPrimitiveService.Create(
            request.FilePath,
            request.Items.Select(MapTorus).ToList(),
            MapReferenceImageObjectModelingAttributes(request.Common, "torus", "massing", "primary"));
    }

    public OperationResponse<GeneralPrimitiveCreationResponse> Create(CreateRoundedBoxesRequest request)
    {
        return _generalPrimitiveService.Create(
            request.FilePath,
            request.Items.Select(MapRoundedBox).ToList(),
            MapReferenceImageObjectModelingAttributes(request.Common, "rounded-box", "massing", "primary"));
    }

    public OperationResponse<GeneralPrimitiveCreationResponse> Create(CreateRaisedStripsRequest request)
    {
        return _generalPrimitiveService.Create(
            request.FilePath,
            request.Items.Select(MapRaisedStrip).ToList(),
            MapReferenceImageObjectModelingAttributes(request.Common, "raised-strip", "detail", "detail"));
    }

    public OperationResponse<GeneralPrimitiveCreationResponse> Create(CreateTaperedBoxesRequest request)
    {
        return _generalPrimitiveService.Create(
            request.FilePath,
            request.Items.Select(MapTaperedBox).ToList(),
            MapReferenceImageObjectModelingAttributes(request.Common, "tapered-box", "massing", "primary"));
    }

    private static GeometryCreationSpec MapPoint(PointItemRequest request)
    {
        return new GeometryCreationSpec
        {
            Primitive = GeometryPrimitiveKind.Point,
            X = request.X,
            Y = request.Y,
            Z = request.Z
        };
    }

    private static GeometryCreationSpec MapLine(LineItemRequest request)
    {
        return new GeometryCreationSpec
        {
            Primitive = GeometryPrimitiveKind.Line,
            StartX = request.StartX,
            StartY = request.StartY,
            StartZ = request.StartZ,
            EndX = request.EndX,
            EndY = request.EndY,
            EndZ = request.EndZ
        };
    }

    private static GeometryCreationSpec MapArc(ArcItemRequest request)
    {
        return new GeometryCreationSpec
        {
            Primitive = GeometryPrimitiveKind.Arc,
            ArcMode = request.Mode,
            StartX = request.StartX,
            StartY = request.StartY,
            StartZ = request.StartZ,
            MidX = request.MidX,
            MidY = request.MidY,
            MidZ = request.MidZ,
            EndX = request.EndX,
            EndY = request.EndY,
            EndZ = request.EndZ,
            CenterX = request.CenterX,
            CenterY = request.CenterY,
            CenterZ = request.CenterZ,
            Radius = request.Radius,
            NormalX = request.NormalX,
            NormalY = request.NormalY,
            NormalZ = request.NormalZ,
            StartAngleRadians = request.StartAngleRadians,
            EndAngleRadians = request.EndAngleRadians
        };
    }

    private static GeometryCreationSpec MapSurface(SurfaceItemRequest request)
    {
        return new GeometryCreationSpec
        {
            Primitive = GeometryPrimitiveKind.Surface,
            SurfaceMode = request.Mode,
            Corner0X = request.Corner0X,
            Corner0Y = request.Corner0Y,
            Corner0Z = request.Corner0Z,
            Corner1X = request.Corner1X,
            Corner1Y = request.Corner1Y,
            Corner1Z = request.Corner1Z,
            Corner2X = request.Corner2X,
            Corner2Y = request.Corner2Y,
            Corner2Z = request.Corner2Z,
            Corner3X = request.Corner3X,
            Corner3Y = request.Corner3Y,
            Corner3Z = request.Corner3Z,
            OriginX = request.OriginX,
            OriginY = request.OriginY,
            OriginZ = request.OriginZ,
            NormalX = request.NormalX,
            NormalY = request.NormalY,
            NormalZ = request.NormalZ,
            ULength = request.ULength,
            VLength = request.VLength
        };
    }

    private static GeneralPrimitiveCreationSpec MapCircle(CircleItemRequest request)
    {
        return new GeneralPrimitiveCreationSpec
        {
            Kind = GeneralPrimitiveKind.Circle,
            CenterX = request.CenterX,
            CenterY = request.CenterY,
            CenterZ = request.CenterZ,
            Radius = request.Radius,
            NormalX = request.NormalX,
            NormalY = request.NormalY,
            NormalZ = request.NormalZ,
            Name = request.Name
        };
    }

    private static GeneralPrimitiveCreationSpec MapEllipse(EllipseItemRequest request)
    {
        return new GeneralPrimitiveCreationSpec
        {
            Kind = GeneralPrimitiveKind.Ellipse,
            CenterX = request.CenterX,
            CenterY = request.CenterY,
            CenterZ = request.CenterZ,
            RadiusX = request.RadiusX,
            RadiusY = request.RadiusY,
            NormalX = request.NormalX,
            NormalY = request.NormalY,
            NormalZ = request.NormalZ,
            XAxisX = request.XAxisX,
            XAxisY = request.XAxisY,
            XAxisZ = request.XAxisZ,
            Name = request.Name
        };
    }

    private static GeneralPrimitiveCreationSpec MapPolyline(PolylineItemRequest request)
    {
        return new GeneralPrimitiveCreationSpec
        {
            Kind = GeneralPrimitiveKind.Polyline,
            Points = request.Points.Select(MapPointSpec).ToList(),
            Closed = request.Closed,
            Name = request.Name
        };
    }

    private static GeneralPrimitiveCreationSpec MapNurbsCurve(NurbsCurveItemRequest request)
    {
        return new GeneralPrimitiveCreationSpec
        {
            Kind = GeneralPrimitiveKind.NurbsCurve,
            Points = request.ControlPoints.Select(MapPointSpec).ToList(),
            Degree = request.Degree,
            Closed = request.Closed,
            Name = request.Name
        };
    }

    private static GeneralPrimitiveCreationSpec MapSphere(SphereItemRequest request)
    {
        return new GeneralPrimitiveCreationSpec
        {
            Kind = GeneralPrimitiveKind.Sphere,
            CenterX = request.CenterX,
            CenterY = request.CenterY,
            CenterZ = request.CenterZ,
            Radius = request.Radius,
            Name = request.Name
        };
    }

    private static GeneralPrimitiveCreationSpec MapCone(ConeItemRequest request)
    {
        return new GeneralPrimitiveCreationSpec
        {
            Kind = GeneralPrimitiveKind.Cone,
            BaseX = request.BaseX,
            BaseY = request.BaseY,
            BaseZ = request.BaseZ,
            Radius = request.Radius,
            Height = request.Height,
            AxisX = request.AxisX,
            AxisY = request.AxisY,
            AxisZ = request.AxisZ,
            CapBottom = request.CapBottom,
            Name = request.Name
        };
    }

    private static GeneralPrimitiveCreationSpec MapCylinder(CylinderItemRequest request)
    {
        return new GeneralPrimitiveCreationSpec
        {
            Kind = GeneralPrimitiveKind.Cylinder,
            BaseX = request.BaseX,
            BaseY = request.BaseY,
            BaseZ = request.BaseZ,
            Radius = request.Radius,
            Height = request.Height,
            AxisX = request.AxisX,
            AxisY = request.AxisY,
            AxisZ = request.AxisZ,
            CapBottom = request.CapBottom,
            CapTop = request.CapTop,
            Name = request.Name
        };
    }

    private static GeneralPrimitiveCreationSpec MapEllipsoid(EllipsoidItemRequest request)
    {
        return new GeneralPrimitiveCreationSpec
        {
            Kind = GeneralPrimitiveKind.Ellipsoid,
            CenterX = request.CenterX,
            CenterY = request.CenterY,
            CenterZ = request.CenterZ,
            RadiusX = request.RadiusX,
            RadiusY = request.RadiusY,
            RadiusZ = request.RadiusZ,
            NormalX = request.NormalX,
            NormalY = request.NormalY,
            NormalZ = request.NormalZ,
            XAxisX = request.XAxisX,
            XAxisY = request.XAxisY,
            XAxisZ = request.XAxisZ,
            Name = request.Name
        };
    }

    private static GeneralPrimitiveCreationSpec MapCapsule(CapsuleItemRequest request)
    {
        return new GeneralPrimitiveCreationSpec
        {
            Kind = GeneralPrimitiveKind.Capsule,
            BaseX = request.StartX,
            BaseY = request.StartY,
            BaseZ = request.StartZ,
            EndX = request.EndX,
            EndY = request.EndY,
            EndZ = request.EndZ,
            Radius = request.Radius,
            Tolerance = request.Tolerance,
            Name = request.Name
        };
    }

    private static GeneralPrimitiveCreationSpec MapTorus(TorusItemRequest request)
    {
        return new GeneralPrimitiveCreationSpec
        {
            Kind = GeneralPrimitiveKind.Torus,
            CenterX = request.CenterX,
            CenterY = request.CenterY,
            CenterZ = request.CenterZ,
            MajorRadius = request.MajorRadius,
            MinorRadius = request.MinorRadius,
            NormalX = request.NormalX,
            NormalY = request.NormalY,
            NormalZ = request.NormalZ,
            XAxisX = request.XAxisX,
            XAxisY = request.XAxisY,
            XAxisZ = request.XAxisZ,
            Name = request.Name
        };
    }

    private static GeneralPrimitiveCreationSpec MapRoundedBox(RoundedBoxItemRequest request)
    {
        return new GeneralPrimitiveCreationSpec
        {
            Kind = GeneralPrimitiveKind.RoundedBox,
            CenterX = request.CenterX,
            CenterY = request.CenterY,
            CenterZ = request.CenterZ,
            Width = request.Width,
            Depth = request.Depth,
            Height = request.Height,
            Radius = request.Radius,
            NormalX = request.NormalX,
            NormalY = request.NormalY,
            NormalZ = request.NormalZ,
            XAxisX = request.XAxisX,
            XAxisY = request.XAxisY,
            XAxisZ = request.XAxisZ,
            Tolerance = request.Tolerance,
            Name = request.Name
        };
    }

    private static GeneralPrimitiveCreationSpec MapRaisedStrip(RaisedStripItemRequest request)
    {
        return new GeneralPrimitiveCreationSpec
        {
            Kind = GeneralPrimitiveKind.RaisedStrip,
            BaseX = request.StartX,
            BaseY = request.StartY,
            BaseZ = request.StartZ,
            EndX = request.EndX,
            EndY = request.EndY,
            EndZ = request.EndZ,
            Width = request.Width,
            Height = request.Height,
            UpX = request.UpX,
            UpY = request.UpY,
            UpZ = request.UpZ,
            Name = request.Name
        };
    }

    private static GeneralPrimitiveCreationSpec MapTaperedBox(TaperedBoxItemRequest request)
    {
        return new GeneralPrimitiveCreationSpec
        {
            Kind = GeneralPrimitiveKind.TaperedBox,
            BaseX = request.StartX,
            BaseY = request.StartY,
            BaseZ = request.StartZ,
            EndX = request.EndX,
            EndY = request.EndY,
            EndZ = request.EndZ,
            StartWidth = request.StartWidth,
            StartDepth = request.StartDepth,
            EndWidth = request.EndWidth,
            EndDepth = request.EndDepth,
            UpX = request.UpX,
            UpY = request.UpY,
            UpZ = request.UpZ,
            Name = request.Name
        };
    }

    private static GeneralPrimitivePointSpec MapPointSpec(GeneralPrimitivePointRequest request)
    {
        return new GeneralPrimitivePointSpec
        {
            X = request.X,
            Y = request.Y,
            Z = request.Z
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

    private static GeometryObjectAttributesSpec MapReferenceImageObjectModelingAttributes(
        GeometryCreationCommonOptions? options,
        string primitiveKind,
        string stage,
        string role)
    {
        GeometryObjectAttributesSpec attributes = MapAttributes(options);
        attributes.UserText.TryAdd("mcp.capability", "reference-image-object-modeling");
        attributes.UserText.TryAdd("mcp.modeling.stage", stage);
        attributes.UserText.TryAdd("mcp.primitive.kind", primitiveKind);
        attributes.UserText.TryAdd("mcp.object.role", role);
        return attributes;
    }
}
