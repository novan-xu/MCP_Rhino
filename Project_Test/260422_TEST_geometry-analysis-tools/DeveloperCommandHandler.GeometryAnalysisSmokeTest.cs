extern alias rhinocommon;

using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using MCP_Rhino.Server.Tools.Analysis;
using BoundingBox = rhinocommon::Rhino.Geometry.BoundingBox;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using Curve = rhinocommon::Rhino.Geometry.Curve;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using Surface = rhinocommon::Rhino.Geometry.Surface;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    partial void RegisterGeometryAnalysisHandlers()
    {
        _extensionHandlers["geometry-analysis-smoke-test"] = HandleGeometryAnalysisSmokeTest;
    }

    private bool HandleGeometryAnalysisSmokeTest(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: dotnet run --project src/MCP_Rhino.Server -- geometry-analysis-smoke-test <3dm-file-path>");
            Environment.ExitCode = 1;
            return true;
        }

        try
        {
            string filePath = Path.GetFullPath(args[1]);
            if (!File.Exists(filePath))
            {
                Console.Error.WriteLine($"Smoke test source file was not found: {filePath}");
                Environment.ExitCode = 1;
                return true;
            }

            if (MCP_Rhino.Server.Infrastructure.Plugin.McpRhinoPlugin.Instance is null)
            {
                RunCliFallbackGeometryAnalysisSmoke(filePath);
            }
            else
            {
                RunLiveGeometryAnalysisSmoke(filePath);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Geometry analysis smoke test failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunCliFallbackGeometryAnalysisSmoke(string filePath)
    {
        var checkpoints = new List<string>();
        Guid anyObjectId = Guid.NewGuid();

        var metricsTool = new GetObjectMetricsInLiveTool(_geometryMetricsService);
        var distancesTool = new MeasureDistancesInLiveTool(_geometryMetricsService);
        var anglesTool = new MeasureAnglesInLiveTool(_geometryMetricsService);
        var framesTool = new GetGeometryFramesInLiveTool(_geometryMetricsService);
        var curvatureTool = new GetCurvatureSamplesInLiveTool(_geometryCurvatureService);
        var continuityTool = new CheckContinuityInLiveTool(_geometryCurvatureService);
        var massTool = new GetMassPropertiesInLiveTool(_geometryIntersectionService);
        var intersectTool = new IntersectObjectsInLiveTool(_geometryIntersectionService);
        var closestTool = new GetClosestPointsInLiveTool(_geometryIntersectionService);
        var contourTool = new GetContourCurvesInLiveTool(_geometryIntersectionService);

        RequireGeometryAnalysisFailureWithMessage(
            metricsTool.GetObjectMetricsInLive(filePath, new List<Guid> { anyObjectId }),
            "LIVE_RHINO_REQUIRED",
            "GetObjectMetricsInLive should require live Rhino.");
        checkpoints.Add("GetObjectMetricsInLive rejected in CLI fallback");

        RequireGeometryAnalysisFailureWithMessage(
            distancesTool.MeasureDistancesInLive(filePath, new List<MeasureDistanceEntryRequest>
            {
                new()
                {
                    EntryId = "distance-cli",
                    From = CreatePointReference(0d, 0d, 0d),
                    To = new GeometryAnalysisReferenceRequest { ObjectId = anyObjectId }
                }
            }),
            "LIVE_RHINO_REQUIRED",
            "MeasureDistancesInLive should require live Rhino.");
        checkpoints.Add("MeasureDistancesInLive rejected in CLI fallback");

        RequireGeometryAnalysisFailureWithMessage(
            anglesTool.MeasureAnglesInLive(filePath, new List<MeasureAngleEntryRequest>
            {
                new()
                {
                    EntryId = "angle-cli",
                    Mode = GeometryAngleMeasurementMode.TwoVectors,
                    FirstVector = new GeometryAnalysisVectorRequest { X = 1d, Y = 0d, Z = 0d },
                    SecondVector = new GeometryAnalysisVectorRequest { X = 0d, Y = 1d, Z = 0d }
                }
            }),
            "LIVE_RHINO_REQUIRED",
            "MeasureAnglesInLive should require live Rhino.");
        checkpoints.Add("MeasureAnglesInLive rejected in CLI fallback");

        RequireGeometryAnalysisFailureWithMessage(
            framesTool.GetGeometryFramesInLive(filePath, new List<GeometryFrameEntryRequest>
            {
                new()
                {
                    EntryId = "frame-cli",
                    ObjectId = anyObjectId,
                    Kind = GeometryFrameKind.SurfaceFrame,
                    U = 0.5,
                    V = 0.5
                }
            }),
            "LIVE_RHINO_REQUIRED",
            "GetGeometryFramesInLive should require live Rhino.");
        checkpoints.Add("GetGeometryFramesInLive rejected in CLI fallback");

        RequireGeometryAnalysisFailureWithMessage(
            curvatureTool.GetCurvatureSamplesInLive(filePath, new List<CurvatureSampleEntryRequest>
            {
                new()
                {
                    EntryId = "curvature-cli",
                    ObjectId = anyObjectId,
                    Mode = GeometryCurvatureSamplingMode.EvenByCount,
                    SampleCount = 4
                }
            }),
            "LIVE_RHINO_REQUIRED",
            "GetCurvatureSamplesInLive should require live Rhino.");
        checkpoints.Add("GetCurvatureSamplesInLive rejected in CLI fallback");

        RequireGeometryAnalysisFailureWithMessage(
            continuityTool.CheckContinuityInLive(filePath, new List<ContinuityCheckEntryRequest>
            {
                new()
                {
                    EntryId = "continuity-cli",
                    FirstObjectId = anyObjectId,
                    SecondObjectId = Guid.NewGuid(),
                    FirstEdgeIndex = 0,
                    SecondEdgeIndex = 0
                }
            }),
            "LIVE_RHINO_REQUIRED",
            "CheckContinuityInLive should require live Rhino.");
        checkpoints.Add("CheckContinuityInLive rejected in CLI fallback");

        RequireGeometryAnalysisFailureWithMessage(
            massTool.GetMassPropertiesInLive(filePath, new List<Guid> { anyObjectId }, GeometryMassKind.Area),
            "LIVE_RHINO_REQUIRED",
            "GetMassPropertiesInLive should require live Rhino.");
        checkpoints.Add("GetMassPropertiesInLive rejected in CLI fallback");

        RequireGeometryAnalysisFailureWithMessage(
            intersectTool.IntersectObjectsInLive(filePath, new List<GeometryIntersectionEntryRequest>
            {
                new()
                {
                    EntryId = "intersection-cli",
                    Kind = GeometryIntersectionKind.CurveSurface,
                    First = CreateLineReference(new Point3d(-1d, 0d, 0d), new Point3d(1d, 0d, 0d)),
                    Second = new GeometryAnalysisReferenceRequest { ObjectId = anyObjectId }
                }
            }),
            "LIVE_RHINO_REQUIRED",
            "IntersectObjectsInLive should require live Rhino.");
        checkpoints.Add("IntersectObjectsInLive rejected in CLI fallback");

        RequireGeometryAnalysisFailureWithMessage(
            closestTool.GetClosestPointsInLive(filePath, new List<GeometryClosestPointEntryRequest>
            {
                new()
                {
                    EntryId = "closest-cli",
                    Source = CreatePointReference(0d, 0d, 0d),
                    Target = new GeometryAnalysisReferenceRequest { ObjectId = anyObjectId }
                }
            }),
            "LIVE_RHINO_REQUIRED",
            "GetClosestPointsInLive should require live Rhino.");
        checkpoints.Add("GetClosestPointsInLive rejected in CLI fallback");

        RequireGeometryAnalysisFailureWithMessage(
            contourTool.GetContourCurvesInLive(filePath, new List<GeometryContourEntryRequest>
            {
                new()
                {
                    EntryId = "contour-cli",
                    ObjectId = anyObjectId,
                    StartX = 0d,
                    StartY = 0d,
                    StartZ = -1d,
                    EndX = 0d,
                    EndY = 0d,
                    EndZ = 1d,
                    Interval = 0.5d
                }
            }),
            "LIVE_RHINO_REQUIRED",
            "GetContourCurvesInLive should require live Rhino.");
        checkpoints.Add("GetContourCurvesInLive rejected in CLI fallback");

        Console.WriteLine("Geometry analysis smoke test completed successfully (CLI fallback mode).");
        Console.WriteLine($"Source: {filePath}");
        foreach (string checkpoint in checkpoints)
        {
            Console.WriteLine($"- {checkpoint}");
        }
    }

    private void RunLiveGeometryAnalysisSmoke(string filePath)
    {
        var checkpoints = new List<string>();
        GeometryAnalysisDocumentSnapshot before = CaptureGeometryAnalysisSnapshot(filePath);
        GeometryAnalysisFixtureContext fixture = ReadGeometryAnalysisFixtureContext(filePath);

        var metricsTool = new GetObjectMetricsInLiveTool(_geometryMetricsService);
        var distancesTool = new MeasureDistancesInLiveTool(_geometryMetricsService);
        var anglesTool = new MeasureAnglesInLiveTool(_geometryMetricsService);
        var framesTool = new GetGeometryFramesInLiveTool(_geometryMetricsService);
        var curvatureTool = new GetCurvatureSamplesInLiveTool(_geometryCurvatureService);
        var continuityTool = new CheckContinuityInLiveTool(_geometryCurvatureService);
        var massTool = new GetMassPropertiesInLiveTool(_geometryIntersectionService);
        var intersectTool = new IntersectObjectsInLiveTool(_geometryIntersectionService);
        var closestTool = new GetClosestPointsInLiveTool(_geometryIntersectionService);
        var contourTool = new GetContourCurvesInLiveTool(_geometryIntersectionService);

        GetObjectMetricsInLiveResponse metrics = RequireGeometryAnalysisSuccess(
            metricsTool.GetObjectMetricsInLive(filePath, new List<Guid> { fixture.SurfaceId, fixture.BrepIds[0] }),
            "GetObjectMetricsInLive");
        RequireGeometryAnalysis(metrics.SucceededCount == 2, "GetObjectMetricsInLive should succeed for the target surface and brep.");
        RequireGeometryAnalysis(metrics.Results.Any(result => result.ObjectId == fixture.SurfaceId && result.Area > 0d), "Surface metrics should include positive area.");
        checkpoints.Add("GetObjectMetricsInLive ok");

        GetObjectMetricsInLiveResponse metricsMixed = RequireGeometryAnalysisSuccess(
            metricsTool.GetObjectMetricsInLive(filePath, new List<Guid> { fixture.SurfaceId, Guid.NewGuid() }),
            "GetObjectMetricsInLive(mixed)");
        RequireGeometryAnalysis(metricsMixed.SucceededCount == 1 && metricsMixed.FailedCount == 1, "Mixed metrics call should preserve per-object failure semantics.");
        checkpoints.Add("GetObjectMetricsInLive per-object failure ok");

        MeasureDistancesInLiveResponse distances = RequireGeometryAnalysisSuccess(
            distancesTool.MeasureDistancesInLive(filePath, new List<MeasureDistanceEntryRequest>
            {
                new()
                {
                    EntryId = "distance-ok",
                    From = CreatePointReference(fixture.SurfacePoint.X, fixture.SurfacePoint.Y, fixture.SurfacePoint.Z),
                    To = new GeometryAnalysisReferenceRequest { ObjectId = fixture.SurfaceId }
                },
                new()
                {
                    EntryId = "distance-invalid",
                    From = CreatePointReference(double.NaN, 0d, 0d),
                    To = new GeometryAnalysisReferenceRequest { ObjectId = fixture.SurfaceId }
                }
            }),
            "MeasureDistancesInLive");
        RequireGeometryAnalysis(distances.SucceededCount == 1 && distances.FailedCount == 1, "MeasureDistancesInLive should support mixed success and per-entry failure.");
        RequireGeometryAnalysis((distances.Results.First(result => result.EntryId == "distance-ok").Distance ?? 1d) <= 1e-6, "Point on surface should measure near-zero distance.");
        checkpoints.Add("MeasureDistancesInLive ok");

        MeasureAnglesInLiveResponse angles = RequireGeometryAnalysisSuccess(
            anglesTool.MeasureAnglesInLive(filePath, new List<MeasureAngleEntryRequest>
            {
                new()
                {
                    EntryId = "angle-ok",
                    Mode = GeometryAngleMeasurementMode.TwoVectors,
                    FirstVector = new GeometryAnalysisVectorRequest { X = 1d, Y = 0d, Z = 0d },
                    SecondVector = new GeometryAnalysisVectorRequest { X = 0d, Y = 1d, Z = 0d }
                }
            }),
            "MeasureAnglesInLive");
        RequireGeometryAnalysis(angles.SucceededCount == 1, "MeasureAnglesInLive should succeed for orthogonal vectors.");
        RequireGeometryAnalysis(Math.Abs((angles.Results[0].AngleDegrees ?? 0d) - 90d) <= 1e-6, "Vector angle should be 90 degrees.");
        checkpoints.Add("MeasureAnglesInLive ok");

        GetGeometryFramesInLiveResponse frames = RequireGeometryAnalysisSuccess(
            framesTool.GetGeometryFramesInLive(filePath, new List<GeometryFrameEntryRequest>
            {
                new()
                {
                    EntryId = "frame-ok",
                    ObjectId = fixture.SurfaceId,
                    Kind = GeometryFrameKind.SurfaceFrame,
                    U = fixture.SurfaceU,
                    V = fixture.SurfaceV
                }
            }),
            "GetGeometryFramesInLive");
        RequireGeometryAnalysis(frames.SucceededCount == 1 && frames.Results[0].Origin is not null, "GetGeometryFramesInLive should return a valid surface frame.");
        checkpoints.Add("GetGeometryFramesInLive ok");

        GetCurvatureSamplesInLiveResponse curvature = RequireGeometryAnalysisSuccess(
            curvatureTool.GetCurvatureSamplesInLive(filePath, new List<CurvatureSampleEntryRequest>
            {
                new()
                {
                    EntryId = "curvature-ok",
                    ObjectId = fixture.SurfaceId,
                    Mode = GeometryCurvatureSamplingMode.EvenByCount,
                    SampleCount = 4
                }
            }),
            "GetCurvatureSamplesInLive");
        RequireGeometryAnalysis(curvature.SucceededCount == 1 && curvature.Results.Count == 4, "GetCurvatureSamplesInLive should return four samples.");
        checkpoints.Add("GetCurvatureSamplesInLive ok");

        GetCurvatureSamplesInLiveResponse curvatureWarning = RequireGeometryAnalysisSuccess(
            curvatureTool.GetCurvatureSamplesInLive(filePath, new List<CurvatureSampleEntryRequest>
            {
                new()
                {
                    EntryId = "curvature-warning",
                    ObjectId = fixture.SurfaceId,
                    Mode = GeometryCurvatureSamplingMode.EvenByCount,
                    SampleCount = 1001
                }
            }),
            "GetCurvatureSamplesInLive(warning)");
        RequireGeometryAnalysis(curvatureWarning.Warnings.Any(warning => warning.Code == "HIGH_SAMPLE_COUNT"), "Curvature sample warning should be emitted at 1001 samples.");
        checkpoints.Add("GetCurvatureSamplesInLive warning ok");

        RequireGeometryAnalysisFailure(
            curvatureTool.GetCurvatureSamplesInLive(filePath, new List<CurvatureSampleEntryRequest>
            {
                new()
                {
                    EntryId = "curvature-hard-fail",
                    ObjectId = fixture.SurfaceId,
                    Mode = GeometryCurvatureSamplingMode.EvenByCount,
                    SampleCount = 10001
                }
            }),
            "GetCurvatureSamplesInLive should hard-fail at 10001 samples.");
        checkpoints.Add("GetCurvatureSamplesInLive hard-fail ok");

        CheckContinuityInLiveResponse continuity = RequireGeometryAnalysisSuccess(
            continuityTool.CheckContinuityInLive(filePath, new List<ContinuityCheckEntryRequest>
            {
                new()
                {
                    EntryId = "continuity-ok",
                    FirstObjectId = fixture.SurfaceId,
                    SecondObjectId = fixture.SurfaceId,
                    FirstEdgeIndex = 0,
                    SecondEdgeIndex = 0,
                    TargetKind = GeometryContinuityKind.G2
                }
            }),
            "CheckContinuityInLive");
        RequireGeometryAnalysis(continuity.SucceededCount == 1 && continuity.Results[0].IsContinuous, "A surface edge compared to itself should satisfy continuity.");
        checkpoints.Add("CheckContinuityInLive ok");

        GetMassPropertiesInLiveResponse mass = RequireGeometryAnalysisSuccess(
            massTool.GetMassPropertiesInLive(filePath, new List<Guid> { fixture.SurfaceId }, GeometryMassKind.Area),
            "GetMassPropertiesInLive");
        RequireGeometryAnalysis(mass.SucceededCount == 1 && (mass.Results[0].Area ?? 0d) > 0d, "GetMassPropertiesInLive should return positive area.");
        checkpoints.Add("GetMassPropertiesInLive ok");

        Point3d lineStart = fixture.SurfacePoint - fixture.SurfaceNormal * fixture.SurfaceDiagonal;
        Point3d lineEnd = fixture.SurfacePoint + fixture.SurfaceNormal * fixture.SurfaceDiagonal;
        IntersectObjectsInLiveResponse intersections = RequireGeometryAnalysisSuccess(
            intersectTool.IntersectObjectsInLive(filePath, new List<GeometryIntersectionEntryRequest>
            {
                new()
                {
                    EntryId = "intersection-ok",
                    Kind = GeometryIntersectionKind.CurveSurface,
                    First = CreateLineReference(lineStart, lineEnd),
                    Second = new GeometryAnalysisReferenceRequest { ObjectId = fixture.SurfaceId }
                }
            }),
            "IntersectObjectsInLive");
        RequireGeometryAnalysis(intersections.SucceededCount == 1, "IntersectObjectsInLive should succeed for a line through the surface normal.");
        RequireGeometryAnalysis(
            intersections.Results[0].Points.Count + intersections.Results[0].OverlapCurves.Count + intersections.Results[0].Curves.Count > 0,
            "IntersectObjectsInLive should return at least one point or overlap result.");
        checkpoints.Add("IntersectObjectsInLive ok");

        Point3d sourcePoint = fixture.SurfacePoint + fixture.SurfaceNormal * (fixture.SurfaceDiagonal * 0.5d);
        GetClosestPointsInLiveResponse closest = RequireGeometryAnalysisSuccess(
            closestTool.GetClosestPointsInLive(filePath, new List<GeometryClosestPointEntryRequest>
            {
                new()
                {
                    EntryId = "closest-ok",
                    Source = CreatePointReference(sourcePoint.X, sourcePoint.Y, sourcePoint.Z),
                    Target = new GeometryAnalysisReferenceRequest { ObjectId = fixture.SurfaceId }
                }
            }),
            "GetClosestPointsInLive");
        RequireGeometryAnalysis(closest.SucceededCount == 1 && (closest.Results[0].Distance ?? 0d) >= 0d, "GetClosestPointsInLive should return a valid distance.");
        checkpoints.Add("GetClosestPointsInLive ok");

        GetContourCurvesInLiveResponse contour = RequireGeometryAnalysisSuccess(
            contourTool.GetContourCurvesInLive(filePath, new List<GeometryContourEntryRequest>
            {
                new()
                {
                    EntryId = "contour-ok",
                    ObjectId = fixture.BrepIds[0],
                    StartX = fixture.BrepContourStart.X,
                    StartY = fixture.BrepContourStart.Y,
                    StartZ = fixture.BrepContourStart.Z,
                    EndX = fixture.BrepContourEnd.X,
                    EndY = fixture.BrepContourEnd.Y,
                    EndZ = fixture.BrepContourEnd.Z,
                    Interval = fixture.BrepContourInterval
                }
            }),
            "GetContourCurvesInLive");
        RequireGeometryAnalysis(contour.SucceededCount == 1 && contour.Results[0].Curves.Count > 0, "GetContourCurvesInLive should return at least one contour curve.");
        checkpoints.Add("GetContourCurvesInLive ok");

        GeometryAnalysisDocumentSnapshot after = CaptureGeometryAnalysisSnapshot(filePath);
        RequireGeometryAnalysis(after.ObjectCount == before.ObjectCount, "Geometry-analysis smoke must not change object count.");
        RequireGeometryAnalysis(after.LayerCount == before.LayerCount, "Geometry-analysis smoke must not change layer count.");
        RequireGeometryAnalysis(after.DocumentStringCount == before.DocumentStringCount, "Geometry-analysis smoke must not change document user string count.");
        RequireGeometryAnalysis(after.ObjectUserTextKeyCount == before.ObjectUserTextKeyCount, "Geometry-analysis smoke must not change object user-text key count.");
        RequireGeometryAnalysis(after.NextUndoRecordSerialNumber == before.NextUndoRecordSerialNumber, "Geometry-analysis smoke must not create a new undo entry.");
        RequireGeometryAnalysis(after.CurrentUndoRecordSerialNumber == before.CurrentUndoRecordSerialNumber, "Geometry-analysis smoke must not change current undo serial.");
        checkpoints.Add("Read-only state preserved");

        Console.WriteLine("Geometry analysis smoke test completed successfully (live Rhino mode).");
        Console.WriteLine($"Active file: {filePath}");
        Console.WriteLine($"SurfaceId: {fixture.SurfaceId}");
        Console.WriteLine($"BrepId: {fixture.BrepIds[0]}");
        foreach (string checkpoint in checkpoints)
        {
            Console.WriteLine($"- {checkpoint}");
        }
    }

    private GeometryAnalysisDocumentSnapshot CaptureGeometryAnalysisSnapshot(string filePath)
    {
        return RequireGeometryAnalysisSuccess(
            _liveRhinoDocumentAccessor.Execute(filePath, document =>
            {
                int objectUserTextKeyCount = 0;
                foreach (var rhinoObject in document.Objects)
                {
                    var userStrings = rhinoObject.Attributes.GetUserStrings();
                    if (userStrings is not null)
                    {
                        objectUserTextKeyCount += userStrings.Count;
                    }
                }

                return OperationResponse<GeometryAnalysisDocumentSnapshot>.Ok(new GeometryAnalysisDocumentSnapshot
                {
                    ObjectCount = document.Objects.Count,
                    LayerCount = Enumerable.Range(0, document.Layers.Count).Count(index => !document.Layers[index].IsDeleted),
                    DocumentStringCount = document.Strings.Count,
                    ObjectUserTextKeyCount = objectUserTextKeyCount,
                    NextUndoRecordSerialNumber = document.NextUndoRecordSerialNumber,
                    CurrentUndoRecordSerialNumber = document.CurrentUndoRecordSerialNumber
                });
            }),
            "CaptureGeometryAnalysisSnapshot");
    }

    private GeometryAnalysisFixtureContext ReadGeometryAnalysisFixtureContext(string filePath)
    {
        return RequireGeometryAnalysisSuccess(
            _liveRhinoDocumentAccessor.Execute(filePath, document =>
            {
                Guid? surfaceId = null;
                Surface? surface = null;
                var brepIds = new List<Guid>();
                Brep? firstBrep = null;

                foreach (var rhinoObject in document.Objects)
                {
                    if (surface is null && rhinoObject.Geometry is Surface candidateSurface)
                    {
                        surfaceId = rhinoObject.Attributes.ObjectId;
                        surface = candidateSurface;
                    }

                    if (rhinoObject.Geometry is Brep candidateBrep)
                    {
                        brepIds.Add(rhinoObject.Attributes.ObjectId);
                        firstBrep ??= candidateBrep;
                    }
                }

                if (!surfaceId.HasValue || surface is null)
                {
                    return OperationResponse<GeometryAnalysisFixtureContext>.Fail("The live fixture does not contain a surface object.");
                }

                if (brepIds.Count == 0 || firstBrep is null)
                {
                    return OperationResponse<GeometryAnalysisFixtureContext>.Fail("The live fixture does not contain a brep object.");
                }

                double surfaceU = surface.Domain(0).Mid;
                double surfaceV = surface.Domain(1).Mid;
                Point3d surfacePoint = surface.PointAt(surfaceU, surfaceV);
                var surfaceNormal = surface.NormalAt(surfaceU, surfaceV);
                surfaceNormal.Unitize();
                BoundingBox surfaceBoundingBox = surface.GetBoundingBox(true);
                double surfaceDiagonal = Math.Max(1d, surfaceBoundingBox.Diagonal.Length);

                BoundingBox brepBoundingBox = firstBrep.GetBoundingBox(true);
                Point3d brepCenter = brepBoundingBox.Center;
                double brepDiagonal = Math.Max(1d, brepBoundingBox.Diagonal.Length);

                return OperationResponse<GeometryAnalysisFixtureContext>.Ok(new GeometryAnalysisFixtureContext
                {
                    SurfaceId = surfaceId.Value,
                    BrepIds = brepIds,
                    SurfaceU = surfaceU,
                    SurfaceV = surfaceV,
                    SurfacePoint = surfacePoint,
                    SurfaceNormal = surfaceNormal,
                    SurfaceDiagonal = surfaceDiagonal,
                    BrepContourStart = new Point3d(brepCenter.X, brepCenter.Y, brepCenter.Z - brepDiagonal),
                    BrepContourEnd = new Point3d(brepCenter.X, brepCenter.Y, brepCenter.Z + brepDiagonal),
                    BrepContourInterval = Math.Max(0.1d, brepDiagonal / 4d)
                });
            }),
            "ReadGeometryAnalysisFixtureContext");
    }

    private static GeometryAnalysisReferenceRequest CreatePointReference(double x, double y, double z)
    {
        return new GeometryAnalysisReferenceRequest
        {
            Geometry = new GeometryCreationSpec
            {
                Primitive = GeometryPrimitiveKind.Point,
                X = x,
                Y = y,
                Z = z
            }
        };
    }

    private static GeometryAnalysisReferenceRequest CreateLineReference(Point3d start, Point3d end)
    {
        return new GeometryAnalysisReferenceRequest
        {
            Geometry = new GeometryCreationSpec
            {
                Primitive = GeometryPrimitiveKind.Line,
                StartX = start.X,
                StartY = start.Y,
                StartZ = start.Z,
                EndX = end.X,
                EndY = end.Y,
                EndZ = end.Z
            }
        };
    }

    private static T RequireGeometryAnalysisSuccess<T>(OperationResponse<T> response, string operationName)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{operationName} failed: {response.Message}");
        }

        return response.Data;
    }

    private static void RequireGeometryAnalysisFailure<T>(OperationResponse<T> response, string message)
    {
        if (response.Success)
        {
            throw new InvalidOperationException($"{message} Expected failure but got success.");
        }
    }

    private static void RequireGeometryAnalysisFailureWithMessage<T>(OperationResponse<T> response, string expectedMessage, string message)
    {
        if (response.Success)
        {
            throw new InvalidOperationException($"{message} Expected failure but got success.");
        }

        if (!string.Equals(response.Message, expectedMessage, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{message} Expected [{expectedMessage}], actual [{response.Message}].");
        }
    }

    private static void RequireGeometryAnalysis(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class GeometryAnalysisDocumentSnapshot
    {
        public int ObjectCount { get; set; }
        public int LayerCount { get; set; }
        public int DocumentStringCount { get; set; }
        public int ObjectUserTextKeyCount { get; set; }
        public uint NextUndoRecordSerialNumber { get; set; }
        public uint CurrentUndoRecordSerialNumber { get; set; }
    }

    private sealed class GeometryAnalysisFixtureContext
    {
        public Guid SurfaceId { get; set; }
        public IReadOnlyList<Guid> BrepIds { get; set; } = Array.Empty<Guid>();
        public double SurfaceU { get; set; }
        public double SurfaceV { get; set; }
        public Point3d SurfacePoint { get; set; }
        public rhinocommon::Rhino.Geometry.Vector3d SurfaceNormal { get; set; }
        public double SurfaceDiagonal { get; set; }
        public Point3d BrepContourStart { get; set; }
        public Point3d BrepContourEnd { get; set; }
        public double BrepContourInterval { get; set; }
    }
}
