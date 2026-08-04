using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Skills.Modeling;

public sealed class ArchitecturalPrimitiveCreationSkill
{
    private readonly RhinoArchitecturalPrimitiveService _service;

    public ArchitecturalPrimitiveCreationSkill(RhinoArchitecturalPrimitiveService service)
    {
        _service = service;
    }

    public OperationResponse<ArchitecturalCreationResponse> CreateBoxes(CreateBoxesRequest request)
    {
        return _service.Create(
            request.FilePath,
            request.Items.Select(item => new ArchitecturalPrimitiveSpec
            {
                Kind = ArchitecturalPrimitiveKind.Box,
                Representation = item.Representation,
                OriginX = item.OriginX,
                OriginY = item.OriginY,
                OriginZ = item.OriginZ,
                SizeX = item.SizeX,
                SizeY = item.SizeY,
                SizeZ = item.SizeZ,
                Name = item.Name,
                Metadata = MapMetadata(request.Metadata, item.Metadata, "massing")
            }).ToList(),
            MapAttributes(request.Common, request.Metadata, request.AutoCreateLayers),
            "MCP: CreateArchitecturalBoxes");
    }

    public OperationResponse<ArchitecturalCreationResponse> CreateExtrusions(CreateExtrusionsRequest request)
    {
        return _service.Create(
            request.FilePath,
            request.Items.Select(item => new ArchitecturalPrimitiveSpec
            {
                Kind = ArchitecturalPrimitiveKind.Extrusion,
                Representation = item.Representation,
                Points = item.ProfilePoints.Select(MapPoint).ToList(),
                Height = item.Height,
                Cap = item.Cap,
                Name = item.Name,
                Metadata = MapMetadata(request.Metadata, item.Metadata, "extrusion")
            }).ToList(),
            MapAttributes(request.Common, request.Metadata, request.AutoCreateLayers),
            "MCP: CreateArchitecturalExtrusions");
    }

    public OperationResponse<ArchitecturalCreationResponse> CreatePlanarBreps(CreatePlanarBrepsRequest request)
    {
        return _service.Create(
            request.FilePath,
            request.Items.Select(item => new ArchitecturalPrimitiveSpec
            {
                Kind = ArchitecturalPrimitiveKind.PlanarBrep,
                Loops = item.Loops.Select(loop => (IReadOnlyList<ArchitecturalPointSpec>)loop.Points.Select(MapPoint).ToList()).ToList(),
                Name = item.Name,
                Metadata = MapMetadata(request.Metadata, item.Metadata, "planar-brep")
            }).ToList(),
            MapAttributes(request.Common, request.Metadata, request.AutoCreateLayers),
            "MCP: CreateArchitecturalPlanarBreps");
    }

    public OperationResponse<ArchitecturalCreationResponse> CreateSlabs(CreateSlabsRequest request)
    {
        return _service.Create(
            request.FilePath,
            request.Items.Select(item => new ArchitecturalPrimitiveSpec
            {
                Kind = ArchitecturalPrimitiveKind.Slab,
                Representation = item.Representation,
                Points = item.Footprint.Select(MapPoint).ToList(),
                Elevation = item.Elevation,
                Thickness = item.Thickness,
                Name = item.Name,
                Metadata = MapMetadata(request.Metadata, item.Metadata, "slab")
            }).ToList(),
            MapAttributes(request.Common, request.Metadata, request.AutoCreateLayers),
            "MCP: CreateArchitecturalSlabs");
    }

    public OperationResponse<ArchitecturalCreationResponse> CreateWalls(CreateWallsRequest request)
    {
        return _service.Create(
            request.FilePath,
            request.Items.Select(item => new ArchitecturalPrimitiveSpec
            {
                Kind = ArchitecturalPrimitiveKind.Wall,
                Points = item.Baseline.Select(MapPoint).ToList(),
                Height = item.Height,
                Thickness = item.Thickness,
                BaseElevation = item.BaseElevation,
                WallAlignment = item.Alignment,
                Name = item.Name,
                Metadata = MapMetadata(request.Metadata, item.Metadata, "wall")
            }).ToList(),
            MapAttributes(request.Common, request.Metadata, request.AutoCreateLayers),
            "MCP: CreateArchitecturalWalls");
    }

    public OperationResponse<ArchitecturalCreationResponse> CreateColumns(CreateColumnsRequest request)
    {
        return _service.Create(
            request.FilePath,
            request.Items.Select(item => new ArchitecturalPrimitiveSpec
            {
                Kind = ArchitecturalPrimitiveKind.Column,
                Representation = item.Representation,
                ProfileShape = item.ProfileShape,
                CenterX = item.CenterX,
                CenterY = item.CenterY,
                CenterZ = item.CenterZ,
                Radius = item.Radius,
                Width = item.Width,
                Depth = item.Depth,
                BaseElevation = item.BaseElevation,
                Height = item.Height,
                Name = item.Name,
                Metadata = MapMetadata(request.Metadata, item.Metadata, "column")
            }).ToList(),
            MapAttributes(request.Common, request.Metadata, request.AutoCreateLayers),
            "MCP: CreateArchitecturalColumns");
    }

    public OperationResponse<ArchitecturalCreationResponse> CreateBeams(CreateBeamsRequest request)
    {
        return _service.Create(
            request.FilePath,
            request.Items.Select(item => new ArchitecturalPrimitiveSpec
            {
                Kind = ArchitecturalPrimitiveKind.Beam,
                ProfileShape = item.ProfileShape,
                StartX = item.StartX,
                StartY = item.StartY,
                StartZ = item.StartZ,
                EndX = item.EndX,
                EndY = item.EndY,
                EndZ = item.EndZ,
                Radius = item.Radius,
                Width = item.Width,
                Depth = item.Depth,
                Name = item.Name,
                Metadata = MapMetadata(request.Metadata, item.Metadata, "beam")
            }).ToList(),
            MapAttributes(request.Common, request.Metadata, request.AutoCreateLayers),
            "MCP: CreateArchitecturalBeams");
    }

    internal static ArchitecturalObjectAttributesSpec MapAttributes(
        GeometryCreationCommonOptions? options,
        ArchitecturalMetadataRequest? metadata,
        bool autoCreateLayers)
    {
        GeometryCreationCommonOptions common = options ?? new GeometryCreationCommonOptions();
        return new ArchitecturalObjectAttributesSpec
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
            UserText = new Dictionary<string, string>(common.UserText, StringComparer.OrdinalIgnoreCase),
            Metadata = MapMetadata(metadata, null, string.Empty),
            AutoCreateLayer = autoCreateLayers
        };
    }

    internal static ArchitecturalMetadataSpec MapMetadata(
        ArchitecturalMetadataRequest? common,
        ArchitecturalMetadataRequest? item,
        string defaultCategory)
    {
        return new ArchitecturalMetadataSpec
        {
            Category = FirstNonEmpty(item?.Category, common?.Category, defaultCategory),
            LevelName = FirstNonEmpty(item?.LevelName, common?.LevelName, string.Empty),
            SystemName = FirstNonEmpty(item?.SystemName, common?.SystemName, string.Empty),
            SourceTag = FirstNonEmpty(item?.SourceTag, common?.SourceTag, string.Empty)
        };
    }

    private static ArchitecturalPointSpec MapPoint(ArchitecturalPointRequest request)
    {
        return new ArchitecturalPointSpec
        {
            X = request.X,
            Y = request.Y,
            Z = request.Z
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
}

