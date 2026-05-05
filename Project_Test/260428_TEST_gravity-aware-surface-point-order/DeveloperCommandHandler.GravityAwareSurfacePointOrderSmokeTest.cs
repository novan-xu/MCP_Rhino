extern alias rhinocommon;

using System.Drawing;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using MCP_Rhino.Server.Tools.Geometry.Rebuild;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using NurbsSurface = rhinocommon::Rhino.Geometry.NurbsSurface;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using ObjectColorSource = rhinocommon::Rhino.DocObjects.ObjectColorSource;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    partial void RegisterGravityAwareSurfacePointOrderHandlers()
    {
        _extensionHandlers["gravity-aware-surface-point-order-smoke-test"] = HandleGravityAwareSurfacePointOrderSmokeTest;
    }

    private bool HandleGravityAwareSurfacePointOrderSmokeTest(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: dotnet run --project src/MCP_Rhino.Server -- gravity-aware-surface-point-order-smoke-test <3dm-file-path>");
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
                RunCliFallbackGravityAwareSurfacePointOrderSmoke(filePath);
            }
            else
            {
                RunLiveGravityAwareSurfacePointOrderSmoke(filePath);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Gravity-aware surface point-order smoke test failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunCliFallbackGravityAwareSurfacePointOrderSmoke(string filePath)
    {
        var previewTool = new PreviewRedefineSurfacePointOrderTool(_surfaceRebuildOrchestrator);
        RequireGravityAwareFailure(
            previewTool.PreviewRedefineSurfacePointOrder(filePath, new[] { Guid.NewGuid() }, new SurfaceRebuildSpec()),
            "LIVE_RHINO_REQUIRED",
            "PreviewRedefineSurfacePointOrder should require live Rhino.");

        Console.WriteLine("Gravity-aware surface point-order smoke test completed successfully (CLI fallback mode).");
        Console.WriteLine($"Source: {filePath}");
        Console.WriteLine("- PreviewRedefineSurfacePointOrder rejected in CLI fallback");
    }

    private void RunLiveGravityAwareSurfacePointOrderSmoke(string filePath)
    {
        var checkpoints = new List<string>();
        var previewTool = new PreviewRedefineSurfacePointOrderTool(_surfaceRebuildOrchestrator);
        var applyTool = new ApplyRedefineSurfacePointOrderTool(_surfaceRebuildOrchestrator);

        GravityAwareSurfacePointOrderSnapshot before = CaptureGravityAwareSurfacePointOrderSnapshot(filePath);
        IReadOnlyList<GravityAwareSmokeTarget> targets = CreateGravityAwareSurfacePointOrderSmokeObjects(filePath);
        IReadOnlyList<Guid> objectIds = targets.Select(target => target.ObjectId).ToList();

        try
        {
            var spec = new SurfaceRebuildSpec
            {
                Direction = SurfacePointOrderDirection.Clockwise,
                StartAnchorMode = SurfacePointOrderStartAnchorMode.ReferenceStart
            };

            SurfacePointOrderPreviewResponse preview = RequireGravityAwareSuccess(
                previewTool.PreviewRedefineSurfacePointOrder(filePath, objectIds, spec),
                "PreviewRedefineSurfacePointOrder");

            foreach (GravityAwareSmokeTarget target in targets.Where(target => !target.ExpectDegenerate))
            {
                SurfacePointOrderPreviewItem result = RequireGravityAwarePreviewResult(preview, target.ObjectId);
                RequireGravityAware(!result.Skipped, $"{target.Name} should be previewable.");
                RequireGravityAware(result.Plan is not null, $"{target.Name} should return a point-order plan.");
                AssertGravityAwarePointClose(result.Plan!.Points[0].Position3d, target.ExpectedStartPoint, $"{target.Name} lower-left start point");
                RequireGravityAware(IsClockwiseInLocalFrame(result.Plan), $"{target.Name} should be clockwise in its surface-local frame.");
            }
            checkpoints.Add("Preview selected expected lower-left starts on opposite and sloped front faces");

            GravityAwareSmokeTarget degenerateTarget = targets.Single(target => target.ExpectDegenerate);
            SurfacePointOrderPreviewItem degenerateResult = RequireGravityAwarePreviewResult(preview, degenerateTarget.ObjectId);
            RequireGravityAware(degenerateResult.Skipped, "Horizontal target should be skipped.");
            RequireGravityAware(
                degenerateResult.SkipReason.Contains("SURFACE_GRAVITY_PROJECTION_DEGENERATE", StringComparison.Ordinal),
                $"Horizontal target should report gravity projection degeneracy, actual: {degenerateResult.SkipReason}");
            checkpoints.Add("Horizontal gravity-parallel surface reports degeneracy");

            SurfacePointOrderApplyResponse apply = RequireGravityAwareSuccess(
                applyTool.ApplyRedefineSurfacePointOrder(filePath, objectIds, spec),
                "ApplyRedefineSurfacePointOrder");
            RequireGravityAware(string.Equals(apply.UndoRecordName, "MCP:RedefineSurfacePointOrder", StringComparison.Ordinal), "Apply should use the point-order undo name.");
            RequireGravityAware(apply.Results.Count == targets.Count, "Apply should return one result per target.");
            RequireGravityAware(apply.Results.Count(result => !result.Skipped) == targets.Count(target => !target.ExpectDegenerate), "Apply should execute only non-degenerate targets.");
            RequireGravityAware(apply.Results.Any(result => result.ObjectId == degenerateTarget.ObjectId && result.Skipped), "Apply should skip the degenerate horizontal target.");
            checkpoints.Add("Apply executes non-degenerate targets and skips degenerate target");
        }
        finally
        {
            DeleteGravityAwareSurfacePointOrderSmokeObjects(filePath, objectIds);
        }

        GravityAwareSurfacePointOrderSnapshot after = CaptureGravityAwareSurfacePointOrderSnapshot(filePath);
        RequireGravityAware(after.ObjectCount == before.ObjectCount, "Smoke should not leave temporary objects.");
        RequireGravityAware(after.DocumentStringCount == before.DocumentStringCount, "Smoke should not change document strings.");
        checkpoints.Add("Temporary objects cleaned up");

        Console.WriteLine("Gravity-aware surface point-order smoke test completed successfully (live Rhino mode).");
        Console.WriteLine($"Active file: {filePath}");
        foreach (string checkpoint in checkpoints)
        {
            Console.WriteLine($"- {checkpoint}");
        }
    }

    private GravityAwareSurfacePointOrderSnapshot CaptureGravityAwareSurfacePointOrderSnapshot(string filePath)
    {
        return RequireGravityAwareSuccess(
            _liveRhinoDocumentAccessor.Execute(filePath, document =>
            {
                return OperationResponse<GravityAwareSurfacePointOrderSnapshot>.Ok(new GravityAwareSurfacePointOrderSnapshot
                {
                    ObjectCount = document.Objects.Count,
                    DocumentStringCount = document.Strings.Count
                });
            }),
            "CaptureGravityAwareSurfacePointOrderSnapshot");
    }

    private IReadOnlyList<GravityAwareSmokeTarget> CreateGravityAwareSurfacePointOrderSmokeObjects(string filePath)
    {
        return RequireGravityAwareSuccess(
            _liveRhinoDocumentAccessor.ExecuteWithUndo(filePath, "MCP:GravityAwareSurfacePointOrderSmokeSetup", document =>
            {
                var targets = new List<GravityAwareSmokeTarget>
                {
                    CreateGravityAwareSmokeSurface(document, "front-pos-x", new[]
                    {
                        new Point3d(0d, 0d, 0d),
                        new Point3d(0d, 2d, 0d),
                        new Point3d(0d, 2d, 3d),
                        new Point3d(0d, 0d, 3d)
                    }, new Point3d(0d, 0d, 0d), expectDegenerate: false),

                    CreateGravityAwareSmokeSurface(document, "front-neg-x", new[]
                    {
                        new Point3d(10d, 0d, 0d),
                        new Point3d(10d, -2d, 0d),
                        new Point3d(10d, -2d, 3d),
                        new Point3d(10d, 0d, 3d)
                    }, new Point3d(10d, 0d, 0d), expectDegenerate: false),

                    CreateGravityAwareSmokeSurface(document, "front-pos-y", new[]
                    {
                        new Point3d(0d, 10d, 0d),
                        new Point3d(-2d, 10d, 0d),
                        new Point3d(-2d, 10d, 3d),
                        new Point3d(0d, 10d, 3d)
                    }, new Point3d(0d, 10d, 0d), expectDegenerate: false),

                    CreateGravityAwareSmokeSurface(document, "front-sloped", new[]
                    {
                        new Point3d(20d, 0d, 0d),
                        new Point3d(20d, 2d, 0d),
                        new Point3d(21d, 2d, 3d),
                        new Point3d(21d, 0d, 3d)
                    }, new Point3d(20d, 0d, 0d), expectDegenerate: false),

                    CreateGravityAwareSmokeSurface(document, "horizontal-degenerate", new[]
                    {
                        new Point3d(30d, 0d, 0d),
                        new Point3d(32d, 0d, 0d),
                        new Point3d(32d, 2d, 0d),
                        new Point3d(30d, 2d, 0d)
                    }, new Point3d(30d, 0d, 0d), expectDegenerate: true)
                };

                if (targets.Any(target => target.ObjectId == Guid.Empty))
                {
                    return OperationResponse<(bool Mutated, IReadOnlyList<GravityAwareSmokeTarget> Result)>.Fail("Failed to add one or more gravity-aware smoke objects.");
                }

                document.Views.Redraw();
                return OperationResponse<(bool Mutated, IReadOnlyList<GravityAwareSmokeTarget> Result)>.Ok((true, targets));
            }),
            "CreateGravityAwareSurfacePointOrderSmokeObjects");
    }

    private static GravityAwareSmokeTarget CreateGravityAwareSmokeSurface(
        rhinocommon::Rhino.RhinoDoc document,
        string name,
        IReadOnlyList<Point3d> corners,
        Point3d expectedStartPoint,
        bool expectDegenerate)
    {
        NurbsSurface? surface = NurbsSurface.CreateFromCorners(corners[0], corners[1], corners[2], corners[3]);
        if (surface is null)
        {
            return new GravityAwareSmokeTarget(name, Guid.Empty, expectedStartPoint, expectDegenerate);
        }

        Guid objectId = document.Objects.AddSurface(surface, CreateGravityAwareSurfacePointOrderAttributes(name));
        return new GravityAwareSmokeTarget(name, objectId, expectedStartPoint, expectDegenerate);
    }

    private void DeleteGravityAwareSurfacePointOrderSmokeObjects(string filePath, IReadOnlyList<Guid> objectIds)
    {
        OperationResponse<bool> delete = _liveRhinoDocumentAccessor.ExecuteWithUndo(
            filePath,
            "MCP:GravityAwareSurfacePointOrderSmokeCleanup",
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

        RequireGravityAwareSuccess(delete, "DeleteGravityAwareSurfacePointOrderSmokeObjects");
    }

    private static bool IsClockwiseInLocalFrame(SurfacePointOrderPlan plan)
    {
        double twiceArea = 0d;
        for (int i = 0; i < plan.Points.Count; i++)
        {
            GeometryPoint2dData current = plan.Points[i].Position2dInLcs;
            GeometryPoint2dData next = plan.Points[(i + 1) % plan.Points.Count].Position2dInLcs;
            twiceArea += (current.X * next.Y) - (next.X * current.Y);
        }

        return twiceArea < -1e-9;
    }

    private static SurfacePointOrderPreviewItem RequireGravityAwarePreviewResult(
        SurfacePointOrderPreviewResponse response,
        Guid objectId)
    {
        SurfacePointOrderPreviewItem? result = response.Results.FirstOrDefault(item => item.ObjectId == objectId);
        if (result is null)
        {
            throw new InvalidOperationException($"Preview result not found for object {objectId}.");
        }

        return result;
    }

    private static void AssertGravityAwarePointClose(GeometryPointData actual, Point3d expected, string label)
    {
        double dx = actual.X - expected.X;
        double dy = actual.Y - expected.Y;
        double dz = actual.Z - expected.Z;
        double distance = Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
        if (distance > 1e-6)
        {
            throw new InvalidOperationException($"{label} expected ({expected.X}, {expected.Y}, {expected.Z}), actual ({actual.X}, {actual.Y}, {actual.Z}).");
        }
    }

    private static ObjectAttributes CreateGravityAwareSurfacePointOrderAttributes(string suffix)
    {
        var attributes = new ObjectAttributes
        {
            Name = $"mcp-gravity-point-order-{suffix}",
            ObjectColor = Color.FromArgb(255, 34, 154, 190),
            ColorSource = ObjectColorSource.ColorFromObject
        };
        attributes.SetUserString("mcp-smoke", "gravity-aware-surface-point-order");
        return attributes;
    }

    private static T RequireGravityAwareSuccess<T>(OperationResponse<T> response, string operationName)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{operationName} failed: {response.Message}");
        }

        return response.Data;
    }

    private static void RequireGravityAwareFailure<T>(
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

    private static void RequireGravityAware(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class GravityAwareSurfacePointOrderSnapshot
    {
        public int ObjectCount { get; set; }
        public int DocumentStringCount { get; set; }
    }

    private sealed record GravityAwareSmokeTarget(
        string Name,
        Guid ObjectId,
        Point3d ExpectedStartPoint,
        bool ExpectDegenerate);
}

