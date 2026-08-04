extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using Curve = rhinocommon::Rhino.Geometry.Curve;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;
using Interval = rhinocommon::Rhino.Geometry.Interval;
using LoftType = rhinocommon::Rhino.Geometry.LoftType;
using Mesh = rhinocommon::Rhino.Geometry.Mesh;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using ObjectColorSource = rhinocommon::Rhino.DocObjects.ObjectColorSource;
using PipeCapMode = rhinocommon::Rhino.Geometry.PipeCapMode;
using Plane = rhinocommon::Rhino.Geometry.Plane;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using PolylineCurve = rhinocommon::Rhino.Geometry.PolylineCurve;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;
using RhinoCurveOffsetCornerStyle = rhinocommon::Rhino.Geometry.CurveOffsetCornerStyle;
using Surface = rhinocommon::Rhino.Geometry.Surface;
using SweepOneRail = rhinocommon::Rhino.Geometry.SweepOneRail;
using Vector3d = rhinocommon::Rhino.Geometry.Vector3d;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveCurveDerivedGeometryOperator : ILiveCurveDerivedGeometryOperator
{
    private const double NearZeroTolerance = 1e-12;

    private readonly ILiveRhinoDocumentAccessor _documentAccessor;

    public LiveCurveDerivedGeometryOperator(ILiveRhinoDocumentAccessor documentAccessor)
    {
        _documentAccessor = documentAccessor;
    }

    public OperationResponse<CurveDerivedGeometryResponse> CreateLofts(
        string filePath,
        IReadOnlyList<LoftSpec> entries,
        GeometryObjectAttributesSpec attributes)
    {
        return ExecuteCreateOperation(
            filePath,
            entries,
            attributes,
            CurveDerivedOperationKind.Loft,
            "MCP: CreateLofts",
            BuildLoft,
            entry => entry.Name,
            entry => entry.CurveObjectIds);
    }

    public OperationResponse<CurveDerivedGeometryResponse> CreateCurveExtrusions(
        string filePath,
        IReadOnlyList<CurveExtrusionSpec> entries,
        GeometryObjectAttributesSpec attributes)
    {
        return ExecuteCreateOperation(
            filePath,
            entries,
            attributes,
            CurveDerivedOperationKind.CurveExtrusion,
            "MCP: CreateCurveExtrusions",
            BuildCurveExtrusion,
            entry => entry.Name,
            entry => new[] { entry.CurveObjectId });
    }

    public OperationResponse<CurveDerivedGeometryResponse> CreateSweepOneRail(
        string filePath,
        IReadOnlyList<SweepOneRailSpec> entries,
        GeometryObjectAttributesSpec attributes)
    {
        return ExecuteCreateOperation(
            filePath,
            entries,
            attributes,
            CurveDerivedOperationKind.SweepOneRail,
            "MCP: CreateSweepOneRail",
            BuildSweepOneRail,
            entry => entry.Name,
            entry => new[] { entry.RailCurveObjectId }.Concat(entry.ProfileCurveObjectIds).ToArray());
    }

    public OperationResponse<CurveDerivedGeometryResponse> CreateCurveOffsets(
        string filePath,
        IReadOnlyList<CurveOffsetSpec> entries,
        GeometryObjectAttributesSpec attributes)
    {
        return ExecuteCreateOperation(
            filePath,
            entries,
            attributes,
            CurveDerivedOperationKind.CurveOffset,
            "MCP: CreateCurveOffsets",
            BuildCurveOffset,
            entry => entry.Name,
            entry => new[] { entry.CurveObjectId });
    }

    public OperationResponse<CurveDerivedGeometryResponse> CreatePipes(
        string filePath,
        IReadOnlyList<PipeSpec> entries,
        GeometryObjectAttributesSpec attributes)
    {
        return ExecuteCreateOperation(
            filePath,
            entries,
            attributes,
            CurveDerivedOperationKind.Pipe,
            "MCP: CreatePipes",
            BuildPipe,
            entry => entry.Name,
            entry => new[] { entry.CurveObjectId });
    }

    public OperationResponse<CurveDerivedGeometryResponse> CreateProfileExtrusionsFromPoints(
        string filePath,
        IReadOnlyList<ProfileExtrusionFromPointsSpec> entries,
        GeometryObjectAttributesSpec attributes)
    {
        return ExecuteCreateOperation(
            filePath,
            entries,
            attributes,
            CurveDerivedOperationKind.ProfileExtrusionFromPoints,
            "MCP: CreateProfileExtrusionsFromPoints",
            BuildProfileExtrusionFromPoints,
            entry => entry.Name,
            _ => Array.Empty<Guid>());
    }

    public OperationResponse<CurveDerivedGeometryResponse> CreateLoftsFromProfiles(
        string filePath,
        IReadOnlyList<LoftFromProfilesSpec> entries,
        GeometryObjectAttributesSpec attributes)
    {
        return ExecuteCreateOperation(
            filePath,
            entries,
            attributes,
            CurveDerivedOperationKind.LoftFromProfiles,
            "MCP: CreateLoftsFromProfiles",
            BuildLoftFromProfiles,
            entry => entry.Name,
            _ => Array.Empty<Guid>());
    }

    public OperationResponse<CurveDerivedGeometryResponse> CreatePipesFromPoints(
        string filePath,
        IReadOnlyList<PipeFromPointsSpec> entries,
        GeometryObjectAttributesSpec attributes)
    {
        return ExecuteCreateOperation(
            filePath,
            entries,
            attributes,
            CurveDerivedOperationKind.PipeFromPoints,
            "MCP: CreatePipesFromPoints",
            BuildPipeFromPoints,
            entry => entry.Name,
            _ => Array.Empty<Guid>());
    }

    public OperationResponse<CurveDerivedGeometryResponse> ProjectCurves(
        string filePath,
        IReadOnlyList<CurveProjectionSpec> entries,
        GeometryObjectAttributesSpec attributes)
    {
        return ExecuteCreateOperation(
            filePath,
            entries,
            attributes,
            CurveDerivedOperationKind.Projection,
            "MCP: ProjectCurves",
            BuildCurveProjection,
            entry => entry.Name,
            entry => new[] { entry.CurveObjectId }.Concat(entry.TargetObjectIds).ToArray());
    }

    public OperationResponse<CurveSplitPreviewResponse> PreviewSplitCurves(
        string filePath,
        IReadOnlyList<CurveSplitSpec> entries)
    {
        return _documentAccessor.Execute(filePath, document =>
        {
            var results = new List<CurveSplitPreviewEntryResponse>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                CurveSplitSpec entry = entries[i];
                OperationResponse<SplitPlan> splitPlan = BuildSplitPlan(document, entry);
                if (!splitPlan.Success || splitPlan.Data is null)
                {
                    results.Add(new CurveSplitPreviewEntryResponse
                    {
                        EntryIndex = i,
                        CurveObjectId = entry.CurveObjectId,
                        Success = false,
                        Message = splitPlan.Message,
                        SourceWillBeDeletedOnReplace = true
                    });
                    continue;
                }

                results.Add(new CurveSplitPreviewEntryResponse
                {
                    EntryIndex = i,
                    CurveObjectId = entry.CurveObjectId,
                    Success = true,
                    Message = "Split preview generated.",
                    SplitParameters = splitPlan.Data.Parameters,
                    ExpectedSegmentCount = splitPlan.Data.Segments.Count,
                    SourceWillBeDeletedOnReplace = true
                });
            }

            return OperationResponse<CurveSplitPreviewResponse>.Ok(new CurveSplitPreviewResponse
            {
                FilePath = filePath,
                RequestedCount = entries.Count,
                PreviewedCount = results.Count(result => result.Success),
                FailedCount = results.Count(result => !result.Success),
                Results = results
            }, "Curve split preview completed.");
        });
    }

    public OperationResponse<CurveDerivedGeometryResponse> CreateSplitCurveSegments(
        string filePath,
        IReadOnlyList<CurveSplitSpec> entries,
        GeometryObjectAttributesSpec attributes)
    {
        return ExecuteSplitOperation(filePath, entries, attributes, replaceSources: false);
    }

    public OperationResponse<CurveDerivedGeometryResponse> ReplaceSplitCurves(
        string filePath,
        IReadOnlyList<CurveSplitSpec> entries,
        GeometryObjectAttributesSpec attributes)
    {
        return ExecuteSplitOperation(filePath, entries, attributes, replaceSources: true);
    }

    private OperationResponse<CurveDerivedGeometryResponse> ExecuteCreateOperation<TSpec>(
        string filePath,
        IReadOnlyList<TSpec> entries,
        GeometryObjectAttributesSpec attributes,
        CurveDerivedOperationKind operation,
        string undoRecordName,
        Func<RhinoDoc, TSpec, OperationResponse<PreparedGeometrySet>> build,
        Func<TSpec, string> nameSelector,
        Func<TSpec, IReadOnlyList<Guid>> sourceIdsSelector)
    {
        return _documentAccessor.ExecuteWithUndo(filePath, undoRecordName, document =>
        {
            OperationResponse<int> layer = ResolveLayerIndex(document, attributes);
            if (!layer.Success)
            {
                return OperationResponse<(bool Mutated, CurveDerivedGeometryResponse Result)>.Fail(layer.Message);
            }

            var results = new List<CurveDerivedGeometryEntryResponse>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                TSpec entry = entries[i];
                OperationResponse<PreparedGeometrySet> prepared = build(document, entry);
                if (!prepared.Success || prepared.Data is null)
                {
                    results.Add(FailedEntry(i, sourceIdsSelector(entry), attributes.LayerFullPath, prepared.Message));
                    continue;
                }

                ObjectAttributes objectAttributes = CreateAttributes(layer.Data, attributes, nameSelector(entry), i, entries.Count);
                CurveDerivedGeometryEntryResponse result = AddGeometrySet(
                    document,
                    i,
                    attributes.LayerFullPath,
                    prepared.Data,
                    objectAttributes);
                results.Add(result);
            }

            if (results.Any(result => result.Success))
            {
                document.Views.Redraw();
            }

            return OperationResponse<(bool Mutated, CurveDerivedGeometryResponse Result)>.Ok(
                (results.Any(result => result.Success), BuildResponse(filePath, operation, results)),
                $"{operation} completed.");
        });
    }

    private OperationResponse<CurveDerivedGeometryResponse> ExecuteSplitOperation(
        string filePath,
        IReadOnlyList<CurveSplitSpec> entries,
        GeometryObjectAttributesSpec attributes,
        bool replaceSources)
    {
        CurveDerivedOperationKind operation = replaceSources
            ? CurveDerivedOperationKind.SplitReplace
            : CurveDerivedOperationKind.SplitSegments;
        string undoRecordName = replaceSources
            ? "MCP: ReplaceSplitCurves"
            : "MCP: CreateSplitCurveSegments";

        return _documentAccessor.ExecuteWithUndo(filePath, undoRecordName, document =>
        {
            OperationResponse<int> layer = ResolveLayerIndex(document, attributes);
            if (!layer.Success)
            {
                return OperationResponse<(bool Mutated, CurveDerivedGeometryResponse Result)>.Fail(layer.Message);
            }

            var results = new List<CurveDerivedGeometryEntryResponse>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                CurveSplitSpec entry = entries[i];
                OperationResponse<SplitPlan> splitPlan = BuildSplitPlan(document, entry);
                if (!splitPlan.Success || splitPlan.Data is null)
                {
                    results.Add(FailedEntry(i, new[] { entry.CurveObjectId }, attributes.LayerFullPath, splitPlan.Message));
                    continue;
                }

                var prepared = new PreparedGeometrySet(
                    new[] { entry.CurveObjectId },
                    splitPlan.Data.Segments.Cast<GeometryBase>().ToList(),
                    splitPlan.Data.Segments.Count);
                ObjectAttributes objectAttributes = CreateAttributes(layer.Data, attributes, entry.Name, i, entries.Count);
                CurveDerivedGeometryEntryResponse result = AddGeometrySet(
                    document,
                    i,
                    attributes.LayerFullPath,
                    prepared,
                    objectAttributes);

                if (replaceSources && result.Success)
                {
                    bool deleted = document.Objects.Delete(entry.CurveObjectId, true);
                    if (deleted)
                    {
                        result.DeletedSourceObjectIds = new[] { entry.CurveObjectId };
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "Split segments were created, but source curve deletion failed.";
                    }
                }

                results.Add(result);
            }

            if (results.Any(result => result.Success || result.CreatedObjectIds.Count > 0))
            {
                document.Views.Redraw();
            }

            return OperationResponse<(bool Mutated, CurveDerivedGeometryResponse Result)>.Ok(
                (results.Any(result => result.Success || result.CreatedObjectIds.Count > 0), BuildResponse(filePath, operation, results)),
                $"{operation} completed.");
        });
    }

    private OperationResponse<PreparedGeometrySet> BuildLoft(RhinoDoc document, LoftSpec spec)
    {
        if (spec.CurveObjectIds.Count < 2)
        {
            return OperationResponse<PreparedGeometrySet>.Fail("Loft requires at least two curve object ids.");
        }

        OperationResponse<List<Curve>> curves = ResolveCurves(document, spec.CurveObjectIds);
        if (!curves.Success || curves.Data is null)
        {
            return OperationResponse<PreparedGeometrySet>.Fail(curves.Message);
        }

        Brep[] breps = Brep.CreateFromLoft(
            curves.Data,
            Point3d.Unset,
            Point3d.Unset,
            ToLoftType(spec.LoftStyle),
            spec.Closed);
        return ToPrepared(spec.CurveObjectIds, breps, "Loft returned no Brep geometry.");
    }

    private OperationResponse<PreparedGeometrySet> BuildCurveExtrusion(RhinoDoc document, CurveExtrusionSpec spec)
    {
        OperationResponse<Curve> curve = ResolveCurve(document, spec.CurveObjectId);
        if (!curve.Success || curve.Data is null)
        {
            return OperationResponse<PreparedGeometrySet>.Fail(curve.Message);
        }

        if (!AllFinite(spec.VectorX, spec.VectorY, spec.VectorZ))
        {
            return OperationResponse<PreparedGeometrySet>.Fail("Extrusion vector must be finite.");
        }

        var vector = new Vector3d(spec.VectorX, spec.VectorY, spec.VectorZ);
        if (!vector.IsValid || vector.Length <= NearZeroTolerance)
        {
            return OperationResponse<PreparedGeometrySet>.Fail("Extrusion vector cannot be zero.");
        }

        Surface? surface = Surface.CreateExtrusion(curve.Data, vector);
        if (surface is null)
        {
            return OperationResponse<PreparedGeometrySet>.Fail("Rhino failed to create extrusion surface.");
        }

        GeometryBase geometry = surface;
        if (spec.Cap)
        {
            Brep? brep = surface.ToBrep();
            Brep? capped = brep?.CapPlanarHoles(document.ModelAbsoluteTolerance);
            if (capped is not null)
            {
                geometry = capped;
            }
        }

        return ToPrepared(new[] { spec.CurveObjectId }, new[] { geometry }, "Extrusion returned no geometry.");
    }

    private OperationResponse<PreparedGeometrySet> BuildSweepOneRail(RhinoDoc document, SweepOneRailSpec spec)
    {
        if (spec.ProfileCurveObjectIds.Count == 0)
        {
            return OperationResponse<PreparedGeometrySet>.Fail("SweepOneRail requires at least one profile curve id.");
        }

        OperationResponse<Curve> rail = ResolveCurve(document, spec.RailCurveObjectId);
        if (!rail.Success || rail.Data is null)
        {
            return OperationResponse<PreparedGeometrySet>.Fail(rail.Message);
        }

        OperationResponse<List<Curve>> profiles = ResolveCurves(document, spec.ProfileCurveObjectIds);
        if (!profiles.Success || profiles.Data is null)
        {
            return OperationResponse<PreparedGeometrySet>.Fail(profiles.Message);
        }

        var sweep = new SweepOneRail
        {
            SweepTolerance = document.ModelAbsoluteTolerance,
            AngleToleranceRadians = document.ModelAngleToleranceRadians
        };
        Brep[] breps = sweep.PerformSweep(rail.Data, profiles.Data);
        return ToPrepared(
            new[] { spec.RailCurveObjectId }.Concat(spec.ProfileCurveObjectIds).ToArray(),
            breps,
            "SweepOneRail returned no Brep geometry.");
    }

    private OperationResponse<PreparedGeometrySet> BuildProfileExtrusionFromPoints(
        RhinoDoc document,
        ProfileExtrusionFromPointsSpec spec)
    {
        OperationResponse<Curve> profile = BuildCurveFromPoints(spec.ProfilePoints, closed: true, "Profile extrusion");
        if (!profile.Success || profile.Data is null)
        {
            return OperationResponse<PreparedGeometrySet>.Fail(profile.Message);
        }

        if (!AllFinite(spec.VectorX, spec.VectorY, spec.VectorZ))
        {
            return OperationResponse<PreparedGeometrySet>.Fail("Extrusion vector must be finite.");
        }

        var vector = new Vector3d(spec.VectorX, spec.VectorY, spec.VectorZ);
        if (!vector.IsValid || vector.Length <= NearZeroTolerance)
        {
            return OperationResponse<PreparedGeometrySet>.Fail("Extrusion vector cannot be zero.");
        }

        Surface? surface = Surface.CreateExtrusion(profile.Data, vector);
        if (surface is null)
        {
            return OperationResponse<PreparedGeometrySet>.Fail("Rhino failed to create extrusion surface from profile points.");
        }

        GeometryBase geometry = surface;
        if (spec.Cap)
        {
            Brep? brep = surface.ToBrep();
            Brep? capped = brep?.CapPlanarHoles(document.ModelAbsoluteTolerance);
            if (capped is not null)
            {
                geometry = capped;
            }
        }

        return ToPrepared(Array.Empty<Guid>(), new[] { geometry }, "Profile extrusion returned no geometry.");
    }

    private OperationResponse<PreparedGeometrySet> BuildLoftFromProfiles(
        RhinoDoc document,
        LoftFromProfilesSpec spec)
    {
        if (spec.Profiles.Count < 2)
        {
            return OperationResponse<PreparedGeometrySet>.Fail("LoftFromProfiles requires at least two profiles.");
        }

        var curves = new List<Curve>(spec.Profiles.Count);
        foreach (CurvePointProfileSpec profileSpec in spec.Profiles)
        {
            OperationResponse<Curve> profile = BuildCurveFromPoints(profileSpec.Points, profileSpec.Closed, "Loft profile");
            if (!profile.Success || profile.Data is null)
            {
                return OperationResponse<PreparedGeometrySet>.Fail(profile.Message);
            }

            curves.Add(profile.Data);
        }

        Brep[] breps = Brep.CreateFromLoft(
            curves,
            Point3d.Unset,
            Point3d.Unset,
            ToLoftType(spec.LoftStyle),
            spec.ClosedLoft);
        return ToPrepared(Array.Empty<Guid>(), breps, "LoftFromProfiles returned no Brep geometry.");
    }

    private OperationResponse<PreparedGeometrySet> BuildPipeFromPoints(
        RhinoDoc document,
        PipeFromPointsSpec spec)
    {
        OperationResponse<Curve> rail = BuildCurveFromPoints(spec.Points, closed: false, "Pipe rail");
        if (!rail.Success || rail.Data is null)
        {
            return OperationResponse<PreparedGeometrySet>.Fail(rail.Message);
        }

        if (!IsFinite(spec.Radius) || spec.Radius <= NearZeroTolerance)
        {
            return OperationResponse<PreparedGeometrySet>.Fail("Pipe radius must be greater than zero.");
        }

        Brep[] breps = Brep.CreatePipe(
            rail.Data,
            spec.Radius,
            spec.LocalBlending,
            ToPipeCapMode(spec.CapStyle),
            spec.FitRail,
            document.ModelAbsoluteTolerance,
            document.ModelAngleToleranceRadians);
        return ToPrepared(Array.Empty<Guid>(), breps, "PipeFromPoints returned no Brep geometry.");
    }

    private OperationResponse<PreparedGeometrySet> BuildCurveOffset(RhinoDoc document, CurveOffsetSpec spec)
    {
        OperationResponse<Curve> curve = ResolveCurve(document, spec.CurveObjectId);
        if (!curve.Success || curve.Data is null)
        {
            return OperationResponse<PreparedGeometrySet>.Fail(curve.Message);
        }

        if (!AllFinite(
                spec.Distance,
                spec.PlaneOriginX,
                spec.PlaneOriginY,
                spec.PlaneOriginZ,
                spec.PlaneNormalX,
                spec.PlaneNormalY,
                spec.PlaneNormalZ))
        {
            return OperationResponse<PreparedGeometrySet>.Fail("Offset distance and plane values must be finite.");
        }

        if (Math.Abs(spec.Distance) <= NearZeroTolerance)
        {
            return OperationResponse<PreparedGeometrySet>.Fail("Offset distance cannot be zero.");
        }

        var plane = new Plane(
            new Point3d(spec.PlaneOriginX, spec.PlaneOriginY, spec.PlaneOriginZ),
            new Vector3d(spec.PlaneNormalX, spec.PlaneNormalY, spec.PlaneNormalZ));
        if (!plane.IsValid)
        {
            return OperationResponse<PreparedGeometrySet>.Fail("Offset plane is invalid.");
        }

        Curve[] curves = curve.Data.Offset(
            plane,
            spec.Distance,
            document.ModelAbsoluteTolerance,
            ToOffsetCornerStyle(spec.CornerStyle));
        return ToPrepared(new[] { spec.CurveObjectId }, curves, "Curve offset returned no curve geometry.");
    }

    private OperationResponse<PreparedGeometrySet> BuildPipe(RhinoDoc document, PipeSpec spec)
    {
        OperationResponse<Curve> curve = ResolveCurve(document, spec.CurveObjectId);
        if (!curve.Success || curve.Data is null)
        {
            return OperationResponse<PreparedGeometrySet>.Fail(curve.Message);
        }

        if (!IsFinite(spec.Radius) || spec.Radius <= NearZeroTolerance)
        {
            return OperationResponse<PreparedGeometrySet>.Fail("Pipe radius must be greater than zero.");
        }

        Brep[] breps = Brep.CreatePipe(
            curve.Data,
            spec.Radius,
            spec.LocalBlending,
            ToPipeCapMode(spec.CapStyle),
            spec.FitRail,
            document.ModelAbsoluteTolerance,
            document.ModelAngleToleranceRadians);
        return ToPrepared(new[] { spec.CurveObjectId }, breps, "Pipe returned no Brep geometry.");
    }

    private OperationResponse<PreparedGeometrySet> BuildCurveProjection(RhinoDoc document, CurveProjectionSpec spec)
    {
        OperationResponse<Curve> curve = ResolveCurve(document, spec.CurveObjectId);
        if (!curve.Success || curve.Data is null)
        {
            return OperationResponse<PreparedGeometrySet>.Fail(curve.Message);
        }

        if (spec.TargetObjectIds.Count == 0)
        {
            return OperationResponse<PreparedGeometrySet>.Fail("ProjectCurves requires at least one target object id.");
        }

        if (!AllFinite(spec.DirectionX, spec.DirectionY, spec.DirectionZ))
        {
            return OperationResponse<PreparedGeometrySet>.Fail("Projection direction must be finite.");
        }

        var direction = new Vector3d(spec.DirectionX, spec.DirectionY, spec.DirectionZ);
        if (!direction.IsValid || direction.Length <= NearZeroTolerance)
        {
            return OperationResponse<PreparedGeometrySet>.Fail("Projection direction cannot be zero.");
        }

        OperationResponse<TargetGeometrySet> targets = ResolveProjectionTargets(document, spec.TargetObjectIds);
        if (!targets.Success || targets.Data is null)
        {
            return OperationResponse<PreparedGeometrySet>.Fail(targets.Message);
        }

        var projected = new List<GeometryBase>();
        if (targets.Data.Breps.Count > 0)
        {
            projected.AddRange(Curve.ProjectToBrep(
                curve.Data,
                targets.Data.Breps,
                direction,
                document.ModelAbsoluteTolerance));
        }

        if (targets.Data.Meshes.Count > 0)
        {
            projected.AddRange(Curve.ProjectToMesh(
                curve.Data,
                targets.Data.Meshes,
                direction,
                document.ModelAbsoluteTolerance));
        }

        return ToPrepared(
            new[] { spec.CurveObjectId }.Concat(spec.TargetObjectIds).ToArray(),
            projected,
            "Curve projection returned no curve geometry.");
    }

    private OperationResponse<SplitPlan> BuildSplitPlan(RhinoDoc document, CurveSplitSpec spec)
    {
        OperationResponse<Curve> curve = ResolveCurve(document, spec.CurveObjectId);
        if (!curve.Success || curve.Data is null)
        {
            return OperationResponse<SplitPlan>.Fail(curve.Message);
        }

        OperationResponse<List<double>> parameters = ResolveSplitParameters(curve.Data, spec);
        if (!parameters.Success || parameters.Data is null)
        {
            return OperationResponse<SplitPlan>.Fail(parameters.Message);
        }

        Curve[] segments = curve.Data.Split(parameters.Data);
        if (segments.Length == 0)
        {
            return OperationResponse<SplitPlan>.Fail("Curve split returned no segments.");
        }

        return OperationResponse<SplitPlan>.Ok(new SplitPlan(parameters.Data, segments.ToList()));
    }

    private static OperationResponse<List<double>> ResolveSplitParameters(Curve curve, CurveSplitSpec spec)
    {
        var parameters = new List<double>();
        Interval domain = curve.Domain;

        foreach (double parameter in spec.Parameters)
        {
            if (!IsFinite(parameter))
            {
                return OperationResponse<List<double>>.Fail("Split parameters must be finite.");
            }

            parameters.Add(parameter);
        }

        foreach (CurveSplitPointSpec point in spec.Points)
        {
            var point3d = new Point3d(point.X, point.Y, point.Z);
            if (!point3d.IsValid)
            {
                return OperationResponse<List<double>>.Fail("Split points must be finite.");
            }

            bool closest = spec.PointTolerance > 0d
                ? curve.ClosestPoint(point3d, out double pointParameter, spec.PointTolerance)
                : curve.ClosestPoint(point3d, out pointParameter);
            if (!closest)
            {
                return OperationResponse<List<double>>.Fail($"Could not resolve split point ({point.X}, {point.Y}, {point.Z}) to a curve parameter.");
            }

            parameters.Add(pointParameter);
        }

        parameters = parameters
            .Where(parameter => parameter > domain.Min + NearZeroTolerance && parameter < domain.Max - NearZeroTolerance)
            .OrderBy(parameter => parameter)
            .ToList();

        var distinct = new List<double>();
        foreach (double parameter in parameters)
        {
            if (distinct.Count == 0 || Math.Abs(distinct[^1] - parameter) > NearZeroTolerance)
            {
                distinct.Add(parameter);
            }
        }

        return distinct.Count == 0
            ? OperationResponse<List<double>>.Fail("At least one split parameter inside the curve domain is required.")
            : OperationResponse<List<double>>.Ok(distinct);
    }

    private static OperationResponse<Curve> BuildCurveFromPoints(
        IReadOnlyList<CurveSplitPointSpec> points,
        bool closed,
        string label)
    {
        int minimumCount = closed ? 3 : 2;
        if (points.Count < minimumCount)
        {
            return OperationResponse<Curve>.Fail(
                closed
                    ? $"{label} requires at least three points."
                    : $"{label} requires at least two points.");
        }

        var point3ds = new List<Point3d>(points.Count + 1);
        foreach (CurveSplitPointSpec point in points)
        {
            if (!AllFinite(point.X, point.Y, point.Z))
            {
                return OperationResponse<Curve>.Fail($"{label} points must be finite.");
            }

            point3ds.Add(new Point3d(point.X, point.Y, point.Z));
        }

        if (closed && !point3ds[0].EpsilonEquals(point3ds[^1], NearZeroTolerance))
        {
            point3ds.Add(point3ds[0]);
        }

        var curve = new PolylineCurve(point3ds);
        return curve.IsValid
            ? OperationResponse<Curve>.Ok(curve)
            : OperationResponse<Curve>.Fail($"{label} points did not produce a valid polyline curve.");
    }

    private static OperationResponse<List<Curve>> ResolveCurves(RhinoDoc document, IReadOnlyList<Guid> objectIds)
    {
        var curves = new List<Curve>(objectIds.Count);
        foreach (Guid objectId in objectIds)
        {
            OperationResponse<Curve> curve = ResolveCurve(document, objectId);
            if (!curve.Success || curve.Data is null)
            {
                return OperationResponse<List<Curve>>.Fail(curve.Message);
            }

            curves.Add(curve.Data);
        }

        return OperationResponse<List<Curve>>.Ok(curves);
    }

    private static OperationResponse<Curve> ResolveCurve(RhinoDoc document, Guid objectId)
    {
        if (objectId == Guid.Empty)
        {
            return OperationResponse<Curve>.Fail("Curve object id cannot be empty.");
        }

        RhinoObject? rhinoObject = document.Objects.FindId(objectId);
        if (rhinoObject?.Geometry is not Curve curve)
        {
            return OperationResponse<Curve>.Fail($"Object is not a curve or was not found: {objectId}");
        }

        Curve? duplicate = curve.DuplicateCurve();
        return duplicate is null || !duplicate.IsValid
            ? OperationResponse<Curve>.Fail($"Could not duplicate valid curve geometry: {objectId}")
            : OperationResponse<Curve>.Ok(duplicate);
    }

    private static OperationResponse<TargetGeometrySet> ResolveProjectionTargets(RhinoDoc document, IReadOnlyList<Guid> objectIds)
    {
        var breps = new List<Brep>();
        var meshes = new List<Mesh>();

        foreach (Guid objectId in objectIds)
        {
            RhinoObject? rhinoObject = document.Objects.FindId(objectId);
            GeometryBase? geometry = rhinoObject?.Geometry;
            switch (geometry)
            {
                case Brep brep:
                    breps.Add(brep);
                    break;
                case Surface surface:
                    Brep? surfaceBrep = surface.ToBrep();
                    if (surfaceBrep is not null)
                    {
                        breps.Add(surfaceBrep);
                    }
                    break;
                case Mesh mesh:
                    meshes.Add(mesh);
                    break;
                default:
                    return OperationResponse<TargetGeometrySet>.Fail($"Projection target must be a Brep, surface, extrusion, or mesh: {objectId}");
            }
        }

        return breps.Count == 0 && meshes.Count == 0
            ? OperationResponse<TargetGeometrySet>.Fail("No projection target geometry was resolved.")
            : OperationResponse<TargetGeometrySet>.Ok(new TargetGeometrySet(breps, meshes));
    }

    private static OperationResponse<int> ResolveLayerIndex(RhinoDoc document, GeometryObjectAttributesSpec attributes)
    {
        OperationResponse validation = ValidateAttributes(attributes);
        if (!validation.Success)
        {
            return OperationResponse<int>.Fail(validation.Message);
        }

        int layerIndex = document.Layers.FindByFullPath(attributes.LayerFullPath, -1);
        return layerIndex < 0
            ? OperationResponse<int>.Fail($"Target layer was not found: {attributes.LayerFullPath}")
            : OperationResponse<int>.Ok(layerIndex);
    }

    private static OperationResponse ValidateAttributes(GeometryObjectAttributesSpec attributes)
    {
        if (string.IsNullOrWhiteSpace(attributes.LayerFullPath))
        {
            return OperationResponse.Fail("LayerFullPath is required for curve-derived geometry creation.");
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

    private static CurveDerivedGeometryEntryResponse AddGeometrySet(
        RhinoDoc document,
        int entryIndex,
        string layerFullPath,
        PreparedGeometrySet prepared,
        ObjectAttributes attributes)
    {
        var createdIds = new List<Guid>();
        var createdTypes = new List<string>();

        foreach (GeometryBase geometry in prepared.Geometry)
        {
            if (!geometry.IsValid)
            {
                continue;
            }

            Guid objectId = document.Objects.Add(geometry, attributes);
            if (objectId == Guid.Empty)
            {
                continue;
            }

            createdIds.Add(objectId);
            createdTypes.Add(geometry.GetType().Name);
        }

        return new CurveDerivedGeometryEntryResponse
        {
            EntryIndex = entryIndex,
            Success = createdIds.Count > 0,
            Message = createdIds.Count > 0 ? "Created geometry." : "No valid geometry was added.",
            SourceObjectIds = prepared.SourceObjectIds,
            CreatedObjectIds = createdIds,
            CreatedGeometryTypeNames = createdTypes,
            LayerFullPath = layerFullPath,
            SegmentCount = prepared.SegmentCount
        };
    }

    private static CurveDerivedGeometryResponse BuildResponse(
        string filePath,
        CurveDerivedOperationKind operation,
        IReadOnlyList<CurveDerivedGeometryEntryResponse> results)
    {
        return new CurveDerivedGeometryResponse
        {
            FilePath = filePath,
            Operation = operation,
            RequestedCount = results.Count,
            SucceededCount = results.Count(result => result.Success),
            FailedCount = results.Count(result => !result.Success),
            CreatedObjectCount = results.Sum(result => result.CreatedObjectIds.Count),
            DeletedSourceObjectCount = results.Sum(result => result.DeletedSourceObjectIds.Count),
            Results = results
        };
    }

    private static CurveDerivedGeometryEntryResponse FailedEntry(
        int entryIndex,
        IReadOnlyList<Guid> sourceObjectIds,
        string layerFullPath,
        string message)
    {
        return new CurveDerivedGeometryEntryResponse
        {
            EntryIndex = entryIndex,
            Success = false,
            Message = message,
            SourceObjectIds = sourceObjectIds,
            LayerFullPath = layerFullPath
        };
    }

    private static ObjectAttributes CreateAttributes(
        int layerIndex,
        GeometryObjectAttributesSpec attributesSpec,
        string itemName,
        int index,
        int count)
    {
        var attributes = new ObjectAttributes
        {
            LayerIndex = layerIndex,
            Name = ResolveName(attributesSpec.Name, itemName, index, count)
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

    private static OperationResponse<PreparedGeometrySet> ToPrepared(
        IReadOnlyList<Guid> sourceObjectIds,
        IEnumerable<GeometryBase> geometry,
        string emptyMessage)
    {
        List<GeometryBase> items = geometry.Where(item => item.IsValid).ToList();
        return items.Count == 0
            ? OperationResponse<PreparedGeometrySet>.Fail(emptyMessage)
            : OperationResponse<PreparedGeometrySet>.Ok(new PreparedGeometrySet(sourceObjectIds, items, items.Count));
    }

    private static LoftType ToLoftType(CurveLoftStyle style)
    {
        return style switch
        {
            CurveLoftStyle.Loose => LoftType.Loose,
            CurveLoftStyle.Tight => LoftType.Tight,
            CurveLoftStyle.Straight => LoftType.Straight,
            CurveLoftStyle.Uniform => LoftType.Uniform,
            _ => LoftType.Normal
        };
    }

    private static RhinoCurveOffsetCornerStyle ToOffsetCornerStyle(CurveOffsetCornerStyle style)
    {
        return style switch
        {
            CurveOffsetCornerStyle.None => RhinoCurveOffsetCornerStyle.None,
            CurveOffsetCornerStyle.Round => RhinoCurveOffsetCornerStyle.Round,
            CurveOffsetCornerStyle.Smooth => RhinoCurveOffsetCornerStyle.Smooth,
            CurveOffsetCornerStyle.Chamfer => RhinoCurveOffsetCornerStyle.Chamfer,
            _ => RhinoCurveOffsetCornerStyle.Sharp
        };
    }

    private static PipeCapMode ToPipeCapMode(CurvePipeCapStyle style)
    {
        return style switch
        {
            CurvePipeCapStyle.None => PipeCapMode.None,
            CurvePipeCapStyle.Round => PipeCapMode.Round,
            _ => PipeCapMode.Flat
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

    private sealed class PreparedGeometrySet
    {
        public PreparedGeometrySet(
            IReadOnlyList<Guid> sourceObjectIds,
            IReadOnlyList<GeometryBase> geometry,
            int segmentCount)
        {
            SourceObjectIds = sourceObjectIds;
            Geometry = geometry;
            SegmentCount = segmentCount;
        }

        public IReadOnlyList<Guid> SourceObjectIds { get; }
        public IReadOnlyList<GeometryBase> Geometry { get; }
        public int SegmentCount { get; }
    }

    private sealed class SplitPlan
    {
        public SplitPlan(IReadOnlyList<double> parameters, IReadOnlyList<Curve> segments)
        {
            Parameters = parameters;
            Segments = segments;
        }

        public IReadOnlyList<double> Parameters { get; }
        public IReadOnlyList<Curve> Segments { get; }
    }

    private sealed class TargetGeometrySet
    {
        public TargetGeometrySet(IReadOnlyList<Brep> breps, IReadOnlyList<Mesh> meshes)
        {
            Breps = breps;
            Meshes = meshes;
        }

        public IReadOnlyList<Brep> Breps { get; }
        public IReadOnlyList<Mesh> Meshes { get; }
    }
}
