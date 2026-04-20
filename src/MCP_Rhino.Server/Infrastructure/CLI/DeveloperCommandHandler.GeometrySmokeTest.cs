using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using MCP_Rhino.Server.Tools.Geometry;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private bool HandleGeometrySmokeTest(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("用法: dotnet run --project src/MCP_Rhino.Server -- geometry-smoke-test <3dm文件路径>");
            System.Environment.ExitCode = 1;
            return true;
        }

        try
        {
            string sourceFilePath = Path.GetFullPath(args[1]);
            if (!File.Exists(sourceFilePath))
            {
                Console.Error.WriteLine($"Smoke test source file was not found: {sourceFilePath}");
                System.Environment.ExitCode = 1;
                return true;
            }

            string validationDirectory = Path.Combine(
                Directory.GetCurrentDirectory(),
                "_validation",
                "geometry-smoke-test");
            Directory.CreateDirectory(validationDirectory);

            string workingFilePath = Path.Combine(validationDirectory, "MCP_METtest.geometry-smoke.3dm");
            File.Copy(sourceFilePath, workingFilePath, overwrite: true);

            string layerFullPath = GetFirstActiveLayerFullPath(workingFilePath);
            int initialObjectCount = GetObjectCount(workingFilePath);
            var checkpoints = new List<string>();

            var createPointsTool = new CreatePointsTool(_geometryCreationSkill);
            var createLinesTool = new CreateLinesTool(_geometryCreationSkill);
            var createArcsTool = new CreateArcsTool(_geometryCreationSkill);
            var createSurfacesTool = new CreateSurfacesTool(_geometryCreationSkill);
            var previewTransformObjectsTool = new PreviewTransformObjectsTool(_geometryModificationSkill);
            var transformObjectsTool = new TransformObjectsTool(_geometryModificationSkill);
            var previewReplaceGeometryTool = new PreviewReplaceGeometryTool(_geometryModificationSkill);
            var replaceGeometryTool = new ReplaceGeometryTool(_geometryModificationSkill);
            var previewEditControlPointsTool = new PreviewEditControlPointsTool(_geometryModificationSkill);
            var editControlPointsTool = new EditControlPointsTool(_geometryModificationSkill);
            var previewDeleteObjectsTool = new PreviewDeleteObjectsTool(_geometryModificationSkill);
            var deleteObjectsTool = new DeleteObjectsTool(_geometryModificationSkill);

            OperationResponse<GeometryCreationResponse> pointCreate = createPointsTool.CreatePoints(
                workingFilePath,
                new List<PointItemRequest>
                {
                    new()
                    {
                        X = 1d,
                        Y = 2d,
                        Z = 3d
                    }
                },
                CreateCommonOptions(layerFullPath, "260420-smoke-point", "transform-point"));

            GeometryCreationResponse pointCreateData = RequireSuccess(pointCreate, "CreatePoints");
            Guid pointId = RequireSingleCreatedObject(pointCreateData, GeometryPrimitiveKind.Point);
            RequirePointLocation(workingFilePath, pointId, new Point3d(1d, 2d, 3d), "CreatePoints wrote the expected point.");
            checkpoints.Add($"CreatePoints ok: {pointId}");

            OperationResponse<GeometryCreationResponse> lineCreate = createLinesTool.CreateLines(
                workingFilePath,
                new List<LineItemRequest>
                {
                    new()
                    {
                        StartX = 0d,
                        StartY = 0d,
                        StartZ = 0d,
                        EndX = 10d,
                        EndY = 0d,
                        EndZ = 0d
                    }
                },
                CreateCommonOptions(layerFullPath, "260420-smoke-line", "replace-line"));

            GeometryCreationResponse lineCreateData = RequireSuccess(lineCreate, "CreateLines");
            Guid lineId = RequireSingleCreatedObject(lineCreateData, GeometryPrimitiveKind.Line);
            RequireLineGeometry(
                workingFilePath,
                lineId,
                new Point3d(0d, 0d, 0d),
                new Point3d(10d, 0d, 0d),
                "CreateLines wrote the expected line.");
            checkpoints.Add($"CreateLines ok: {lineId}");

            OperationResponse<GeometryCreationResponse> arcCreate = createArcsTool.CreateArcs(
                workingFilePath,
                new List<ArcItemRequest>
                {
                    new()
                    {
                        Mode = ArcConstructionMode.ThreePoint,
                        StartX = 0d,
                        StartY = 0d,
                        StartZ = 0d,
                        MidX = 5d,
                        MidY = 5d,
                        MidZ = 0d,
                        EndX = 10d,
                        EndY = 0d,
                        EndZ = 0d
                    }
                },
                CreateCommonOptions(layerFullPath, "260420-smoke-arc", "delete-arc"));

            GeometryCreationResponse arcCreateData = RequireSuccess(arcCreate, "CreateArcs");
            Guid arcId = RequireSingleCreatedObject(arcCreateData, GeometryPrimitiveKind.Arc);
            RequireArcExists(workingFilePath, arcId, "CreateArcs wrote the expected arc.");
            checkpoints.Add($"CreateArcs ok: {arcId}");

            OperationResponse<GeometryCreationResponse> surfaceCreate = createSurfacesTool.CreateSurfaces(
                workingFilePath,
                new List<SurfaceItemRequest>
                {
                    new()
                    {
                        Mode = SurfaceConstructionMode.FourCorners,
                        Corner0X = 0d,
                        Corner0Y = 0d,
                        Corner0Z = 0d,
                        Corner1X = 10d,
                        Corner1Y = 0d,
                        Corner1Z = 0d,
                        Corner2X = 10d,
                        Corner2Y = 10d,
                        Corner2Z = 0d,
                        Corner3X = 0d,
                        Corner3Y = 10d,
                        Corner3Z = 0d
                    }
                },
                CreateCommonOptions(layerFullPath, "260420-smoke-surface", "edit-surface"));

            GeometryCreationResponse surfaceCreateData = RequireSuccess(surfaceCreate, "CreateSurfaces");
            Guid surfaceId = RequireSingleCreatedObject(surfaceCreateData, GeometryPrimitiveKind.Surface);
            RequireSurfaceControlPoint(
                workingFilePath,
                surfaceId,
                0,
                0,
                new Point3d(0d, 0d, 0d),
                "CreateSurfaces wrote the expected NurbsSurface.");
            checkpoints.Add($"CreateSurfaces ok: {surfaceId}");

            Require(
                GetObjectCount(workingFilePath) == initialObjectCount + 4,
                $"Expected object count to increase by 4 after creation. Initial={initialObjectCount}, Current={GetObjectCount(workingFilePath)}");

            var transformSpec = new GeometryTransformSpec
            {
                Kind = GeometryTransformKind.Translate,
                VectorX = 5d,
                VectorY = 0d,
                VectorZ = 0d
            };

            OperationResponse<GeometryModificationPreviewResponse> transformPreview = previewTransformObjectsTool.PreviewTransformObjects(
                workingFilePath,
                transformSpec,
                confirmedObjectIds: new List<Guid> { pointId },
                objectTypes: new List<string> { "Surface" });

            GeometryModificationPreviewResponse transformPreviewData = RequireSuccess(transformPreview, "PreviewTransformObjects");
            Require(transformPreviewData.MatchedObjectCount == 1, "PreviewTransformObjects should match the explicit point target.");
            Require(transformPreviewData.Warnings.Count > 0, "PreviewTransformObjects should emit a warning when explicit ObjectIds override filters.");
            RequirePointLocation(
                workingFilePath,
                pointId,
                new Point3d(1d, 2d, 3d),
                "PreviewTransformObjects should not mutate file content.");
            checkpoints.Add("PreviewTransformObjects ok");

            OperationResponse<GeometryModificationResponse> transformApply = transformObjectsTool.TransformObjects(
                workingFilePath,
                transformSpec,
                confirmedObjectIds: new List<Guid> { pointId },
                objectTypes: new List<string> { "Surface" });

            GeometryModificationResponse transformApplyData = RequireSuccess(transformApply, "TransformObjects");
            Require(transformApplyData.UpdatedObjectCount == 1, "TransformObjects should update one object.");
            Require(
                transformApplyData.Warnings.Count > 0,
                "TransformObjects Apply should emit filter-override warning when explicit ObjectIds combined with filters.");
            RequirePointLocation(
                workingFilePath,
                pointId,
                new Point3d(6d, 2d, 3d),
                "TransformObjects should translate the point.");
            RequireUserText(
                workingFilePath,
                pointId,
                "geometry_smoke_role",
                "transform-point",
                "TransformObjects should preserve object user text.");
            checkpoints.Add("TransformObjects ok");

            var replaceEntries = new List<GeometryReplacementEntryRequest>
            {
                new()
                {
                    ObjectId = lineId,
                    Geometry = new GeometryCreationSpec
                    {
                        Primitive = GeometryPrimitiveKind.Line,
                        StartX = 0d,
                        StartY = 0d,
                        StartZ = 0d,
                        EndX = 0d,
                        EndY = 12d,
                        EndZ = 0d
                    }
                }
            };

            OperationResponse<GeometryModificationPreviewResponse> replacePreview = previewReplaceGeometryTool.PreviewReplaceGeometry(
                workingFilePath,
                replaceEntries);

            GeometryModificationPreviewResponse replacePreviewData = RequireSuccess(replacePreview, "PreviewReplaceGeometry");
            Require(replacePreviewData.MatchedObjectCount == 1, "PreviewReplaceGeometry should match the explicit line target.");
            RequireLineGeometry(
                workingFilePath,
                lineId,
                new Point3d(0d, 0d, 0d),
                new Point3d(10d, 0d, 0d),
                "PreviewReplaceGeometry should not mutate file content.");
            checkpoints.Add("PreviewReplaceGeometry ok");

            OperationResponse<GeometryModificationResponse> replaceApply = replaceGeometryTool.ReplaceGeometry(
                workingFilePath,
                replaceEntries);

            GeometryModificationResponse replaceApplyData = RequireSuccess(replaceApply, "ReplaceGeometry");
            Require(replaceApplyData.UpdatedObjectCount == 1, "ReplaceGeometry should update one object.");
            RequireLineGeometry(
                workingFilePath,
                lineId,
                new Point3d(0d, 0d, 0d),
                new Point3d(0d, 12d, 0d),
                "ReplaceGeometry should swap line geometry while preserving ObjectId.");
            RequireUserText(
                workingFilePath,
                lineId,
                "geometry_smoke_role",
                "replace-line",
                "ReplaceGeometry should preserve object user text.");
            checkpoints.Add("ReplaceGeometry ok");

            var controlPointEntries = new List<ControlPointEditEntryRequest>
            {
                new()
                {
                    ObjectId = surfaceId,
                    TargetMode = ControlPointTargetMode.SurfaceUV,
                    UIndex = 0,
                    VIndex = 0,
                    X = 0d,
                    Y = 0d,
                    Z = 5d
                }
            };

            OperationResponse<GeometryModificationPreviewResponse> editPreview = previewEditControlPointsTool.PreviewEditControlPoints(
                workingFilePath,
                controlPointEntries);

            GeometryModificationPreviewResponse editPreviewData = RequireSuccess(editPreview, "PreviewEditControlPoints");
            Require(editPreviewData.MatchedObjectCount == 1, "PreviewEditControlPoints should match the explicit surface target.");
            RequireSurfaceControlPoint(
                workingFilePath,
                surfaceId,
                0,
                0,
                new Point3d(0d, 0d, 0d),
                "PreviewEditControlPoints should not mutate file content.");
            checkpoints.Add("PreviewEditControlPoints ok");

            OperationResponse<GeometryModificationResponse> editApply = editControlPointsTool.EditControlPoints(
                workingFilePath,
                controlPointEntries);

            GeometryModificationResponse editApplyData = RequireSuccess(editApply, "EditControlPoints");
            Require(editApplyData.UpdatedObjectCount == 1, "EditControlPoints should update one object.");
            RequireSurfaceControlPoint(
                workingFilePath,
                surfaceId,
                0,
                0,
                new Point3d(0d, 0d, 5d),
                "EditControlPoints should update the target control point.");
            RequireUserText(
                workingFilePath,
                surfaceId,
                "geometry_smoke_role",
                "edit-surface",
                "EditControlPoints should preserve object user text.");
            checkpoints.Add("EditControlPoints ok");

            var deleteConditions = new List<UserAttributeConditionRequest>
            {
                new()
                {
                    Key = "geometry_smoke_role",
                    ExpectedValue = "delete-arc"
                }
            };

            OperationResponse<GeometryModificationPreviewResponse> deletePreview = previewDeleteObjectsTool.PreviewDeleteObjects(
                workingFilePath,
                userAttributeConditions: deleteConditions);

            GeometryModificationPreviewResponse deletePreviewData = RequireSuccess(deletePreview, "PreviewDeleteObjects");
            Require(deletePreviewData.MatchedObjectCount == 1, "PreviewDeleteObjects should match the smoke-test arc.");
            Require(ObjectExists(workingFilePath, arcId), "PreviewDeleteObjects should not remove the target object.");
            checkpoints.Add("PreviewDeleteObjects ok");

            OperationResponse<GeometryModificationResponse> deleteApply = deleteObjectsTool.DeleteObjects(
                workingFilePath,
                userAttributeConditions: deleteConditions);

            GeometryModificationResponse deleteApplyData = RequireSuccess(deleteApply, "DeleteObjects");
            Require(deleteApplyData.UpdatedObjectCount == 1, "DeleteObjects should remove one object.");
            Require(!ObjectExists(workingFilePath, arcId), "DeleteObjects should remove the target arc.");
            Require(
                GetObjectCount(workingFilePath) == initialObjectCount + 3,
                $"Expected net object count to increase by 3 after deleting one of four created objects. Initial={initialObjectCount}, Current={GetObjectCount(workingFilePath)}");
            checkpoints.Add("DeleteObjects ok");

            // --- Extended coverage: Rotate on existing pointId (currently at (6,2,3)) ---
            var rotateSpec = new GeometryTransformSpec
            {
                Kind = GeometryTransformKind.Rotate,
                CenterX = 0d,
                CenterY = 0d,
                CenterZ = 0d,
                AxisX = 0d,
                AxisY = 0d,
                AxisZ = 1d,
                AngleRadians = Math.PI / 2d
            };

            OperationResponse<GeometryModificationResponse> rotateApply = transformObjectsTool.TransformObjects(
                workingFilePath,
                rotateSpec,
                confirmedObjectIds: new List<Guid> { pointId });

            GeometryModificationResponse rotateApplyData = RequireSuccess(rotateApply, "TransformObjects-Rotate");
            Require(rotateApplyData.UpdatedObjectCount == 1, "Rotate should update one object.");
            RequirePointLocation(workingFilePath, pointId, new Point3d(-2d, 6d, 3d), "Rotate should rotate the point 90° around Z.");
            checkpoints.Add("TransformObjects (Rotate) ok");

            // --- Extended coverage: UniformScale on existing pointId (now at (-2,6,3)) ---
            var scaleSpec = new GeometryTransformSpec
            {
                Kind = GeometryTransformKind.UniformScale,
                CenterX = 0d,
                CenterY = 0d,
                CenterZ = 0d,
                ScaleFactor = 2d
            };

            OperationResponse<GeometryModificationResponse> scaleApply = transformObjectsTool.TransformObjects(
                workingFilePath,
                scaleSpec,
                confirmedObjectIds: new List<Guid> { pointId });

            GeometryModificationResponse scaleApplyData = RequireSuccess(scaleApply, "TransformObjects-UniformScale");
            Require(scaleApplyData.UpdatedObjectCount == 1, "UniformScale should update one object.");
            RequirePointLocation(workingFilePath, pointId, new Point3d(-4d, 12d, 6d), "UniformScale should scale point by 2 about origin.");
            checkpoints.Add("TransformObjects (UniformScale) ok");

            // --- Extended coverage: Arc CenterRadius mode create + cleanup ---
            OperationResponse<GeometryCreationResponse> arcCenterRadiusCreate = createArcsTool.CreateArcs(
                workingFilePath,
                new List<ArcItemRequest>
                {
                    new()
                    {
                        Mode = ArcConstructionMode.CenterRadius,
                        CenterX = 50d,
                        CenterY = 50d,
                        CenterZ = 0d,
                        NormalX = 0d,
                        NormalY = 0d,
                        NormalZ = 1d,
                        Radius = 3d,
                        StartAngleRadians = 0d,
                        EndAngleRadians = Math.PI
                    }
                },
                CreateCommonOptions(layerFullPath, "260420-smoke-arc-cr", "extended-arc-cr"));

            GeometryCreationResponse arcCenterRadiusData = RequireSuccess(arcCenterRadiusCreate, "CreateArcs-CenterRadius");
            Guid arcCenterRadiusId = RequireSingleCreatedObject(arcCenterRadiusData, GeometryPrimitiveKind.Arc);
            RequireArcExists(workingFilePath, arcCenterRadiusId, "CreateArcs CenterRadius should produce a valid arc.");
            checkpoints.Add($"CreateArcs (CenterRadius) ok: {arcCenterRadiusId}");

            OperationResponse<GeometryModificationResponse> arcCleanup = deleteObjectsTool.DeleteObjects(
                workingFilePath,
                confirmedObjectIds: new List<Guid> { arcCenterRadiusId });
            RequireSuccess(arcCleanup, "DeleteObjects-ArcCleanup");
            Require(!ObjectExists(workingFilePath, arcCenterRadiusId), "Extended CenterRadius arc should be cleaned up.");

            // --- Extended coverage: Surface Plane mode create + cleanup ---
            OperationResponse<GeometryCreationResponse> surfacePlaneCreate = createSurfacesTool.CreateSurfaces(
                workingFilePath,
                new List<SurfaceItemRequest>
                {
                    new()
                    {
                        Mode = SurfaceConstructionMode.Plane,
                        OriginX = 100d,
                        OriginY = 100d,
                        OriginZ = 0d,
                        NormalX = 0d,
                        NormalY = 0d,
                        NormalZ = 1d,
                        ULength = 5d,
                        VLength = 5d
                    }
                },
                CreateCommonOptions(layerFullPath, "260420-smoke-surface-plane", "extended-plane-surface"));

            GeometryCreationResponse surfacePlaneData = RequireSuccess(surfacePlaneCreate, "CreateSurfaces-Plane");
            Guid surfacePlaneId = RequireSingleCreatedObject(surfacePlaneData, GeometryPrimitiveKind.Surface);
            checkpoints.Add($"CreateSurfaces (Plane) ok: {surfacePlaneId}");

            OperationResponse<GeometryModificationResponse> surfaceCleanup = deleteObjectsTool.DeleteObjects(
                workingFilePath,
                confirmedObjectIds: new List<Guid> { surfacePlaneId });
            RequireSuccess(surfaceCleanup, "DeleteObjects-SurfaceCleanup");
            Require(!ObjectExists(workingFilePath, surfacePlaneId), "Extended plane surface should be cleaned up.");

            // --- Hard error: ReplaceGeometry cross-primitive (Point ← Surface) ---
            OperationResponse<GeometryModificationResponse> replaceCrossPrimitive = replaceGeometryTool.ReplaceGeometry(
                workingFilePath,
                new List<GeometryReplacementEntryRequest>
                {
                    new()
                    {
                        ObjectId = pointId,
                        Geometry = new GeometryCreationSpec
                        {
                            Primitive = GeometryPrimitiveKind.Surface,
                            SurfaceMode = SurfaceConstructionMode.FourCorners,
                            Corner0X = 0d, Corner0Y = 0d, Corner0Z = 0d,
                            Corner1X = 1d, Corner1Y = 0d, Corner1Z = 0d,
                            Corner2X = 1d, Corner2Y = 1d, Corner2Z = 0d,
                            Corner3X = 0d, Corner3Y = 1d, Corner3Z = 0d
                        }
                    }
                });
            RequireFailure(replaceCrossPrimitive, "ReplaceGeometry cross-primitive (Point←Surface) should be blocked.");
            RequirePointLocation(workingFilePath, pointId, new Point3d(-4d, 12d, 6d), "Cross-primitive replace attempt should not mutate the point.");
            checkpoints.Add("ReplaceGeometry cross-primitive rejected");

            // --- Hard error: EditControlPoints on LineCurve (not a NurbsCurve) ---
            OperationResponse<GeometryModificationResponse> editLineCurve = editControlPointsTool.EditControlPoints(
                workingFilePath,
                new List<ControlPointEditEntryRequest>
                {
                    new()
                    {
                        ObjectId = lineId,
                        TargetMode = ControlPointTargetMode.CurveIndex,
                        PointIndex = 0,
                        X = 0d,
                        Y = 0d,
                        Z = 0d
                    }
                });
            RequireFailure(editLineCurve, "EditControlPoints on LineCurve should be blocked (not a NurbsCurve).");
            checkpoints.Add("EditControlPoints on LineCurve rejected");

            // --- Hard error: Control point index out of bounds ---
            OperationResponse<GeometryModificationResponse> editOutOfBounds = editControlPointsTool.EditControlPoints(
                workingFilePath,
                new List<ControlPointEditEntryRequest>
                {
                    new()
                    {
                        ObjectId = surfaceId,
                        TargetMode = ControlPointTargetMode.SurfaceUV,
                        UIndex = 999,
                        VIndex = 0,
                        X = 0d,
                        Y = 0d,
                        Z = 0d
                    }
                });
            RequireFailure(editOutOfBounds, "EditControlPoints with out-of-bounds UIndex should be blocked.");
            checkpoints.Add("EditControlPoints out-of-bounds rejected");

            // --- Hard error: Invalid LayerFullPath ---
            OperationResponse<GeometryCreationResponse> invalidLayer = createPointsTool.CreatePoints(
                workingFilePath,
                new List<PointItemRequest> { new() { X = 0d, Y = 0d, Z = 0d } },
                new GeometryCreationCommonOptions { LayerFullPath = "NoSuch::NoLayer" });
            RequireFailure(invalidLayer, "CreatePoints with missing LayerFullPath should be blocked.");
            checkpoints.Add("CreatePoints missing layer rejected");

            // --- Hard error: NaN coordinate ---
            OperationResponse<GeometryCreationResponse> nanCoord = createPointsTool.CreatePoints(
                workingFilePath,
                new List<PointItemRequest> { new() { X = double.NaN, Y = 0d, Z = 0d } },
                CreateCommonOptions(layerFullPath, "260420-smoke-nan", "extended-nan"));
            RequireFailure(nanCoord, "CreatePoints with NaN coordinate should be blocked.");
            checkpoints.Add("CreatePoints NaN rejected");

            // --- Hard error: Zero-length line ---
            OperationResponse<GeometryCreationResponse> zeroLine = createLinesTool.CreateLines(
                workingFilePath,
                new List<LineItemRequest>
                {
                    new()
                    {
                        StartX = 0d, StartY = 0d, StartZ = 0d,
                        EndX = 0d, EndY = 0d, EndZ = 0d
                    }
                },
                CreateCommonOptions(layerFullPath, "260420-smoke-zero-line", "extended-zero-line"));
            RequireFailure(zeroLine, "CreateLines with zero-length line should be blocked.");
            checkpoints.Add("CreateLines zero-length rejected");

            // --- Hard error: UniformScale with zero scale factor ---
            var zeroScaleSpec = new GeometryTransformSpec
            {
                Kind = GeometryTransformKind.UniformScale,
                CenterX = 0d,
                CenterY = 0d,
                CenterZ = 0d,
                ScaleFactor = 0d
            };
            OperationResponse<GeometryModificationResponse> zeroScaleApply = transformObjectsTool.TransformObjects(
                workingFilePath,
                zeroScaleSpec,
                confirmedObjectIds: new List<Guid> { pointId });
            RequireFailure(zeroScaleApply, "TransformObjects with zero UniformScale factor should be blocked.");
            checkpoints.Add("TransformObjects zero-scale rejected");

            // --- Hard error: Unresolvable ConfirmedObjectIds ---
            Guid unknownId = Guid.NewGuid();
            OperationResponse<GeometryModificationResponse> unresolvable = transformObjectsTool.TransformObjects(
                workingFilePath,
                rotateSpec,
                confirmedObjectIds: new List<Guid> { unknownId });
            RequireFailure(unresolvable, "TransformObjects with unresolvable ObjectId should be blocked.");
            checkpoints.Add("TransformObjects unresolvable ObjectId rejected");

            Console.WriteLine("Geometry smoke test completed successfully.");
            Console.WriteLine($"Source: {sourceFilePath}");
            Console.WriteLine($"Working copy: {workingFilePath}");
            Console.WriteLine($"Layer: {layerFullPath}");
            Console.WriteLine($"Initial objects: {initialObjectCount}");
            Console.WriteLine($"Final objects: {GetObjectCount(workingFilePath)}");

            foreach (string checkpoint in checkpoints)
            {
                Console.WriteLine($"- {checkpoint}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Geometry smoke test failed: {ex}");
            System.Environment.ExitCode = 1;
        }

        return true;
    }

    private static GeometryCreationCommonOptions CreateCommonOptions(string layerFullPath, string name, string role)
    {
        return new GeometryCreationCommonOptions
        {
            LayerFullPath = layerFullPath,
            Name = name,
            UserText = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["geometry_smoke_batch"] = "260420",
                ["geometry_smoke_role"] = role
            }
        };
    }

    private static T RequireSuccess<T>(OperationResponse<T> response, string operationName)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{operationName} failed: {response.Message}");
        }

        return response.Data;
    }

    private static void RequireFailure<T>(OperationResponse<T> response, string message)
    {
        if (response.Success)
        {
            throw new InvalidOperationException($"{message} Expected failure but got success. Response message: {response.Message}");
        }
    }

    private static Guid RequireSingleCreatedObject(GeometryCreationResponse response, GeometryPrimitiveKind expectedPrimitive)
    {
        Require(response.CreatedCount == 1, $"Expected exactly one created object, actual count: {response.CreatedCount}");
        GeometryCreatedObjectResponse createdObject = response.CreatedObjects.Single();
        Require(
            createdObject.Primitive == expectedPrimitive,
            $"Expected created primitive {expectedPrimitive}, actual primitive: {createdObject.Primitive}");
        Require(createdObject.ObjectId != Guid.Empty, "Created object should have a non-empty ObjectId.");
        return createdObject.ObjectId;
    }

    private static void RequirePointLocation(string filePath, Guid objectId, Point3d expected, string message)
    {
        using var model = ReadModel(filePath);
        File3dmObject obj = FindObject(model, objectId);
        if (obj.Geometry is not Point point)
        {
            throw new InvalidOperationException($"Expected object {objectId} to be a Point.");
        }

        Require(point.Location.EpsilonEquals(expected, 1e-6), $"{message} Expected {expected}, actual {point.Location}.");
    }

    private static void RequireLineGeometry(string filePath, Guid objectId, Point3d expectedStart, Point3d expectedEnd, string message)
    {
        using var model = ReadModel(filePath);
        File3dmObject obj = FindObject(model, objectId);
        if (obj.Geometry is not LineCurve lineCurve)
        {
            throw new InvalidOperationException($"Expected object {objectId} to be a LineCurve.");
        }

        Require(
            lineCurve.Line.From.EpsilonEquals(expectedStart, 1e-6) && lineCurve.Line.To.EpsilonEquals(expectedEnd, 1e-6),
            $"{message} Expected line {expectedStart} -> {expectedEnd}, actual {lineCurve.Line.From} -> {lineCurve.Line.To}.");
    }

    private static void RequireArcExists(string filePath, Guid objectId, string message)
    {
        using var model = ReadModel(filePath);
        File3dmObject obj = FindObject(model, objectId);
        if (obj.Geometry is not ArcCurve arcCurve)
        {
            throw new InvalidOperationException($"Expected object {objectId} to be an ArcCurve.");
        }

        Require(arcCurve.Arc.IsValid, message);
    }

    private static void RequireSurfaceControlPoint(string filePath, Guid objectId, int uIndex, int vIndex, Point3d expected, string message)
    {
        using var model = ReadModel(filePath);
        File3dmObject obj = FindObject(model, objectId);
        if (obj.Geometry is not NurbsSurface surface)
        {
            throw new InvalidOperationException($"Expected object {objectId} to be a NurbsSurface.");
        }

        ControlPoint point = surface.Points.GetControlPoint(uIndex, vIndex);
        Require(point.Location.EpsilonEquals(expected, 1e-6), $"{message} Expected {expected}, actual {point.Location}.");
    }

    private static void RequireUserText(string filePath, Guid objectId, string key, string expectedValue, string message)
    {
        using var model = ReadModel(filePath);
        File3dmObject obj = FindObject(model, objectId);
        string actualValue = obj.Attributes.GetUserString(key) ?? string.Empty;
        Require(string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase), $"{message} Expected [{key}]={expectedValue}, actual value: {actualValue}.");
    }

    private static bool ObjectExists(string filePath, Guid objectId)
    {
        using var model = ReadModel(filePath);
        return model.Objects.Any(obj => obj.Attributes.ObjectId == objectId);
    }

    private static int GetObjectCount(string filePath)
    {
        using var model = ReadModel(filePath);
        return model.Objects.Count;
    }

    private static string GetFirstActiveLayerFullPath(string filePath)
    {
        using var model = ReadModel(filePath);
        Layer? layer = model.AllLayers.FirstOrDefault(candidate => !candidate.IsDeleted);
        if (layer is null)
        {
            throw new InvalidOperationException("The smoke test source file does not contain an active layer.");
        }

        return layer.FullPath;
    }

    private static File3dm ReadModel(string filePath)
    {
        File3dm? model = File3dm.Read(filePath);
        if (model is null)
        {
            throw new InvalidOperationException($"Failed to read Rhino model: {filePath}");
        }

        return model;
    }

    private static File3dmObject FindObject(File3dm model, Guid objectId)
    {
        foreach (File3dmObject obj in model.Objects)
        {
            if (obj.Attributes.ObjectId == objectId)
            {
                return obj;
            }
        }

        throw new InvalidOperationException($"Expected object was not found: {objectId}");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
