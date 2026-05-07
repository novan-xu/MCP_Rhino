extern alias rhinocommon;

using System.Drawing;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
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
    partial void RegisterStandardFourPointSurfaceRebuildHandlers()
    {
        _extensionHandlers["standard-four-point-surface-rebuild-smoke-test"] = HandleStandardFourPointSurfaceRebuildSmokeTest;
    }

    private bool HandleStandardFourPointSurfaceRebuildSmokeTest(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: dotnet run --project src/MCP_Rhino.Server -- standard-four-point-surface-rebuild-smoke-test <3dm-file-path>");
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
                RunCliFallbackStandardFourPointSurfaceRebuildSmoke(filePath);
            }
            else
            {
                RunLiveStandardFourPointSurfaceRebuildSmoke(filePath);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Standard four-point surface rebuild smoke test failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunCliFallbackStandardFourPointSurfaceRebuildSmoke(string filePath)
    {
        var standardTool = new ApplyStandardFourPointSurfaceRebuildTool(_standardFourPointSurfaceRebuildSkill);
        var directionTool = new ApplyTweakSurfaceDirectionsTool(_surfaceDirectionTweakOrchestrator);

        RequireStandardFourPointFailure(
            standardTool.ApplyStandardFourPointSurfaceRebuild(filePath, new[] { Guid.NewGuid() }),
            "LIVE_RHINO_REQUIRED",
            "ApplyStandardFourPointSurfaceRebuild should require live Rhino.");

        RequireStandardFourPointFailure(
            directionTool.ApplyTweakSurfaceDirections(filePath, new[] { Guid.NewGuid() }),
            "LIVE_RHINO_REQUIRED",
            "ApplyTweakSurfaceDirections default operation should require live Rhino.");

        Console.WriteLine("Standard four-point surface rebuild smoke test completed successfully (CLI fallback mode).");
        Console.WriteLine($"Source: {filePath}");
        Console.WriteLine("- ApplyStandardFourPointSurfaceRebuild rejected in CLI fallback");
        Console.WriteLine("- ApplyTweakSurfaceDirections default rejected in CLI fallback");
    }

    private void RunLiveStandardFourPointSurfaceRebuildSmoke(string filePath)
    {
        var checkpoints = new List<string>();
        var standardTool = new ApplyStandardFourPointSurfaceRebuildTool(_standardFourPointSurfaceRebuildSkill);
        var directionTool = new PreviewTweakSurfaceDirectionsTool(_surfaceDirectionTweakOrchestrator);

        StandardFourPointSurfaceRebuildSnapshot before = CaptureStandardFourPointSurfaceRebuildSnapshot(filePath);
        IReadOnlyList<Guid> objectIds = CreateStandardFourPointSurfaceRebuildSmokeObjects(filePath);

        try
        {
            SurfaceDirectionTweakPreviewResponse defaultDirectionPreview = RequireStandardFourPointSuccess(
                directionTool.PreviewTweakSurfaceDirections(filePath, new[] { objectIds[0] }),
                "PreviewTweakSurfaceDirections default");
            RequireStandardFourPoint(defaultDirectionPreview.Results.Count == 1, "Default direction preview should return one result.");
            RequireStandardFourPoint(
                defaultDirectionPreview.Results[0].Operations.SequenceEqual(new[]
                {
                    SurfaceDirectionTweakKind.SwapUV
                }),
                "Direction tweak default should be SwapUV.");
            checkpoints.Add("Direction tweak default resolves to SwapUV");

            StandardFourPointSurfaceRebuildResponse standard = RequireStandardFourPointSuccess(
                standardTool.ApplyStandardFourPointSurfaceRebuild(filePath, objectIds),
                "ApplyStandardFourPointSurfaceRebuild");
            RequireStandardFourPoint(string.Equals(standard.RebuildUndoRecordName, "MCP:RedefineSurfacePointOrder", StringComparison.Ordinal), "Standard rebuild should report rebuild undo name.");
            RequireStandardFourPoint(string.Equals(standard.FrontBackFlipUndoRecordName, "MCP:FlipSurfaceFrontBack", StringComparison.Ordinal), "Standard rebuild should report front/back flip undo name.");
            RequireStandardFourPoint(string.Equals(standard.DirectionUndoRecordName, "MCP:TweakSurfaceDirections", StringComparison.Ordinal), "Standard rebuild should report direction undo name.");
            RequireStandardFourPoint(standard.Results.Count == objectIds.Count, "Standard rebuild should return one result per target.");
            RequireStandardFourPoint(
                standard.PostRebuildDirectionOperations.SequenceEqual(new[]
                {
                    SurfaceDirectionTweakKind.SwapUV
                }),
                "Standard rebuild should use SwapUV as the Dir step.");

            foreach (StandardFourPointSurfaceRebuildItem result in standard.Results)
            {
                RequireStandardFourPoint(!result.RebuildSkipped, $"Target {result.ObjectId} should rebuild.");
                RequireStandardFourPoint(!result.FrontBackFlipSkipped, $"Target {result.ObjectId} should receive front/back flip.");
                RequireStandardFourPoint(!result.DirectionSkipped, $"Target {result.ObjectId} should receive post-rebuild direction tweak.");
                RequireStandardFourPoint(result.ObjectId == result.OriginalObjectId, "Standard rebuild should preserve ObjectId.");
            }
            checkpoints.Add("Standard rebuild applies point order, front/back flip, and SwapUV to all targets");
        }
        finally
        {
            DeleteStandardFourPointSurfaceRebuildSmokeObjects(filePath, objectIds);
        }

        StandardFourPointSurfaceRebuildSnapshot after = CaptureStandardFourPointSurfaceRebuildSnapshot(filePath);
        RequireStandardFourPoint(after.ObjectCount == before.ObjectCount, "Smoke should not leave temporary objects.");
        RequireStandardFourPoint(after.DocumentStringCount == before.DocumentStringCount, "Smoke should not change document strings.");
        checkpoints.Add("Temporary objects cleaned up");

        Console.WriteLine("Standard four-point surface rebuild smoke test completed successfully (live Rhino mode).");
        Console.WriteLine($"Active file: {filePath}");
        foreach (string checkpoint in checkpoints)
        {
            Console.WriteLine($"- {checkpoint}");
        }
    }

    private StandardFourPointSurfaceRebuildSnapshot CaptureStandardFourPointSurfaceRebuildSnapshot(string filePath)
    {
        return RequireStandardFourPointSuccess(
            _liveRhinoDocumentAccessor.Execute(filePath, document =>
            {
                return OperationResponse<StandardFourPointSurfaceRebuildSnapshot>.Ok(new StandardFourPointSurfaceRebuildSnapshot
                {
                    ObjectCount = document.Objects.Count,
                    DocumentStringCount = document.Strings.Count
                });
            }),
            "CaptureStandardFourPointSurfaceRebuildSnapshot");
    }

    private IReadOnlyList<Guid> CreateStandardFourPointSurfaceRebuildSmokeObjects(string filePath)
    {
        return RequireStandardFourPointSuccess(
            _liveRhinoDocumentAccessor.ExecuteWithUndo(filePath, "MCP:StandardFourPointSurfaceRebuildSmokeSetup", document =>
            {
                NurbsSurface? first = NurbsSurface.CreateFromCorners(
                    new Point3d(0d, 0d, 0d),
                    new Point3d(0d, 2d, 0d),
                    new Point3d(0d, 2d, 3d),
                    new Point3d(0d, 0d, 3d));
                NurbsSurface? secondSurface = NurbsSurface.CreateFromCorners(
                    new Point3d(10d, 0d, 0d),
                    new Point3d(10d, -2d, 0d),
                    new Point3d(10d, -2d, 3d),
                    new Point3d(10d, 0d, 3d));
                if (first is null || secondSurface is null)
                {
                    return OperationResponse<(bool Mutated, IReadOnlyList<Guid> Result)>.Fail("Failed to create standard rebuild smoke surfaces.");
                }

                Brep? second = Brep.CreateFromSurface(secondSurface);
                if (second is null)
                {
                    return OperationResponse<(bool Mutated, IReadOnlyList<Guid> Result)>.Fail("Failed to wrap smoke surface as Brep.");
                }

                Guid firstId = document.Objects.AddSurface(first, CreateStandardFourPointSurfaceRebuildAttributes("surface"));
                Guid secondId = document.Objects.AddBrep(second, CreateStandardFourPointSurfaceRebuildAttributes("brep"));
                if (firstId == Guid.Empty || secondId == Guid.Empty)
                {
                    return OperationResponse<(bool Mutated, IReadOnlyList<Guid> Result)>.Fail("Failed to add standard rebuild smoke objects.");
                }

                document.Views.Redraw();
                return OperationResponse<(bool Mutated, IReadOnlyList<Guid> Result)>.Ok((true, new[] { firstId, secondId }));
            }),
            "CreateStandardFourPointSurfaceRebuildSmokeObjects");
    }

    private void DeleteStandardFourPointSurfaceRebuildSmokeObjects(string filePath, IReadOnlyList<Guid> objectIds)
    {
        OperationResponse<bool> delete = _liveRhinoDocumentAccessor.ExecuteWithUndo(
            filePath,
            "MCP:StandardFourPointSurfaceRebuildSmokeCleanup",
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

        RequireStandardFourPointSuccess(delete, "DeleteStandardFourPointSurfaceRebuildSmokeObjects");
    }

    private static ObjectAttributes CreateStandardFourPointSurfaceRebuildAttributes(string suffix)
    {
        var attributes = new ObjectAttributes
        {
            Name = $"mcp-standard-four-point-rebuild-{suffix}",
            ObjectColor = Color.FromArgb(255, 95, 125, 35),
            ColorSource = ObjectColorSource.ColorFromObject
        };
        attributes.SetUserString("mcp-smoke", "standard-four-point-surface-rebuild");
        return attributes;
    }

    private static T RequireStandardFourPointSuccess<T>(OperationResponse<T> response, string operationName)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{operationName} failed: {response.Message}");
        }

        return response.Data;
    }

    private static void RequireStandardFourPointFailure<T>(
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

    private static void RequireStandardFourPoint(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class StandardFourPointSurfaceRebuildSnapshot
    {
        public int ObjectCount { get; set; }
        public int DocumentStringCount { get; set; }
    }
}
