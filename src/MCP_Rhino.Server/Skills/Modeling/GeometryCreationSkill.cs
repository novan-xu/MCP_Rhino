using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Skills.Modeling;

public sealed class GeometryCreationSkill
{
    private readonly RhinoGeometryCreationService _creationService;

    public GeometryCreationSkill(RhinoGeometryCreationService creationService)
    {
        _creationService = creationService;
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
}
