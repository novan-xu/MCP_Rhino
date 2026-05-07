using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Tools.Geometry;
using MCP_Rhino.Server.Tools.Layers;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private bool HandleLayerManagementSmokeTest(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: dotnet run --project src/MCP_Rhino.Server -- layer-management-smoke-test <3dm-file-path>");
            Environment.ExitCode = 1;
            return true;
        }

        try
        {
            string sourceFilePath = Path.GetFullPath(args[1]);
            if (!File.Exists(sourceFilePath))
            {
                Console.Error.WriteLine($"Smoke test source file was not found: {sourceFilePath}");
                Environment.ExitCode = 1;
                return true;
            }

            bool pluginMode = MCP_Rhino.Server.Infrastructure.Plugin.McpRhinoPlugin.Instance is not null;
            if (pluginMode)
            {
                RunLiveLayerManagementSmoke(sourceFilePath);
            }
            else
            {
                RunCliFallbackLayerManagementSmoke(sourceFilePath);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Layer management smoke test failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunCliFallbackLayerManagementSmoke(string sourceFilePath)
    {
        string workingFilePath = sourceFilePath;
        string firstLayerPath = "Default";
        string createLayerPath = "__CodexSmoke__Create::Leaf";
        var checkpoints = new List<string>();

        var getTool = new GetLayersTool(_layerManagementService);
        var createTool = new CreateLayersTool(_layerManagementService);
        var modifyTool = new ModifyLayersTool(_layerManagementService);
        var deleteTool = new DeleteLayersTool(_layerManagementService);
        var purgeTool = new PurgeLayersTool(_layerManagementService);
        var previewModifyTool = new PreviewModifyLayersTool(_layerManagementService);
        var previewDeleteTool = new PreviewDeleteLayersTool(_layerManagementService);
        var previewPurgeTool = new PreviewPurgeLayersTool(_layerManagementService);

        RequireLayerManagementFailureWithMessage(
            getTool.GetLayers(workingFilePath),
            "LIVE_RHINO_REQUIRED",
            "GetLayers should require live Rhino.");
        checkpoints.Add("GetLayers rejected in CLI fallback");

        RequireLayerManagementFailureWithMessage(
            createTool.CreateLayers(
                workingFilePath,
                new List<LayerCreationEntryRequest>
                {
                    new()
                    {
                        FullPath = createLayerPath
                    }
                }),
            "LIVE_RHINO_REQUIRED",
            "CreateLayers should require live Rhino.");
        checkpoints.Add("CreateLayers rejected in CLI fallback");

        RequireLayerManagementFailureWithMessage(
            modifyTool.ModifyLayers(
                workingFilePath,
                new List<LayerModificationEntryRequest>
                {
                    new()
                    {
                        FullPath = firstLayerPath,
                        NewName = "__CodexSmoke__Renamed"
                    }
                }),
            "LIVE_RHINO_REQUIRED",
            "ModifyLayers should require live Rhino.");
        checkpoints.Add("ModifyLayers rejected in CLI fallback");

        RequireLayerManagementFailureWithMessage(
            deleteTool.DeleteLayers(workingFilePath, new List<string> { firstLayerPath }),
            "LIVE_RHINO_REQUIRED",
            "DeleteLayers should require live Rhino.");
        checkpoints.Add("DeleteLayers rejected in CLI fallback");

        RequireLayerManagementFailureWithMessage(
            purgeTool.PurgeLayers(workingFilePath, new List<string> { firstLayerPath }),
            "LIVE_RHINO_REQUIRED",
            "PurgeLayers should require live Rhino.");
        checkpoints.Add("PurgeLayers rejected in CLI fallback");

        RequireLayerManagementFailureWithMessage(
            previewModifyTool.PreviewModifyLayers(
                workingFilePath,
                new List<LayerModificationEntryRequest>
                {
                    new()
                    {
                        FullPath = firstLayerPath,
                        Locked = true
                    }
                }),
            "LIVE_RHINO_REQUIRED",
            "PreviewModifyLayers should require live Rhino.");
        checkpoints.Add("PreviewModifyLayers rejected in CLI fallback");

        RequireLayerManagementFailureWithMessage(
            previewDeleteTool.PreviewDeleteLayers(workingFilePath, new List<string> { firstLayerPath }),
            "LIVE_RHINO_REQUIRED",
            "PreviewDeleteLayers should require live Rhino.");
        checkpoints.Add("PreviewDeleteLayers rejected in CLI fallback");

        RequireLayerManagementFailureWithMessage(
            previewPurgeTool.PreviewPurgeLayers(workingFilePath, new List<string> { firstLayerPath }),
            "LIVE_RHINO_REQUIRED",
            "PreviewPurgeLayers should require live Rhino.");
        checkpoints.Add("PreviewPurgeLayers rejected in CLI fallback");

        Console.WriteLine("Layer management smoke test completed successfully (CLI fallback mode).");
        Console.WriteLine($"Source: {sourceFilePath}");

        foreach (string checkpoint in checkpoints)
        {
            Console.WriteLine($"- {checkpoint}");
        }
    }

    private void RunLiveLayerManagementSmoke(string activeFilePath)
    {
        string slug = Guid.NewGuid().ToString("N")[..8];
        string root = $"__CodexSmoke__LayerMgmt_{slug}";
        string modifyTarget = $"{root}::ModifyTarget";
        string deleteTarget = $"{root}::DeleteTarget";
        string deleteChild = $"{deleteTarget}::Child";
        string purgeTarget = $"{root}::PurgeTarget";
        string purgeChild = $"{purgeTarget}::Child";
        var checkpoints = new List<string>();

        var getTool = new GetLayersTool(_layerManagementService);
        var createTool = new CreateLayersTool(_layerManagementService);
        var modifyTool = new ModifyLayersTool(_layerManagementService);
        var deleteTool = new DeleteLayersTool(_layerManagementService);
        var purgeTool = new PurgeLayersTool(_layerManagementService);
        var previewModifyTool = new PreviewModifyLayersTool(_layerManagementService);
        var previewDeleteTool = new PreviewDeleteLayersTool(_layerManagementService);
        var previewPurgeTool = new PreviewPurgeLayersTool(_layerManagementService);
        var createPointsTool = new CreatePointsTool(_geometryCreationSkill);

        bool cleanupPerformed = false;
        try
        {

        LayerReadResponse initialRead = RequireLayerManagementSuccess(getTool.GetLayers(activeFilePath), "GetLayers");
        RequireLayerManagement(initialRead.TotalCount > 0, "Live smoke requires a saved document with at least one layer.");
        checkpoints.Add("GetLayers ok");

        LayerMutationResponse createResponse = RequireLayerManagementSuccess(
            createTool.CreateLayers(
                activeFilePath,
                new List<LayerCreationEntryRequest>
                {
                    new() { FullPath = modifyTarget },
                    new() { FullPath = deleteChild },
                    new() { FullPath = purgeChild }
                }),
            "CreateLayers");
        RequireLayerManagement(createResponse.SucceededCount == 3, "CreateLayers should create all smoke-test layers in live mode.");
        checkpoints.Add("CreateLayers ok");

        GeometryCreationResponse createDeletePoint = RequireLayerManagementSuccess(
            createPointsTool.CreatePoints(
                activeFilePath,
                new List<PointItemRequest>
                {
                    new() { X = 101d, Y = 0d, Z = 0d }
                },
                new GeometryCreationCommonOptions
                {
                    LayerFullPath = deleteChild
                }),
            "CreatePoints(delete)");
        RequireLayerManagement(createDeletePoint.CreatedCount == 1, "Delete smoke point should be created.");

        GeometryCreationResponse createPurgePoint = RequireLayerManagementSuccess(
            createPointsTool.CreatePoints(
                activeFilePath,
                new List<PointItemRequest>
                {
                    new() { X = 102d, Y = 0d, Z = 0d }
                },
                new GeometryCreationCommonOptions
                {
                    LayerFullPath = purgeChild
                }),
            "CreatePoints(purge)");
        RequireLayerManagement(createPurgePoint.CreatedCount == 1, "Purge smoke point should be created.");
        checkpoints.Add("CreatePoints ok");

        LayerModificationPreviewResponse previewModify = RequireLayerManagementSuccess(
            previewModifyTool.PreviewModifyLayers(
                activeFilePath,
                new List<LayerModificationEntryRequest>
                {
                    new()
                    {
                        FullPath = modifyTarget,
                        NewName = "ModifyTargetRenamed",
                        Locked = true
                    }
                }),
            "PreviewModifyLayers");
        RequireLayerManagement(previewModify.Impacts.Count == 1 && previewModify.Impacts[0].Success, "PreviewModifyLayers should succeed in live mode.");
        checkpoints.Add("PreviewModifyLayers ok");

        LayerMutationResponse modifyResponse = RequireLayerManagementSuccess(
            modifyTool.ModifyLayers(
                activeFilePath,
                new List<LayerModificationEntryRequest>
                {
                    new()
                    {
                        FullPath = modifyTarget,
                        NewName = "ModifyTargetRenamed",
                        Locked = true
                    }
                }),
            "ModifyLayers");
        RequireLayerManagement(modifyResponse.SucceededCount == 1, "ModifyLayers should succeed in live mode.");
        checkpoints.Add("ModifyLayers ok");

        LayerDeletionPreviewResponse previewDelete = RequireLayerManagementSuccess(
            previewDeleteTool.PreviewDeleteLayers(activeFilePath, new List<string> { deleteTarget }),
            "PreviewDeleteLayers");
        RequireLayerManagement(
            previewDelete.Impacts.Count == 1 && previewDelete.Impacts[0].Success && previewDelete.Impacts[0].TotalAffectedObjectCount >= 1,
            "PreviewDeleteLayers should report at least one affected object.");
        checkpoints.Add("PreviewDeleteLayers ok");

        LayerMutationResponse deleteResponse = RequireLayerManagementSuccess(
            deleteTool.DeleteLayers(activeFilePath, new List<string> { deleteTarget }),
            "DeleteLayers");
        RequireLayerManagement(deleteResponse.SucceededCount == 1, "DeleteLayers should succeed in live mode.");
        checkpoints.Add("DeleteLayers ok");

        LayerDeletionPreviewResponse previewPurge = RequireLayerManagementSuccess(
            previewPurgeTool.PreviewPurgeLayers(activeFilePath, new List<string> { purgeTarget }),
            "PreviewPurgeLayers");
        RequireLayerManagement(
            previewPurge.Impacts.Count == 1 && previewPurge.Impacts[0].Success && previewPurge.Impacts[0].TotalAffectedObjectCount >= 1,
            "PreviewPurgeLayers should report at least one affected object.");
        checkpoints.Add("PreviewPurgeLayers ok");

        LayerMutationResponse purgeResponse = RequireLayerManagementSuccess(
            purgeTool.PurgeLayers(activeFilePath, new List<string> { purgeTarget }),
            "PurgeLayers(target)");
        RequireLayerManagement(purgeResponse.SucceededCount == 1, "PurgeLayers should succeed in live mode.");
        checkpoints.Add("PurgeLayers(target) ok");

            Console.WriteLine("Layer management smoke test completed successfully (live Rhino mode).");
            Console.WriteLine($"Active file: {activeFilePath}");
            Console.WriteLine($"Smoke root: {root}");
            foreach (string checkpoint in checkpoints)
            {
                Console.WriteLine($"- {checkpoint}");
            }
        }
        finally
        {
            // Best-effort cleanup: if any smoke step above threw, the user's real
            // document still has __CodexSmoke__LayerMgmt_<slug>* debris. Try once
            // to purge the smoke root; any failure is logged as a warning rather
            // than propagating, so the original exception (if any) still surfaces.
            try
            {
                OperationResponse<LayerMutationResponse> cleanup = purgeTool.PurgeLayers(activeFilePath, new List<string> { root });
                if (cleanup.Success && cleanup.Data is not null && cleanup.Data.SucceededCount >= 1)
                {
                    cleanupPerformed = true;
                    Console.WriteLine($"Cleanup: purged smoke root [{root}].");
                }
                else
                {
                    Console.Error.WriteLine($"Cleanup warning: failed to purge smoke root [{root}]: {cleanup.Message}");
                }
            }
            catch (Exception cleanupEx)
            {
                Console.Error.WriteLine($"Cleanup warning while purging [{root}]: {cleanupEx.Message}");
            }

            if (!cleanupPerformed)
            {
                Console.Error.WriteLine($"Manual cleanup needed: residual layers under [{root}] may remain in the active document.");
            }
        }
    }

    private static T RequireLayerManagementSuccess<T>(OperationResponse<T> response, string operationName)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{operationName} failed: {response.Message}");
        }

        return response.Data;
    }

    private static void RequireLayerManagementFailureWithMessage<T>(OperationResponse<T> response, string expectedMessage, string message)
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

    private static void RequireLayerManagement(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

}
