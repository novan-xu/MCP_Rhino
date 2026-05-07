extern alias rhinocommon;

using System.Drawing;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using MCP_Rhino.Server.Tools.Geometry.Rebuild;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using Interval = rhinocommon::Rhino.Geometry.Interval;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using ObjectColorSource = rhinocommon::Rhino.DocObjects.ObjectColorSource;
using Plane = rhinocommon::Rhino.Geometry.Plane;
using PlaneSurface = rhinocommon::Rhino.Geometry.PlaneSurface;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using Polyline = rhinocommon::Rhino.Geometry.Polyline;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;
using Vector3d = rhinocommon::Rhino.Geometry.Vector3d;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    partial void RegisterSurfacePointOrderRebuildHandlers()
    {
        _extensionHandlers["surface-point-order-rebuild-smoke-test"] = HandleSurfacePointOrderRebuildSmokeTest;
    }

    private bool HandleSurfacePointOrderRebuildSmokeTest(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: dotnet run --project src/MCP_Rhino.Server -- surface-point-order-rebuild-smoke-test <3dm-file-path>");
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
                RunCliFallbackSurfacePointOrderRebuildSmoke(filePath);
            }
            else
            {
                RunLiveSurfacePointOrderRebuildSmoke(filePath);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Surface point order rebuild smoke test failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunCliFallbackSurfacePointOrderRebuildSmoke(string filePath)
    {
        var checkpoints = new List<string>();
        Guid objectId = Guid.NewGuid();
        var inspectTool = new InspectSurfaceRebuildDescriptorTool(_surfaceRebuildOrchestrator);
        var previewTool = new PreviewRedefineSurfacePointOrderTool(_surfaceRebuildOrchestrator);
        var applyTool = new ApplyRedefineSurfacePointOrderTool(_surfaceRebuildOrchestrator);

        RequireSurfacePointOrderRebuildFailureWithMessage(
            inspectTool.InspectSurfaceRebuildDescriptor(filePath, new[] { objectId }),
            "LIVE_RHINO_REQUIRED",
            "InspectSurfaceRebuildDescriptor should require live Rhino.");
        checkpoints.Add("InspectSurfaceRebuildDescriptor rejected in CLI fallback");

        RequireSurfacePointOrderRebuildFailureWithMessage(
            previewTool.PreviewRedefineSurfacePointOrder(filePath, new[] { objectId }, new SurfaceRebuildSpec()),
            "LIVE_RHINO_REQUIRED",
            "PreviewRedefineSurfacePointOrder should require live Rhino.");
        checkpoints.Add("PreviewRedefineSurfacePointOrder rejected in CLI fallback");

        RequireSurfacePointOrderRebuildFailureWithMessage(
            applyTool.ApplyRedefineSurfacePointOrder(filePath, new[] { objectId }, new SurfaceRebuildSpec()),
            "LIVE_RHINO_REQUIRED",
            "ApplyRedefineSurfacePointOrder should require live Rhino.");
        checkpoints.Add("ApplyRedefineSurfacePointOrder rejected in CLI fallback");

        Console.WriteLine("Surface point order rebuild smoke test completed successfully (CLI fallback mode).");
        Console.WriteLine($"Source: {filePath}");
        foreach (string checkpoint in checkpoints)
        {
            Console.WriteLine($"- {checkpoint}");
        }
    }

    private void RunLiveSurfacePointOrderRebuildSmoke(string filePath)
    {
        var checkpoints = new List<string>();
        var inspectTool = new InspectSurfaceRebuildDescriptorTool(_surfaceRebuildOrchestrator);
        var previewTool = new PreviewRedefineSurfacePointOrderTool(_surfaceRebuildOrchestrator);
        var applyTool = new ApplyRedefineSurfacePointOrderTool(_surfaceRebuildOrchestrator);

        SurfacePointOrderRebuildSnapshot before = CaptureSurfacePointOrderRebuildSnapshot(filePath);
        IReadOnlyList<Guid> objectIds = CreateSurfacePointOrderRebuildSmokeObjects(filePath);
        Guid quadId = objectIds[0];
        Guid polygonId = objectIds[1];

        try
        {
            SurfaceRebuildDescriptorResponse inspect = RequireSurfacePointOrderRebuildSuccess(
                inspectTool.InspectSurfaceRebuildDescriptor(filePath, objectIds),
                "InspectSurfaceRebuildDescriptor");
            RequireSurfacePointOrderRebuild(inspect.Descriptors.Count == 2, "Inspect should return two descriptors.");
            RequireSurfacePointOrderRebuild(inspect.Descriptors[0].SuggestedReferenceCurve is not null, "Quad should have an automatic reference edge.");
            RequireSurfacePointOrderRebuild(inspect.Descriptors[0].SuggestedRoute == SurfaceRebuildRouteKind.FourPoint, "Quad should route to FourPoint.");
            RequireSurfacePointOrderRebuild(inspect.Descriptors[1].SuggestedRoute == SurfaceRebuildRouteKind.BoundarySurface, "Polygon should route to BoundarySurface.");
            checkpoints.Add("Inspect quad + polygon descriptors ok");

            SurfacePointOrderPreviewResponse preview = RequireSurfacePointOrderRebuildSuccess(
                previewTool.PreviewRedefineSurfacePointOrder(filePath, objectIds, new SurfaceRebuildSpec()),
                "PreviewRedefineSurfacePointOrder");
            RequireSurfacePointOrderRebuild(preview.Results.Count == 2, "Preview should return two results.");
            RequireSurfacePointOrderRebuild(preview.Results.Any(result => result.ObjectId == quadId && !result.Skipped && result.Plan?.Route == SurfaceRebuildRouteKind.FourPoint), "Preview should include an executable quad FourPoint route.");
            RequireSurfacePointOrderRebuild(preview.Results.Any(result => result.ObjectId == polygonId && result.Skipped && result.SkipReason.Contains("SURFACE_POINT_ORDER_REBUILD_REQUIRES_QUAD_SURFACE", StringComparison.Ordinal)), "Preview should skip non-quad targets.");
            checkpoints.Add("Preview quad-only guard ok");

            SurfacePointOrderApplyResponse apply = RequireSurfacePointOrderRebuildSuccess(
                applyTool.ApplyRedefineSurfacePointOrder(filePath, objectIds, new SurfaceRebuildSpec()),
                "ApplyRedefineSurfacePointOrder");
            RequireSurfacePointOrderRebuild(string.Equals(apply.UndoRecordName, "MCP:RedefineSurfacePointOrder", StringComparison.Ordinal), "Apply should report point-order rebuild undo name.");
            RequireSurfacePointOrderRebuild(apply.Results.Count == 2, "Apply should return two results.");
            RequireSurfacePointOrderRebuild(apply.Results.Any(result => result.ObjectId == quadId && !result.Skipped), "Apply should execute the quad target.");
            RequireSurfacePointOrderRebuild(apply.Results.Any(result => result.ObjectId == polygonId && result.Skipped && result.SkipReason.Contains("SURFACE_POINT_ORDER_REBUILD_REQUIRES_QUAD_SURFACE", StringComparison.Ordinal)), "Apply should skip non-quad targets.");
            checkpoints.Add("Batch Apply quad-only guard ok");

            RequireSurfacePointOrderRebuild(IsSurfacePointOrderRebuildBrep(filePath, polygonId), "Skipped polygon target should remain a Brep.");
            checkpoints.Add("Skipped polygon remains unchanged ok");

            SurfaceRebuildDescriptorResponse explicitEdgeInspect = RequireSurfacePointOrderRebuildSuccess(
                inspectTool.InspectSurfaceRebuildDescriptor(filePath, new[] { quadId }, referenceEdgeIndex: 1),
                "Explicit edge inspect");
            RequireSurfacePointOrderRebuild(explicitEdgeInspect.Descriptors[0].SuggestedReferenceCurve?.EdgeIndex == 1, "Explicit ReferenceEdgeIndex should be used.");
            checkpoints.Add("Explicit ReferenceEdgeIndex override ok");

            RequireSurfacePointOrderRebuildFailure(
                previewTool.PreviewRedefineSurfacePointOrder(
                    filePath,
                    new[] { Guid.NewGuid() },
                    new SurfaceRebuildSpec()),
                "OBJECT_NOT_FOUND",
                "Missing ObjectId should fail.");
            checkpoints.Add("OBJECT_NOT_FOUND guard ok");
        }
        finally
        {
            DeleteSurfacePointOrderRebuildSmokeObjects(filePath, objectIds);
        }

        SurfacePointOrderRebuildSnapshot after = CaptureSurfacePointOrderRebuildSnapshot(filePath);
        RequireSurfacePointOrderRebuild(after.ObjectCount == before.ObjectCount, "Smoke should not leave temporary objects.");
        RequireSurfacePointOrderRebuild(after.DocumentStringCount == before.DocumentStringCount, "Smoke should not change document strings.");
        checkpoints.Add("Temporary objects cleaned up");

        Console.WriteLine("Surface point order rebuild smoke test completed successfully (live Rhino mode).");
        Console.WriteLine($"Active file: {filePath}");
        foreach (string checkpoint in checkpoints)
        {
            Console.WriteLine($"- {checkpoint}");
        }
    }

    private SurfacePointOrderRebuildSnapshot CaptureSurfacePointOrderRebuildSnapshot(string filePath)
    {
        return RequireSurfacePointOrderRebuildSuccess(
            _liveRhinoDocumentAccessor.Execute(filePath, document =>
            {
                return OperationResponse<SurfacePointOrderRebuildSnapshot>.Ok(new SurfacePointOrderRebuildSnapshot
                {
                    ObjectCount = document.Objects.Count,
                    DocumentStringCount = document.Strings.Count
                });
            }),
            "CaptureSurfacePointOrderRebuildSnapshot");
    }

    private IReadOnlyList<Guid> CreateSurfacePointOrderRebuildSmokeObjects(string filePath)
    {
        return RequireSurfacePointOrderRebuildSuccess(
            _liveRhinoDocumentAccessor.ExecuteWithUndo(filePath, "MCP:SurfacePointOrderRebuildSmokeSetup", document =>
            {
                var ids = new List<Guid>();
                var verticalPlane = new Plane(Point3d.Origin, Vector3d.YAxis, Vector3d.ZAxis);
                var quad = new PlaneSurface(verticalPlane, new Interval(0d, 2d), new Interval(0d, 2d));
                ids.Add(document.Objects.AddSurface(quad, CreateSurfacePointOrderRebuildAttributes("quad")));

                var polygon = new Polyline(new[]
                {
                    new Point3d(4d, 0d, 0d),
                    new Point3d(4d, 2d, 0d),
                    new Point3d(4d, 3d, 1d),
                    new Point3d(4d, 2d, 2d),
                    new Point3d(4d, 0d, 2d),
                    new Point3d(4d, -1d, 1d),
                    new Point3d(4d, 0d, 0d)
                });
                Brep[]? polygonBreps = Brep.CreatePlanarBreps(polygon.ToNurbsCurve(), document.ModelAbsoluteTolerance);
                if (polygonBreps is null || polygonBreps.Length == 0)
                {
                    return OperationResponse<(bool Mutated, IReadOnlyList<Guid> Result)>.Fail("Failed to create polygon smoke Brep.");
                }

                ids.Add(document.Objects.AddBrep(polygonBreps[0], CreateSurfacePointOrderRebuildAttributes("polygon")));

                if (ids.Any(id => id == Guid.Empty))
                {
                    return OperationResponse<(bool Mutated, IReadOnlyList<Guid> Result)>.Fail("Failed to add one or more point-order smoke objects.");
                }

                document.Views.Redraw();
                return OperationResponse<(bool Mutated, IReadOnlyList<Guid> Result)>.Ok((true, ids));
            }),
            "CreateSurfacePointOrderRebuildSmokeObjects");
    }

    private void DeleteSurfacePointOrderRebuildSmokeObjects(string filePath, IReadOnlyList<Guid> objectIds)
    {
        OperationResponse<bool> delete = _liveRhinoDocumentAccessor.ExecuteWithUndo(
            filePath,
            "MCP:SurfacePointOrderRebuildSmokeCleanup",
            document =>
            {
                bool mutated = false;
                foreach (Guid objectId in objectIds)
                {
                    RhinoObject? rhinoObject = document.Objects.FindId(objectId);
                    if (rhinoObject is not null && !rhinoObject.IsDeleted)
                    {
                        mutated |= document.Objects.Delete(objectId, true);
                    }
                }

                document.Views.Redraw();
                return OperationResponse<(bool Mutated, bool Result)>.Ok((mutated, true));
            });

        RequireSurfacePointOrderRebuildSuccess(delete, "DeleteSurfacePointOrderRebuildSmokeObjects");
    }

    private bool IsSurfacePointOrderRebuildBrep(string filePath, Guid objectId)
    {
        return RequireSurfacePointOrderRebuildSuccess(
            _liveRhinoDocumentAccessor.Execute(filePath, document =>
            {
                return OperationResponse<bool>.Ok(document.Objects.FindId(objectId)?.Geometry is Brep);
            }),
            "IsSurfacePointOrderRebuildBrep");
    }

    private static ObjectAttributes CreateSurfacePointOrderRebuildAttributes(string suffix)
    {
        var attributes = new ObjectAttributes
        {
            Name = $"mcp-point-order-{suffix}",
            ObjectColor = Color.FromArgb(255, 92, 116, 180),
            ColorSource = ObjectColorSource.ColorFromObject
        };
        attributes.SetUserString("mcp-smoke", "surface-point-order-rebuild");
        return attributes;
    }

    private static T RequireSurfacePointOrderRebuildSuccess<T>(OperationResponse<T> response, string operationName)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{operationName} failed: {response.Message}");
        }

        return response.Data;
    }

    private static void RequireSurfacePointOrderRebuildFailureWithMessage<T>(
        OperationResponse<T> response,
        string expectedMessage,
        string message)
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

    private static void RequireSurfacePointOrderRebuildFailure<T>(
        OperationResponse<T> response,
        string expectedMessageFragment,
        string message)
    {
        if (response.Success)
        {
            throw new InvalidOperationException($"{message} Expected failure but got success.");
        }

        if (!response.Message.Contains(expectedMessageFragment, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{message} Expected message containing [{expectedMessageFragment}], actual [{response.Message}].");
        }
    }

    private static void RequireSurfacePointOrderRebuild(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class SurfacePointOrderRebuildSnapshot
    {
        public int ObjectCount { get; set; }
        public int DocumentStringCount { get; set; }
    }
}
