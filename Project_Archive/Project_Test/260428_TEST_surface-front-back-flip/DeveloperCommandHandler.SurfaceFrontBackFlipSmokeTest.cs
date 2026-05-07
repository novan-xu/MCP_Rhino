extern alias rhinocommon;

using System.Drawing;
using MCP_Rhino.Server.Contracts.Responses;
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
    partial void RegisterSurfaceFrontBackFlipHandlers()
    {
        _extensionHandlers["surface-front-back-flip-smoke-test"] = HandleSurfaceFrontBackFlipSmokeTest;
    }

    private bool HandleSurfaceFrontBackFlipSmokeTest(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: dotnet run --project src/MCP_Rhino.Server -- surface-front-back-flip-smoke-test <3dm-file-path>");
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
                RunCliFallbackSurfaceFrontBackFlipSmoke(filePath);
            }
            else
            {
                RunLiveSurfaceFrontBackFlipSmoke(filePath);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Surface front/back flip smoke test failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunCliFallbackSurfaceFrontBackFlipSmoke(string filePath)
    {
        var flipTool = new ApplyFlipSurfaceFrontBackTool(_surfaceFrontBackFlipOrchestrator);

        RequireSurfaceFrontBackFlipFailure(
            flipTool.ApplyFlipSurfaceFrontBack(filePath, new[] { Guid.NewGuid() }),
            "LIVE_RHINO_REQUIRED",
            "ApplyFlipSurfaceFrontBack should require live Rhino.");

        Console.WriteLine("Surface front/back flip smoke test completed successfully (CLI fallback mode).");
        Console.WriteLine($"Source: {filePath}");
        Console.WriteLine("- ApplyFlipSurfaceFrontBack rejected in CLI fallback");
    }

    private void RunLiveSurfaceFrontBackFlipSmoke(string filePath)
    {
        var checkpoints = new List<string>();
        var flipTool = new ApplyFlipSurfaceFrontBackTool(_surfaceFrontBackFlipOrchestrator);

        SurfaceFrontBackFlipSnapshot before = CaptureSurfaceFrontBackFlipSnapshot(filePath);
        Guid objectId = CreateSurfaceFrontBackFlipSmokeObject(filePath);

        try
        {
            SurfaceFrontBackFlipApplyResponse apply = RequireSurfaceFrontBackFlipSuccess(
                flipTool.ApplyFlipSurfaceFrontBack(filePath, new[] { objectId }),
                "ApplyFlipSurfaceFrontBack");
            RequireSurfaceFrontBackFlip(string.Equals(apply.UndoRecordName, "MCP:FlipSurfaceFrontBack", StringComparison.Ordinal), "Apply should report front/back flip undo name.");
            RequireSurfaceFrontBackFlip(apply.Results.Count == 1, "Apply should return one result.");
            SurfaceFrontBackFlipApplyItem result = apply.Results[0];
            RequireSurfaceFrontBackFlip(!result.Skipped, "Front/back flip should not skip the smoke target.");
            RequireSurfaceFrontBackFlip(result.ObjectId == objectId, "Front/back flip should preserve ObjectId.");
            RequireSurfaceFrontBackFlip(
                FrontBackFlipDot(result.Before!.Normal, result.After!.Normal) < -0.99d
                || result.Before!.SolidOrientation != result.After!.SolidOrientation,
                "Front/back flip should reverse face normal or solid orientation snapshot.");
            checkpoints.Add("ApplyFlipSurfaceFrontBack flips the smoke target and preserves ObjectId");
        }
        finally
        {
            DeleteSurfaceFrontBackFlipSmokeObjects(filePath, new[] { objectId });
        }

        SurfaceFrontBackFlipSnapshot after = CaptureSurfaceFrontBackFlipSnapshot(filePath);
        RequireSurfaceFrontBackFlip(after.ObjectCount == before.ObjectCount, "Smoke should not leave temporary objects.");
        RequireSurfaceFrontBackFlip(after.DocumentStringCount == before.DocumentStringCount, "Smoke should not change document strings.");
        checkpoints.Add("Temporary objects cleaned up");

        Console.WriteLine("Surface front/back flip smoke test completed successfully (live Rhino mode).");
        Console.WriteLine($"Active file: {filePath}");
        foreach (string checkpoint in checkpoints)
        {
            Console.WriteLine($"- {checkpoint}");
        }
    }

    private SurfaceFrontBackFlipSnapshot CaptureSurfaceFrontBackFlipSnapshot(string filePath)
    {
        return RequireSurfaceFrontBackFlipSuccess(
            _liveRhinoDocumentAccessor.Execute(filePath, document =>
            {
                return OperationResponse<SurfaceFrontBackFlipSnapshot>.Ok(new SurfaceFrontBackFlipSnapshot
                {
                    ObjectCount = document.Objects.Count,
                    DocumentStringCount = document.Strings.Count
                });
            }),
            "CaptureSurfaceFrontBackFlipSnapshot");
    }

    private Guid CreateSurfaceFrontBackFlipSmokeObject(string filePath)
    {
        return RequireSurfaceFrontBackFlipSuccess(
            _liveRhinoDocumentAccessor.ExecuteWithUndo(filePath, "MCP:SurfaceFrontBackFlipSmokeSetup", document =>
            {
                NurbsSurface? surface = NurbsSurface.CreateFromCorners(
                    new Point3d(0d, 0d, 0d),
                    new Point3d(0d, 2d, 0d),
                    new Point3d(0d, 2d, 3d),
                    new Point3d(0d, 0d, 3d));
                if (surface is null)
                {
                    return OperationResponse<(bool Mutated, Guid Result)>.Fail("Failed to create front/back flip smoke surface.");
                }

                Brep? brep = Brep.CreateFromSurface(surface);
                if (brep is null)
                {
                    return OperationResponse<(bool Mutated, Guid Result)>.Fail("Failed to wrap front/back flip smoke surface as Brep.");
                }

                Guid objectId = document.Objects.AddBrep(brep, CreateSurfaceFrontBackFlipAttributes());
                if (objectId == Guid.Empty)
                {
                    return OperationResponse<(bool Mutated, Guid Result)>.Fail("Failed to add front/back flip smoke object.");
                }

                document.Views.Redraw();
                return OperationResponse<(bool Mutated, Guid Result)>.Ok((true, objectId));
            }),
            "CreateSurfaceFrontBackFlipSmokeObject");
    }

    private void DeleteSurfaceFrontBackFlipSmokeObjects(string filePath, IReadOnlyList<Guid> objectIds)
    {
        OperationResponse<bool> delete = _liveRhinoDocumentAccessor.ExecuteWithUndo(
            filePath,
            "MCP:SurfaceFrontBackFlipSmokeCleanup",
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

        RequireSurfaceFrontBackFlipSuccess(delete, "DeleteSurfaceFrontBackFlipSmokeObjects");
    }

    private static ObjectAttributes CreateSurfaceFrontBackFlipAttributes()
    {
        var attributes = new ObjectAttributes
        {
            Name = "mcp-surface-front-back-flip",
            ObjectColor = Color.FromArgb(255, 140, 70, 120),
            ColorSource = ObjectColorSource.ColorFromObject
        };
        attributes.SetUserString("mcp-smoke", "surface-front-back-flip");
        return attributes;
    }

    private static double FrontBackFlipDot(MCP_Rhino.Server.Domain.Models.GeometryVectorData left, MCP_Rhino.Server.Domain.Models.GeometryVectorData right)
    {
        return (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z);
    }

    private static T RequireSurfaceFrontBackFlipSuccess<T>(OperationResponse<T> response, string operationName)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{operationName} failed: {response.Message}");
        }

        return response.Data;
    }

    private static void RequireSurfaceFrontBackFlipFailure<T>(
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

    private static void RequireSurfaceFrontBackFlip(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class SurfaceFrontBackFlipSnapshot
    {
        public int ObjectCount { get; set; }
        public int DocumentStringCount { get; set; }
    }
}
