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
    partial void RegisterSurfaceDirectionTweakHandlers()
    {
        _extensionHandlers["surface-direction-tweak-smoke-test"] = HandleSurfaceDirectionTweakSmokeTest;
    }

    private bool HandleSurfaceDirectionTweakSmokeTest(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: dotnet run --project src/MCP_Rhino.Server -- surface-direction-tweak-smoke-test <3dm-file-path>");
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
                RunCliFallbackSurfaceDirectionTweakSmoke(filePath);
            }
            else
            {
                RunLiveSurfaceDirectionTweakSmoke(filePath);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Surface direction tweak smoke test failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunCliFallbackSurfaceDirectionTweakSmoke(string filePath)
    {
        var previewTool = new PreviewTweakSurfaceDirectionsTool(_surfaceDirectionTweakOrchestrator);
        var applyTool = new ApplyTweakSurfaceDirectionsTool(_surfaceDirectionTweakOrchestrator);

        RequireSurfaceDirectionTweakFailure(
            previewTool.PreviewTweakSurfaceDirections(
                filePath,
                new[] { Guid.NewGuid() },
                new[] { SurfaceDirectionTweakKind.ReverseU }),
            "LIVE_RHINO_REQUIRED",
            "PreviewTweakSurfaceDirections should require live Rhino.");

        RequireSurfaceDirectionTweakFailure(
            applyTool.ApplyTweakSurfaceDirections(
                filePath,
                new[] { Guid.NewGuid() },
                new[] { SurfaceDirectionTweakKind.ReverseU }),
            "LIVE_RHINO_REQUIRED",
            "ApplyTweakSurfaceDirections should require live Rhino.");

        Console.WriteLine("Surface direction tweak smoke test completed successfully (CLI fallback mode).");
        Console.WriteLine($"Source: {filePath}");
        Console.WriteLine("- PreviewTweakSurfaceDirections rejected in CLI fallback");
        Console.WriteLine("- ApplyTweakSurfaceDirections rejected in CLI fallback");
    }

    private void RunLiveSurfaceDirectionTweakSmoke(string filePath)
    {
        var checkpoints = new List<string>();
        var previewTool = new PreviewTweakSurfaceDirectionsTool(_surfaceDirectionTweakOrchestrator);
        var applyTool = new ApplyTweakSurfaceDirectionsTool(_surfaceDirectionTweakOrchestrator);

        SurfaceDirectionTweakSnapshot before = CaptureSurfaceDirectionTweakSnapshot(filePath);
        IReadOnlyList<Guid> objectIds = CreateSurfaceDirectionTweakSmokeObjects(filePath);

        try
        {
            SurfaceDirectionTweakPreviewResponse preview = RequireSurfaceDirectionTweakSuccess(
                previewTool.PreviewTweakSurfaceDirections(
                    filePath,
                    objectIds,
                    new[] { SurfaceDirectionTweakKind.ReverseU }),
                "PreviewTweakSurfaceDirections ReverseU");
            RequireSurfaceDirectionTweak(preview.Results.Count == objectIds.Count, "Preview should return one result per target.");
            foreach (SurfaceDirectionTweakPreviewItem result in preview.Results)
            {
                RequireSurfaceDirectionTweak(!result.Skipped, $"Preview target {result.ObjectId} should not be skipped.");
                RequireSurfaceDirectionTweak(Dot(result.Before!.UTangent, result.After!.UTangent) < -0.99d, "ReverseU should reverse U tangent.");
            }
            checkpoints.Add("Preview ReverseU reverses U tangent for Surface and single-face Brep");

            Guid brepId = objectIds[1];
            SurfaceDirectionTweakPreviewResponse brepNormalPreview = RequireSurfaceDirectionTweakSuccess(
                previewTool.PreviewTweakSurfaceDirections(
                    filePath,
                    new[] { brepId },
                    new[] { SurfaceDirectionTweakKind.FlipNormal }),
                "PreviewTweakSurfaceDirections FlipNormal");
            SurfaceDirectionTweakPreviewItem brepNormal = brepNormalPreview.Results.Single();
            RequireSurfaceDirectionTweak(!brepNormal.Skipped, "Brep FlipNormal preview should not be skipped.");
            RequireSurfaceDirectionTweak(brepNormal.Before!.FaceOrientationIsReversed != brepNormal.After!.FaceOrientationIsReversed, "FlipNormal should toggle BrepFace orientation flag.");
            checkpoints.Add("Preview FlipNormal toggles single-face Brep orientation flag");

            SurfaceDirectionTweakApplyResponse apply = RequireSurfaceDirectionTweakSuccess(
                applyTool.ApplyTweakSurfaceDirections(
                    filePath,
                    objectIds,
                    new[] { SurfaceDirectionTweakKind.SwapUV }),
                "ApplyTweakSurfaceDirections SwapUV");
            RequireSurfaceDirectionTweak(string.Equals(apply.UndoRecordName, "MCP:TweakSurfaceDirections", StringComparison.Ordinal), "Apply should report the direction tweak undo name.");
            RequireSurfaceDirectionTweak(apply.Results.Count == objectIds.Count, "Apply should return one result per target.");
            foreach (SurfaceDirectionTweakApplyItem result in apply.Results)
            {
                RequireSurfaceDirectionTweak(!result.Skipped, $"Apply target {result.ObjectId} should not be skipped.");
                RequireSurfaceDirectionTweak(result.ObjectId == result.OriginalObjectId, "Apply should preserve ObjectId.");
                RequireSurfaceDirectionTweak(Dot(result.Before!.UTangent, result.After!.VTangent) > 0.99d, "SwapUV should move old U tangent to new V tangent.");
            }
            checkpoints.Add("Apply SwapUV preserves ObjectIds and swaps U/V tangents");
        }
        finally
        {
            DeleteSurfaceDirectionTweakSmokeObjects(filePath, objectIds);
        }

        SurfaceDirectionTweakSnapshot after = CaptureSurfaceDirectionTweakSnapshot(filePath);
        RequireSurfaceDirectionTweak(after.ObjectCount == before.ObjectCount, "Smoke should not leave temporary objects.");
        RequireSurfaceDirectionTweak(after.DocumentStringCount == before.DocumentStringCount, "Smoke should not change document strings.");
        checkpoints.Add("Temporary objects cleaned up");

        Console.WriteLine("Surface direction tweak smoke test completed successfully (live Rhino mode).");
        Console.WriteLine($"Active file: {filePath}");
        foreach (string checkpoint in checkpoints)
        {
            Console.WriteLine($"- {checkpoint}");
        }
    }

    private SurfaceDirectionTweakSnapshot CaptureSurfaceDirectionTweakSnapshot(string filePath)
    {
        return RequireSurfaceDirectionTweakSuccess(
            _liveRhinoDocumentAccessor.Execute(filePath, document =>
            {
                return OperationResponse<SurfaceDirectionTweakSnapshot>.Ok(new SurfaceDirectionTweakSnapshot
                {
                    ObjectCount = document.Objects.Count,
                    DocumentStringCount = document.Strings.Count
                });
            }),
            "CaptureSurfaceDirectionTweakSnapshot");
    }

    private IReadOnlyList<Guid> CreateSurfaceDirectionTweakSmokeObjects(string filePath)
    {
        return RequireSurfaceDirectionTweakSuccess(
            _liveRhinoDocumentAccessor.ExecuteWithUndo(filePath, "MCP:SurfaceDirectionTweakSmokeSetup", document =>
            {
                NurbsSurface? surface = NurbsSurface.CreateFromCorners(
                    new Point3d(0d, 0d, 0d),
                    new Point3d(0d, 3d, 0d),
                    new Point3d(0d, 3d, 2d),
                    new Point3d(0d, 0d, 2d));
                NurbsSurface? brepSurface = NurbsSurface.CreateFromCorners(
                    new Point3d(10d, 0d, 0d),
                    new Point3d(10d, 3d, 0d),
                    new Point3d(10d, 3d, 2d),
                    new Point3d(10d, 0d, 2d));
                if (surface is null || brepSurface is null)
                {
                    return OperationResponse<(bool Mutated, IReadOnlyList<Guid> Result)>.Fail("Failed to create smoke surfaces.");
                }

                Brep? brep = Brep.CreateFromSurface(brepSurface);
                if (brep is null)
                {
                    return OperationResponse<(bool Mutated, IReadOnlyList<Guid> Result)>.Fail("Failed to wrap smoke surface as Brep.");
                }

                Guid surfaceId = document.Objects.AddSurface(surface, CreateSurfaceDirectionTweakAttributes("surface"));
                Guid brepId = document.Objects.AddBrep(brep, CreateSurfaceDirectionTweakAttributes("brep"));
                if (surfaceId == Guid.Empty || brepId == Guid.Empty)
                {
                    return OperationResponse<(bool Mutated, IReadOnlyList<Guid> Result)>.Fail("Failed to add surface direction tweak smoke objects.");
                }

                document.Views.Redraw();
                return OperationResponse<(bool Mutated, IReadOnlyList<Guid> Result)>.Ok((true, new[] { surfaceId, brepId }));
            }),
            "CreateSurfaceDirectionTweakSmokeObjects");
    }

    private void DeleteSurfaceDirectionTweakSmokeObjects(string filePath, IReadOnlyList<Guid> objectIds)
    {
        OperationResponse<bool> delete = _liveRhinoDocumentAccessor.ExecuteWithUndo(
            filePath,
            "MCP:SurfaceDirectionTweakSmokeCleanup",
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

        RequireSurfaceDirectionTweakSuccess(delete, "DeleteSurfaceDirectionTweakSmokeObjects");
    }

    private static ObjectAttributes CreateSurfaceDirectionTweakAttributes(string suffix)
    {
        var attributes = new ObjectAttributes
        {
            Name = $"mcp-surface-direction-tweak-{suffix}",
            ObjectColor = Color.FromArgb(255, 170, 95, 30),
            ColorSource = ObjectColorSource.ColorFromObject
        };
        attributes.SetUserString("mcp-smoke", "surface-direction-tweak");
        return attributes;
    }

    private static double Dot(GeometryVectorData left, GeometryVectorData right)
    {
        return (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z);
    }

    private static T RequireSurfaceDirectionTweakSuccess<T>(OperationResponse<T> response, string operationName)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{operationName} failed: {response.Message}");
        }

        return response.Data;
    }

    private static void RequireSurfaceDirectionTweakFailure<T>(
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

    private static void RequireSurfaceDirectionTweak(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class SurfaceDirectionTweakSnapshot
    {
        public int ObjectCount { get; set; }
        public int DocumentStringCount { get; set; }
    }
}
