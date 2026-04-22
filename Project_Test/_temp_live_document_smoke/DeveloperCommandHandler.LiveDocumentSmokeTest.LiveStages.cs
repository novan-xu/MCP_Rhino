extern alias rhinocommon;

using System.Globalization;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using MCP_Rhino.Server.Tools.Editing;
using MCP_Rhino.Server.Tools.File;
using MCP_Rhino.Server.Tools.Geometry;
using MCP_Rhino.Server.Tools.Layers;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Infrastructure.CLI;

// Partial implementation of the 6-stage Live pass. Each stage calls report.Run
// per checkpoint so a single failing stage does not abort later stages.
// The orchestrator creates a smoke-root layer up front and purges it in a
// finally block so the fixture is left clean even after crashes.
public sealed partial class DeveloperCommandHandler
{
    private void RunLiveDocumentSmoke(string sourceFilePath, LiveSmokeReport report)
    {
        (int initialObjects, int initialLayers, uint initialUndoSerial) = CaptureLiveState();
        report.InitialObjectCount = initialObjects;
        report.InitialLayerCount = initialLayers;
        report.InitialUndoSerial = initialUndoSerial;

        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string smokeRoot = $"{LiveSmokeRootPrefix}{stamp}";
        string geomLayer = $"{smokeRoot}::Geom";
        string deleteLayer = $"{smokeRoot}::DeleteMe";
        string purgeLayer = $"{smokeRoot}::PurgeMe";
        string purgeGrandchild = $"{purgeLayer}::Target";

        var state = new LiveSmokeState
        {
            SmokeRoot = smokeRoot,
            GeomLayer = geomLayer,
            DeleteLayer = deleteLayer,
            PurgeLayer = purgeLayer,
            PurgeGrandchild = purgeGrandchild,
            FallbackLayerFullPath = SafeFindFallbackLayer()
        };

        try
        {
            SafeRunStage("Layer", report, () => RunLiveLayerStage(sourceFilePath, state, report));
            SafeRunStage("GeometryCreate", report, () => RunLiveGeometryCreateStage(sourceFilePath, state, report));
            SafeRunStage("GeometryModify", report, () => RunLiveGeometryModifyStage(sourceFilePath, state, report));
            SafeRunStage("ObjectEdits", report, () => RunLiveObjectEditsStage(sourceFilePath, state, report));
            SafeRunStage("ObjectUserText", report, () => RunLiveObjectUserTextStage(sourceFilePath, state, report));
            SafeRunStage("DocumentUserString", report, () => RunLiveDocumentUserStringStage(sourceFilePath, state, report));
        }
        finally
        {
            TryCleanupSmokeRoot(sourceFilePath, smokeRoot);

            (int finalObjects, int finalLayers, uint finalUndoSerial) = CaptureLiveState();
            report.FinalObjectCount = finalObjects;
            report.FinalLayerCount = finalLayers;
            report.FinalUndoSerial = finalUndoSerial;
        }
    }

    // Wraps a stage invocation so an unhandled exception inside it gets recorded
    // as a FAIL checkpoint instead of aborting the remaining stages.
    private static void SafeRunStage(string stageName, LiveSmokeReport report, Action stage)
    {
        try
        {
            stage();
        }
        catch (Exception ex)
        {
            report.Run(
                stage: stageName,
                feature: "StageOrchestrationFailure",
                codeLocations: new[]
                {
                    "Project_Test/_temp_live_document_smoke/DeveloperCommandHandler.LiveDocumentSmokeTest.LiveStages.cs"
                },
                input: "(internal)",
                expected: "Stage must complete without unhandled exceptions.",
                suspects: new[]
                {
                    "Unhandled exception escaped a stage method — see Failure reason.",
                    "Stage-level precondition logic (should live inside report.Run bodies)."
                },
                body: _ => throw new InvalidOperationException(ex.ToString()));
        }
    }

    // ----- Stage 1: Layer management --------------------------------------

    private void RunLiveLayerStage(string sourceFilePath, LiveSmokeState state, LiveSmokeReport report)
    {
        string[] locations =
        {
            "src/MCP_Rhino.Server/Tools/Layers/*.cs",
            "src/MCP_Rhino.Server/Application/Services/RhinoLayerManagementService.cs",
            "src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoDocumentAccessor.cs"
        };

        var createLayersTool = new CreateLayersTool(_layerManagementService);
        report.Run(
            stage: "Layer",
            feature: "CreateLayers: smoke root + 3 children + grandchild",
            codeLocations: locations,
            input: $"{state.SmokeRoot}, {state.GeomLayer}, {state.DeleteLayer}, {state.PurgeLayer}, {state.PurgeGrandchild}",
            expected: "Success=true; 5 layers created; UndoDelta > 0",
            suspects: new[]
            {
                "RhinoLayerManagementService.Create live path",
                "LiveRhinoLayerMutator.CreateLayers"
            },
            body: cp =>
            {
                uint before = CaptureUndoSerial();
                var resp = createLayersTool.CreateLayers(
                    sourceFilePath,
                    new List<LayerCreationEntryRequest>
                    {
                        new() { FullPath = state.SmokeRoot },
                        new() { FullPath = state.GeomLayer },
                        new() { FullPath = state.DeleteLayer },
                        new() { FullPath = state.PurgeLayer },
                        new() { FullPath = state.PurgeGrandchild }
                    });
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                if (resp.Data!.SucceededCount < 5)
                {
                    throw new InvalidOperationException($"Expected 5 layers created, got {resp.Data.SucceededCount}.");
                }
                cp.Evidence = $"SucceededCount={resp.Data.SucceededCount}, ChangedCount={resp.Data.ChangedCount}";
            });

        var getLayersTool = new GetLayersTool(_layerManagementService);
        report.Run(
            stage: "Layer",
            feature: "GetLayers: smoke layers visible",
            codeLocations: locations,
            input: $"filePath={sourceFilePath}",
            expected: "All 5 smoke layers appear in result.",
            suspects: new[] { "RhinoLayerManagementService.Get live path" },
            body: cp =>
            {
                uint before = CaptureUndoSerial();
                var resp = getLayersTool.GetLayers(sourceFilePath);
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                int smokeHits = resp.Data!.Entries.Count(e => e.FullPath.StartsWith(state.SmokeRoot, StringComparison.Ordinal));
                cp.ObservedDataSummary = $"TotalCount={resp.Data.TotalCount}, SmokeLayerHits={smokeHits}";
                if (smokeHits < 5)
                {
                    throw new InvalidOperationException($"Expected ≥5 smoke layers visible, got {smokeHits}.");
                }
                cp.Evidence = $"{smokeHits} smoke layers visible.";
            });

        var previewModifyTool = new PreviewModifyLayersTool(_layerManagementService);
        report.Run(
            stage: "Layer",
            feature: "PreviewModifyLayers: rename DeleteMe to DeleteMeRenamed",
            codeLocations: locations,
            input: $"target={state.DeleteLayer}, NewName=DeleteMeRenamed",
            expected: "Success, no mutation (UndoDelta=0 ideally).",
            suspects: new[] { "RhinoLayerManagementService.PreviewModify" },
            body: cp =>
            {
                uint before = CaptureUndoSerial();
                var resp = previewModifyTool.PreviewModifyLayers(
                    sourceFilePath,
                    new List<LayerModificationEntryRequest>
                    {
                        new() { FullPath = state.DeleteLayer, NewName = "DeleteMeRenamed" }
                    });
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                cp.ObservedDataSummary = $"Impacts={resp.Data!.Impacts.Count}";
                cp.Evidence = $"Preview produced {resp.Data.Impacts.Count} impact(s).";
            });

        var modifyLayersTool = new ModifyLayersTool(_layerManagementService);
        string renamedDeleteLayer = $"{state.SmokeRoot}::DeleteMeRenamed";
        report.Run(
            stage: "Layer",
            feature: "ModifyLayers: apply rename",
            codeLocations: locations,
            input: $"target={state.DeleteLayer} → NewName=DeleteMeRenamed",
            expected: "SucceededCount>=1, UndoDelta>0, renamed layer visible.",
            suspects: new[] { "RhinoLayerManagementService.Modify live path" },
            body: cp =>
            {
                uint before = CaptureUndoSerial();
                var resp = modifyLayersTool.ModifyLayers(
                    sourceFilePath,
                    new List<LayerModificationEntryRequest>
                    {
                        new() { FullPath = state.DeleteLayer, NewName = "DeleteMeRenamed" }
                    });
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                RequireUndoDeltaPositive(cp);
                if (resp.Data!.SucceededCount < 1)
                {
                    throw new InvalidOperationException($"Expected SucceededCount>=1, got {resp.Data.SucceededCount}.");
                }
                state.DeleteLayer = renamedDeleteLayer;
                cp.Evidence = $"Renamed, ChangedCount={resp.Data.ChangedCount}.";
            });

        var previewDeleteTool = new PreviewDeleteLayersTool(_layerManagementService);
        report.Run(
            stage: "Layer",
            feature: "PreviewDeleteLayers",
            codeLocations: locations,
            input: $"paths=[{state.DeleteLayer}]",
            expected: "Preview returns impacts with no mutation.",
            suspects: new[] { "RhinoLayerManagementService.PreviewDelete" },
            body: cp =>
            {
                uint before = CaptureUndoSerial();
                var resp = previewDeleteTool.PreviewDeleteLayers(sourceFilePath, new List<string> { state.DeleteLayer });
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                cp.ObservedDataSummary = $"Impacts={resp.Data!.Impacts.Count}";
                cp.Evidence = $"Preview produced {resp.Data.Impacts.Count} impact(s).";
            });

        var deleteLayersTool = new DeleteLayersTool(_layerManagementService);
        report.Run(
            stage: "Layer",
            feature: "DeleteLayers: apply",
            codeLocations: locations,
            input: $"paths=[{state.DeleteLayer}]",
            expected: "SucceededCount>=1, UndoDelta>0, layer no longer visible.",
            suspects: new[] { "RhinoLayerManagementService.Delete live path" },
            body: cp =>
            {
                uint before = CaptureUndoSerial();
                var resp = deleteLayersTool.DeleteLayers(sourceFilePath, new List<string> { state.DeleteLayer });
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                RequireUndoDeltaPositive(cp);
                cp.Evidence = $"Deleted. SucceededCount={resp.Data!.SucceededCount}.";
            });

        var previewPurgeTool = new PreviewPurgeLayersTool(_layerManagementService);
        report.Run(
            stage: "Layer",
            feature: "PreviewPurgeLayers: subtree",
            codeLocations: locations,
            input: $"paths=[{state.PurgeLayer}]",
            expected: "Preview returns subtree impacts with no mutation.",
            suspects: new[] { "RhinoLayerManagementService.PreviewPurge" },
            body: cp =>
            {
                uint before = CaptureUndoSerial();
                var resp = previewPurgeTool.PreviewPurgeLayers(sourceFilePath, new List<string> { state.PurgeLayer });
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                cp.ObservedDataSummary = $"Impacts={resp.Data!.Impacts.Count}";
                cp.Evidence = $"Preview produced {resp.Data.Impacts.Count} impact(s).";
            });

        var purgeLayersTool = new PurgeLayersTool(_layerManagementService);
        report.Run(
            stage: "Layer",
            feature: "PurgeLayers: subtree apply",
            codeLocations: locations,
            input: $"paths=[{state.PurgeLayer}]",
            expected: "SucceededCount>=1 (subtree purged), UndoDelta>0.",
            suspects: new[] { "RhinoLayerManagementService.Purge live path" },
            body: cp =>
            {
                uint before = CaptureUndoSerial();
                var resp = purgeLayersTool.PurgeLayers(sourceFilePath, new List<string> { state.PurgeLayer });
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                RequireUndoDeltaPositive(cp);
                cp.Evidence = $"Purged. SucceededCount={resp.Data!.SucceededCount}.";
            });
    }

    // ----- Stage 2: Geometry creation -------------------------------------

    private void RunLiveGeometryCreateStage(string sourceFilePath, LiveSmokeState state, LiveSmokeReport report)
    {
        string[] locations =
        {
            "src/MCP_Rhino.Server/Tools/Geometry/Create*Tool.cs",
            "src/MCP_Rhino.Server/Skills/Modeling/GeometryCreationSkill.cs",
            "src/MCP_Rhino.Server/Application/Services/RhinoGeometryCreationService.cs",
            "src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoGeometryBuilder.cs"
        };

        var commonOptions = new GeometryCreationCommonOptions { LayerFullPath = state.GeomLayer };

        var createPointsTool = new CreatePointsTool(_geometryCreationSkill);
        report.Run(
            stage: "GeometryCreate",
            feature: "CreatePoints",
            codeLocations: locations,
            input: $"layer={state.GeomLayer}, points=3",
            expected: "3 points created; UndoDelta>0.",
            suspects: new[] { "LiveRhinoGeometryBuilder.CreatePoints" },
            body: cp =>
            {
                uint before = CaptureUndoSerial();
                var resp = createPointsTool.CreatePoints(
                    sourceFilePath,
                    new List<PointItemRequest>
                    {
                        new() { X = 0, Y = 0, Z = 0 },
                        new() { X = 1, Y = 1, Z = 0 },
                        new() { X = 2, Y = 2, Z = 0 }
                    },
                    commonOptions);
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                RequireUndoDeltaPositive(cp);
                RecordCreatedIds(state, resp.Data!);
                cp.ObservedDataSummary = $"CreatedCount={resp.Data!.CreatedCount}, CreatedObjects.Count={resp.Data.CreatedObjects.Count}";
                cp.Evidence = $"CreatedCount={resp.Data.CreatedCount}, ids.recorded={resp.Data.CreatedObjects.Count}.";
                if (resp.Data.CreatedCount > 0 && resp.Data.CreatedObjects.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Live create returned CreatedCount>0 but CreatedObjects is empty — downstream stages will have no targets. " +
                        "Inspect RhinoGeometryCreationService / LiveRhinoGeometryBuilder: the CreatedObjects list is not being populated on the Live path.");
                }
            });

        var createLinesTool = new CreateLinesTool(_geometryCreationSkill);
        report.Run(
            stage: "GeometryCreate",
            feature: "CreateLines",
            codeLocations: locations,
            input: $"layer={state.GeomLayer}, lines=2",
            expected: "2 lines created; UndoDelta>0.",
            suspects: new[] { "LiveRhinoGeometryBuilder.CreateLines" },
            body: cp =>
            {
                uint before = CaptureUndoSerial();
                var resp = createLinesTool.CreateLines(
                    sourceFilePath,
                    new List<LineItemRequest>
                    {
                        new() { StartX = 0, StartY = 0, StartZ = 0, EndX = 10, EndY = 0, EndZ = 0 },
                        new() { StartX = 0, StartY = 0, StartZ = 0, EndX = 0, EndY = 10, EndZ = 0 }
                    },
                    commonOptions);
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                RequireUndoDeltaPositive(cp);
                RecordCreatedIds(state, resp.Data!);
                cp.ObservedDataSummary = $"CreatedCount={resp.Data!.CreatedCount}, CreatedObjects.Count={resp.Data.CreatedObjects.Count}";
                cp.Evidence = $"CreatedCount={resp.Data.CreatedCount}, ids.recorded={resp.Data.CreatedObjects.Count}.";
                if (resp.Data.CreatedCount > 0 && resp.Data.CreatedObjects.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Live create returned CreatedCount>0 but CreatedObjects is empty — downstream stages will have no targets. " +
                        "Inspect RhinoGeometryCreationService / LiveRhinoGeometryBuilder: the CreatedObjects list is not being populated on the Live path.");
                }
            });

        var createArcsTool = new CreateArcsTool(_geometryCreationSkill);
        report.Run(
            stage: "GeometryCreate",
            feature: "CreateArcs ThreePoint",
            codeLocations: locations,
            input: "ThreePoint arc",
            expected: "1 arc created; UndoDelta>0.",
            suspects: new[] { "LiveRhinoGeometryBuilder.CreateArcs ThreePoint" },
            body: cp =>
            {
                uint before = CaptureUndoSerial();
                var resp = createArcsTool.CreateArcs(
                    sourceFilePath,
                    new List<ArcItemRequest>
                    {
                        new()
                        {
                            Mode = ArcConstructionMode.ThreePoint,
                            StartX = 0, StartY = 0, StartZ = 0,
                            MidX = 5, MidY = 5, MidZ = 0,
                            EndX = 10, EndY = 0, EndZ = 0
                        }
                    },
                    commonOptions);
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                RequireUndoDeltaPositive(cp);
                RecordCreatedIds(state, resp.Data!);
                cp.ObservedDataSummary = $"CreatedCount={resp.Data!.CreatedCount}, CreatedObjects.Count={resp.Data.CreatedObjects.Count}";
                cp.Evidence = $"CreatedCount={resp.Data.CreatedCount}, ids.recorded={resp.Data.CreatedObjects.Count}.";
                if (resp.Data.CreatedCount > 0 && resp.Data.CreatedObjects.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Live create returned CreatedCount>0 but CreatedObjects is empty — downstream stages will have no targets. " +
                        "Inspect RhinoGeometryCreationService / LiveRhinoGeometryBuilder: the CreatedObjects list is not being populated on the Live path.");
                }
            });

        report.Run(
            stage: "GeometryCreate",
            feature: "CreateArcs CenterRadius",
            codeLocations: locations,
            input: "CenterRadius arc",
            expected: "1 arc created; UndoDelta>0.",
            suspects: new[] { "LiveRhinoGeometryBuilder.CreateArcs CenterRadius" },
            body: cp =>
            {
                uint before = CaptureUndoSerial();
                var resp = createArcsTool.CreateArcs(
                    sourceFilePath,
                    new List<ArcItemRequest>
                    {
                        new()
                        {
                            Mode = ArcConstructionMode.CenterRadius,
                            CenterX = 0, CenterY = 0, CenterZ = 0,
                            Radius = 5,
                            NormalX = 0, NormalY = 0, NormalZ = 1,
                            StartAngleRadians = 0,
                            EndAngleRadians = Math.PI
                        }
                    },
                    commonOptions);
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                RequireUndoDeltaPositive(cp);
                RecordCreatedIds(state, resp.Data!);
                cp.ObservedDataSummary = $"CreatedCount={resp.Data!.CreatedCount}, CreatedObjects.Count={resp.Data.CreatedObjects.Count}";
                cp.Evidence = $"CreatedCount={resp.Data.CreatedCount}, ids.recorded={resp.Data.CreatedObjects.Count}.";
                if (resp.Data.CreatedCount > 0 && resp.Data.CreatedObjects.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Live create returned CreatedCount>0 but CreatedObjects is empty — downstream stages will have no targets. " +
                        "Inspect RhinoGeometryCreationService / LiveRhinoGeometryBuilder: the CreatedObjects list is not being populated on the Live path.");
                }
            });

        var createSurfacesTool = new CreateSurfacesTool(_geometryCreationSkill);
        report.Run(
            stage: "GeometryCreate",
            feature: "CreateSurfaces FourCorners",
            codeLocations: locations,
            input: "FourCorners unit square",
            expected: "1 surface created; UndoDelta>0.",
            suspects: new[] { "LiveRhinoGeometryBuilder.CreateSurfaces FourCorners" },
            body: cp =>
            {
                uint before = CaptureUndoSerial();
                var resp = createSurfacesTool.CreateSurfaces(
                    sourceFilePath,
                    new List<SurfaceItemRequest>
                    {
                        new()
                        {
                            Mode = SurfaceConstructionMode.FourCorners,
                            Corner0X = 0, Corner0Y = 0, Corner0Z = 0,
                            Corner1X = 10, Corner1Y = 0, Corner1Z = 0,
                            Corner2X = 10, Corner2Y = 10, Corner2Z = 0,
                            Corner3X = 0, Corner3Y = 10, Corner3Z = 0
                        }
                    },
                    commonOptions);
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                RequireUndoDeltaPositive(cp);
                RecordCreatedIds(state, resp.Data!);
                cp.ObservedDataSummary = $"CreatedCount={resp.Data!.CreatedCount}, CreatedObjects.Count={resp.Data.CreatedObjects.Count}";
                cp.Evidence = $"CreatedCount={resp.Data.CreatedCount}, ids.recorded={resp.Data.CreatedObjects.Count}.";
                if (resp.Data.CreatedCount > 0 && resp.Data.CreatedObjects.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Live create returned CreatedCount>0 but CreatedObjects is empty — downstream stages will have no targets. " +
                        "Inspect RhinoGeometryCreationService / LiveRhinoGeometryBuilder: the CreatedObjects list is not being populated on the Live path.");
                }
            });

        report.Run(
            stage: "GeometryCreate",
            feature: "CreateSurfaces Plane",
            codeLocations: locations,
            input: "Plane origin=(20,20,0)",
            expected: "1 surface created; UndoDelta>0.",
            suspects: new[] { "LiveRhinoGeometryBuilder.CreateSurfaces Plane" },
            body: cp =>
            {
                uint before = CaptureUndoSerial();
                var resp = createSurfacesTool.CreateSurfaces(
                    sourceFilePath,
                    new List<SurfaceItemRequest>
                    {
                        new()
                        {
                            Mode = SurfaceConstructionMode.Plane,
                            OriginX = 20, OriginY = 20, OriginZ = 0,
                            NormalX = 0, NormalY = 0, NormalZ = 1,
                            ULength = 5, VLength = 5
                        }
                    },
                    commonOptions);
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                RequireUndoDeltaPositive(cp);
                RecordCreatedIds(state, resp.Data!);
                cp.ObservedDataSummary = $"CreatedCount={resp.Data!.CreatedCount}, CreatedObjects.Count={resp.Data.CreatedObjects.Count}";
                cp.Evidence = $"CreatedCount={resp.Data.CreatedCount}, ids.recorded={resp.Data.CreatedObjects.Count}.";
                if (resp.Data.CreatedCount > 0 && resp.Data.CreatedObjects.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Live create returned CreatedCount>0 but CreatedObjects is empty — downstream stages will have no targets. " +
                        "Inspect RhinoGeometryCreationService / LiveRhinoGeometryBuilder: the CreatedObjects list is not being populated on the Live path.");
                }
            });
    }

    // ----- Stage 3: Geometry modification ---------------------------------

    private void RunLiveGeometryModifyStage(string sourceFilePath, LiveSmokeState state, LiveSmokeReport report)
    {
        string[] locations =
        {
            "src/MCP_Rhino.Server/Tools/Geometry/{Transform,Replace,Delete,EditControlPoints}*.cs",
            "src/MCP_Rhino.Server/Skills/Modeling/GeometryModificationSkill.cs",
            "src/MCP_Rhino.Server/Application/Services/RhinoGeometryModificationService.cs",
            "src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoGeometryMutator.cs"
        };

        var previewTransformTool = new PreviewTransformObjectsTool(_geometryModificationSkill);
        var transformTool = new TransformObjectsTool(_geometryModificationSkill);

        report.Run(
            stage: "GeometryModify",
            feature: "PreviewTransformObjects Translate",
            codeLocations: locations,
            input: "translate vx=1, target=first created point",
            expected: "Preview success; no mutation.",
            suspects: new[] { "GeometryModificationSkill.Preview(PreviewTransformObjectsRequest)" },
            body: cp =>
            {
                Guid target = RequireCreatedTarget(state.CreatedPointIds, 0, "No point was created — cannot test transform preview.");
                uint before = CaptureUndoSerial();
                var resp = previewTransformTool.PreviewTransformObjects(
                    sourceFilePath,
                    new GeometryTransformSpec { Kind = GeometryTransformKind.Translate, VectorX = 1 },
                    confirmedObjectIds: new List<Guid> { target });
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                cp.Evidence = $"MatchedObjectCount={resp.Data!.MatchedObjectCount}.";
            });

        report.Run(
            stage: "GeometryModify",
            feature: "TransformObjects Translate apply",
            codeLocations: locations,
            input: "translate vx=1, target=first created point",
            expected: "Update success; UndoDelta>0.",
            suspects: new[] { "LiveRhinoGeometryMutator.Transform Translate" },
            body: cp =>
            {
                Guid target = RequireCreatedTarget(state.CreatedPointIds, 0, "No point was created — cannot test translate apply.");
                uint before = CaptureUndoSerial();
                var resp = transformTool.TransformObjects(
                    sourceFilePath,
                    new GeometryTransformSpec { Kind = GeometryTransformKind.Translate, VectorX = 1 },
                    confirmedObjectIds: new List<Guid> { target });
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                RequireUndoDeltaPositive(cp);
                cp.Evidence = $"UpdatedObjectCount={resp.Data!.UpdatedObjectCount}.";
            });

        report.Run(
            stage: "GeometryModify",
            feature: "TransformObjects Rotate apply",
            codeLocations: locations,
            input: "rotate Pi/4 about Z-axis, target=first created line",
            expected: "Update success; UndoDelta>0.",
            suspects: new[] { "LiveRhinoGeometryMutator.Transform Rotate" },
            body: cp =>
            {
                Guid target = RequireCreatedTarget(state.CreatedLineIds, 0, "No line was created — cannot test rotate.");
                uint before = CaptureUndoSerial();
                var resp = transformTool.TransformObjects(
                    sourceFilePath,
                    new GeometryTransformSpec
                    {
                        Kind = GeometryTransformKind.Rotate,
                        AxisX = 0, AxisY = 0, AxisZ = 1,
                        CenterX = 0, CenterY = 0, CenterZ = 0,
                        AngleRadians = Math.PI / 4
                    },
                    confirmedObjectIds: new List<Guid> { target });
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                RequireUndoDeltaPositive(cp);
                cp.Evidence = $"UpdatedObjectCount={resp.Data!.UpdatedObjectCount}.";
            });

        report.Run(
            stage: "GeometryModify",
            feature: "TransformObjects UniformScale apply",
            codeLocations: locations,
            input: "scale 2x about origin, target=second created line",
            expected: "Update success; UndoDelta>0.",
            suspects: new[] { "LiveRhinoGeometryMutator.Transform UniformScale" },
            body: cp =>
            {
                Guid target = RequireCreatedTarget(state.CreatedLineIds, 1, "No second line was created — cannot test scale.");
                uint before = CaptureUndoSerial();
                var resp = transformTool.TransformObjects(
                    sourceFilePath,
                    new GeometryTransformSpec
                    {
                        Kind = GeometryTransformKind.UniformScale,
                        CenterX = 0, CenterY = 0, CenterZ = 0,
                        ScaleFactor = 2
                    },
                    confirmedObjectIds: new List<Guid> { target });
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                RequireUndoDeltaPositive(cp);
                cp.Evidence = $"UpdatedObjectCount={resp.Data!.UpdatedObjectCount}.";
            });

        var previewReplaceTool = new PreviewReplaceGeometryTool(_geometryModificationSkill);
        var replaceTool = new ReplaceGeometryTool(_geometryModificationSkill);

        report.Run(
            stage: "GeometryModify",
            feature: "PreviewReplaceGeometry: point → line",
            codeLocations: locations,
            input: "target=second created point, new=Line",
            expected: "Preview success, no mutation.",
            suspects: new[] { "GeometryModificationSkill.Preview(PreviewReplaceGeometryRequest)" },
            body: cp =>
            {
                Guid target = RequireCreatedTarget(state.CreatedPointIds, 1, "No second point was created — cannot test replace preview.");
                uint before = CaptureUndoSerial();
                var resp = previewReplaceTool.PreviewReplaceGeometry(
                    sourceFilePath,
                    new List<GeometryReplacementEntryRequest>
                    {
                        new()
                        {
                            ObjectId = target,
                            Geometry = new GeometryCreationSpec
                            {
                                Primitive = GeometryPrimitiveKind.Line,
                                StartX = 0, StartY = 0, StartZ = 0,
                                EndX = 3, EndY = 4, EndZ = 0
                            }
                        }
                    });
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                cp.Evidence = $"PreviewObjectCount={resp.Data!.PreviewObjectCount}.";
            });

        report.Run(
            stage: "GeometryModify",
            feature: "ReplaceGeometry apply: point → line",
            codeLocations: locations,
            input: "target=second created point, new=Line",
            expected: "Update success; UndoDelta>0.",
            suspects: new[] { "LiveRhinoGeometryMutator.Replace" },
            body: cp =>
            {
                Guid target = RequireCreatedTarget(state.CreatedPointIds, 1, "No second point was created — cannot test replace apply.");
                uint before = CaptureUndoSerial();
                var resp = replaceTool.ReplaceGeometry(
                    sourceFilePath,
                    new List<GeometryReplacementEntryRequest>
                    {
                        new()
                        {
                            ObjectId = target,
                            Geometry = new GeometryCreationSpec
                            {
                                Primitive = GeometryPrimitiveKind.Line,
                                StartX = 0, StartY = 0, StartZ = 0,
                                EndX = 3, EndY = 4, EndZ = 0
                            }
                        }
                    });
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                RequireUndoDeltaPositive(cp);
                cp.Evidence = $"UpdatedObjectCount={resp.Data!.UpdatedObjectCount}.";
            });

        var previewDeleteTool = new PreviewDeleteObjectsTool(_geometryModificationSkill);
        var deleteTool = new DeleteObjectsTool(_geometryModificationSkill);

        report.Run(
            stage: "GeometryModify",
            feature: "PreviewDeleteObjects",
            codeLocations: locations,
            input: "target=third created point",
            expected: "Preview success, no mutation.",
            suspects: new[] { "GeometryModificationSkill.Preview(PreviewDeleteObjectsRequest)" },
            body: cp =>
            {
                Guid target = RequireCreatedTarget(state.CreatedPointIds, 2, "No third point was created — cannot test delete preview.");
                uint before = CaptureUndoSerial();
                var resp = previewDeleteTool.PreviewDeleteObjects(
                    sourceFilePath,
                    confirmedObjectIds: new List<Guid> { target });
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                cp.Evidence = $"PreviewObjectCount={resp.Data!.PreviewObjectCount}.";
            });

        report.Run(
            stage: "GeometryModify",
            feature: "DeleteObjects apply",
            codeLocations: locations,
            input: "target=third created point",
            expected: "Update success; UndoDelta>0.",
            suspects: new[] { "LiveRhinoGeometryMutator.Delete" },
            body: cp =>
            {
                Guid target = RequireCreatedTarget(state.CreatedPointIds, 2, "No third point was created — cannot test delete apply.");
                uint before = CaptureUndoSerial();
                var resp = deleteTool.DeleteObjects(
                    sourceFilePath,
                    confirmedObjectIds: new List<Guid> { target });
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                RequireUndoDeltaPositive(cp);
                cp.Evidence = $"UpdatedObjectCount={resp.Data!.UpdatedObjectCount}.";
            });
    }

    // ----- Stage 4: Object edits (via agent) ------------------------------

    private void RunLiveObjectEditsStage(string sourceFilePath, LiveSmokeState state, LiveSmokeReport report)
    {
        string[] locations =
        {
            "src/MCP_Rhino.Server/Agents/Editing/RhinoObjectEditingAgent.cs",
            "src/MCP_Rhino.Server/Skills/Editing/ObjectEdit{Preview,Apply}Skill.cs",
            "src/MCP_Rhino.Server/Application/Services/RhinoObjectEditingService.cs",
            "src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoObjectEditOperationApplier.cs"
        };

        string tempLayer = $"{state.SmokeRoot}::TempMove";

        report.Run(
            stage: "ObjectEdits",
            feature: "SetUserText via agent.Apply",
            codeLocations: locations,
            input: "target=first created line, key=smoke_key, value=smoke_value",
            expected: "UpdatedObjectCount>=1; UndoDelta>0.",
            suspects: new[] { "LiveRhinoObjectEditOperationApplier SetUserText branch" },
            body: cp =>
            {
                RequireAnyCreated(state.CreatedLineIds, "No lines were created — cannot probe SetUserText.");
                uint before = CaptureUndoSerial();
                var resp = _editingAgent.Apply(new ApplyObjectEditsRequest
                {
                    FilePath = sourceFilePath,
                    ConfirmedLayerFullPaths = new List<string> { state.GeomLayer },
                    Operations = new List<ObjectEditOperationRequest>
                    {
                        new() { OperationType = ObjectEditOperationType.SetUserText, Key = "smoke_key", Value = "smoke_value" }
                    }
                });
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                RequireUndoDeltaPositive(cp);
                cp.Evidence = $"UpdatedObjectCount={resp.Data!.UpdatedObjectCount}.";
            });

        var createLayersTool = new CreateLayersTool(_layerManagementService);
        report.Run(
            stage: "ObjectEdits",
            feature: "Prep: create temporary move-target layer",
            codeLocations: new[] { "src/MCP_Rhino.Server/Tools/Layers/CreateLayersTool.cs" },
            input: tempLayer,
            expected: "Layer created.",
            suspects: new[] { "(prep step — not a feature under test)" },
            body: cp =>
            {
                var resp = createLayersTool.CreateLayers(
                    sourceFilePath,
                    new List<LayerCreationEntryRequest> { new() { FullPath = tempLayer } });
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                cp.Evidence = "Prep layer created.";
            });

        report.Run(
            stage: "ObjectEdits",
            feature: "SetLayer via agent.Apply",
            codeLocations: locations,
            input: $"filter by smoke_key=smoke_value, target layer={tempLayer}",
            expected: "UpdatedObjectCount>=1; UndoDelta>0.",
            suspects: new[] { "LiveRhinoObjectEditOperationApplier SetLayer branch" },
            body: cp =>
            {
                RequireAnyCreated(state.CreatedLineIds, "No lines were created — cannot probe SetLayer.");
                uint before = CaptureUndoSerial();
                var resp = _editingAgent.Apply(new ApplyObjectEditsRequest
                {
                    FilePath = sourceFilePath,
                    ConfirmedLayerFullPaths = new List<string> { state.GeomLayer },
                    UserAttributeConditions = new List<UserAttributeConditionRequest>
                    {
                        new() { Key = "smoke_key", ExpectedValue = "smoke_value" }
                    },
                    Operations = new List<ObjectEditOperationRequest>
                    {
                        new() { OperationType = ObjectEditOperationType.SetLayer, TargetLayerFullPath = tempLayer }
                    }
                });
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                RequireUndoDeltaPositive(cp);
                cp.Evidence = $"UpdatedObjectCount={resp.Data!.UpdatedObjectCount}.";
            });

        report.Run(
            stage: "ObjectEdits",
            feature: "SetDisplayColor via agent.Apply",
            codeLocations: locations,
            input: "color=(200,50,50)",
            expected: "UpdatedObjectCount>=1; UndoDelta>0.",
            suspects: new[] { "LiveRhinoObjectEditOperationApplier SetDisplayColor branch" },
            body: cp =>
            {
                uint before = CaptureUndoSerial();
                var resp = _editingAgent.Apply(new ApplyObjectEditsRequest
                {
                    FilePath = sourceFilePath,
                    ConfirmedLayerFullPaths = new List<string> { tempLayer },
                    Operations = new List<ObjectEditOperationRequest>
                    {
                        new()
                        {
                            OperationType = ObjectEditOperationType.SetDisplayColor,
                            Color = new ObjectColorRequest { R = 200, G = 50, B = 50 }
                        }
                    }
                });
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                RequireUndoDeltaPositive(cp);
                cp.Evidence = $"UpdatedObjectCount={resp.Data!.UpdatedObjectCount}.";
            });

        report.Run(
            stage: "ObjectEdits",
            feature: "RemoveUserText via agent.Apply",
            codeLocations: locations,
            input: "key=smoke_key",
            expected: "UpdatedObjectCount>=1; UndoDelta>0.",
            suspects: new[] { "LiveRhinoObjectEditOperationApplier RemoveUserText branch" },
            body: cp =>
            {
                uint before = CaptureUndoSerial();
                var resp = _editingAgent.Apply(new ApplyObjectEditsRequest
                {
                    FilePath = sourceFilePath,
                    ConfirmedLayerFullPaths = new List<string> { tempLayer },
                    Operations = new List<ObjectEditOperationRequest>
                    {
                        new() { OperationType = ObjectEditOperationType.RemoveUserText, Key = "smoke_key" }
                    }
                });
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                RequireUndoDeltaPositive(cp);
                cp.Evidence = $"UpdatedObjectCount={resp.Data!.UpdatedObjectCount}.";
            });
    }

    // ----- Stage 5: Object user text --------------------------------------

    private void RunLiveObjectUserTextStage(string sourceFilePath, LiveSmokeState state, LiveSmokeReport report)
    {
        string[] locations =
        {
            "src/MCP_Rhino.Server/Tools/Editing/{Preview,Apply,Get,Delete}ObjectUserText*.cs",
            "src/MCP_Rhino.Server/Application/Services/RhinoObjectUserTextService.cs",
            "src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoObjectEditOperationApplier.cs"
        };

        var previewTool = new PreviewObjectUserTextWritesTool(_userTextService);
        var applyTool = new ApplyObjectUserTextWritesTool(_userTextService);
        var getTool = new GetObjectUserStringsTool(_userTextService);
        var deleteTool = new DeleteObjectUserTextTool(_userTextService);

        report.Run(
            stage: "ObjectUserText",
            feature: "PreviewObjectUserTextWrites",
            codeLocations: locations,
            input: "target=first created point, key=ut_smoke, value=v1",
            expected: "Preview success; no mutation.",
            suspects: new[] { "RhinoObjectUserTextService.Preview" },
            body: cp =>
            {
                Guid target = RequireCreatedTarget(state.CreatedPointIds, 0, "No point was created — cannot preview user text.");
                uint before = CaptureUndoSerial();
                var resp = previewTool.PreviewObjectUserTextWrites(
                    sourceFilePath,
                    new List<ObjectScopedUserTextEntryRequest>
                    {
                        new() { ObjectId = target, Key = "ut_smoke", Value = "v1" }
                    });
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                cp.Evidence = $"MatchedObjectCount={resp.Data!.MatchedObjectCount}.";
            });

        report.Run(
            stage: "ObjectUserText",
            feature: "ApplyObjectUserTextWrites",
            codeLocations: locations,
            input: "target=first created point, key=ut_smoke, value=v1",
            expected: "UpdatedObjectCount>=1; UndoDelta>0.",
            suspects: new[] { "RhinoObjectUserTextService.Apply live path" },
            body: cp =>
            {
                Guid target = RequireCreatedTarget(state.CreatedPointIds, 0, "No point was created — cannot apply user text.");
                uint before = CaptureUndoSerial();
                var resp = applyTool.ApplyObjectUserTextWrites(
                    sourceFilePath,
                    new List<ObjectScopedUserTextEntryRequest>
                    {
                        new() { ObjectId = target, Key = "ut_smoke", Value = "v1" }
                    });
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                RequireUndoDeltaPositive(cp);
                cp.Evidence = $"UpdatedObjectCount={resp.Data!.UpdatedObjectCount}.";
            });

        report.Run(
            stage: "ObjectUserText",
            feature: "GetObjectUserStrings",
            codeLocations: locations,
            input: "target=first created point",
            expected: "Success; entry ut_smoke=v1 visible.",
            suspects: new[] { "RhinoObjectUserTextService.Read live path" },
            body: cp =>
            {
                Guid target = RequireCreatedTarget(state.CreatedPointIds, 0, "No point was created — cannot read user text.");
                uint before = CaptureUndoSerial();
                var resp = getTool.GetObjectUserStrings(sourceFilePath, new List<Guid> { target });
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                bool found = resp.Data!.Records.Any(r => r.Entries.Any(e => e.Key == "ut_smoke" && e.Value == "v1"));
                cp.ObservedDataSummary = $"Records={resp.Data.Records.Count}, foundProbe={found}";
                if (!found)
                {
                    throw new InvalidOperationException("Expected ut_smoke=v1 in read result.");
                }
                cp.Evidence = "Wrote user text is readable.";
            });

        report.Run(
            stage: "ObjectUserText",
            feature: "DeleteObjectUserText",
            codeLocations: locations,
            input: "target=first created point, key=ut_smoke",
            expected: "UpdatedObjectCount>=1; UndoDelta>0.",
            suspects: new[] { "RhinoObjectUserTextService.Delete live path" },
            body: cp =>
            {
                Guid target = RequireCreatedTarget(state.CreatedPointIds, 0, "No point was created — cannot delete user text.");
                uint before = CaptureUndoSerial();
                var resp = deleteTool.DeleteObjectUserText(
                    sourceFilePath,
                    new List<ObjectScopedUserTextKeyRequest>
                    {
                        new() { ObjectId = target, Key = "ut_smoke" }
                    });
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                RequireUndoDeltaPositive(cp);
                cp.Evidence = $"UpdatedObjectCount={resp.Data!.UpdatedObjectCount}.";
            });
    }

    // ----- Stage 6: Document user strings ---------------------------------

    private void RunLiveDocumentUserStringStage(string sourceFilePath, LiveSmokeState state, LiveSmokeReport report)
    {
        _ = state; // stage is self-contained; no cross-stage state.

        string[] locations =
        {
            "src/MCP_Rhino.Server/Tools/File/{Set,Get,Delete}DocumentUserStringsTool.cs",
            "src/MCP_Rhino.Server/Application/Services/RhinoDocumentUserStringService.cs",
            "src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoDocumentAccessor.cs"
        };

        var setTool = new SetDocumentUserStringsTool(_documentUserStringService);
        var getTool = new GetDocumentUserStringsTool(_documentUserStringService);
        var deleteTool = new DeleteDocumentUserStringsTool(_documentUserStringService);

        const string probeKey = "__CodexLiveSmokeDocKey";
        const string probeValue = "live-smoke-value";

        report.Run(
            stage: "DocumentUserString",
            feature: "SetDocumentUserStrings",
            codeLocations: locations,
            input: $"key={probeKey}, value={probeValue}",
            expected: "SucceededCount>=1; UndoDelta>0.",
            suspects: new[] { "RhinoDocumentUserStringService.Set live path" },
            body: cp =>
            {
                uint before = CaptureUndoSerial();
                var resp = setTool.SetDocumentUserStrings(
                    sourceFilePath,
                    new List<DocumentUserStringEntryRequest>
                    {
                        new() { Key = probeKey, Value = probeValue }
                    });
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                RequireUndoDeltaPositive(cp);
                cp.Evidence = $"SucceededCount={resp.Data!.SucceededCount}.";
            });

        report.Run(
            stage: "DocumentUserString",
            feature: "GetDocumentUserStrings",
            codeLocations: locations,
            input: $"expect key={probeKey}",
            expected: $"Entry {probeKey}={probeValue} visible.",
            suspects: new[] { "RhinoDocumentUserStringService.Read live path" },
            body: cp =>
            {
                uint before = CaptureUndoSerial();
                var resp = getTool.GetDocumentUserStrings(sourceFilePath);
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                bool found = resp.Data!.Entries.Any(e => e.Key == probeKey && e.Value == probeValue);
                cp.ObservedDataSummary = $"Entries={resp.Data.Entries.Count}, foundProbe={found}";
                if (!found)
                {
                    throw new InvalidOperationException($"Expected {probeKey}={probeValue} in read result.");
                }
                cp.Evidence = "Written doc user string is readable.";
            });

        report.Run(
            stage: "DocumentUserString",
            feature: "DeleteDocumentUserStrings",
            codeLocations: locations,
            input: $"key={probeKey}",
            expected: "SucceededCount>=1; UndoDelta>0.",
            suspects: new[] { "RhinoDocumentUserStringService.Delete live path" },
            body: cp =>
            {
                uint before = CaptureUndoSerial();
                var resp = deleteTool.DeleteDocumentUserStrings(
                    sourceFilePath,
                    new List<DocumentUserStringEntryRequest>
                    {
                        new() { Key = probeKey }
                    });
                cp.UndoDelta = (int)(CaptureUndoSerial() - before);
                cp.RecordResponse(resp);
                RequireLiveSuccess(resp, cp);
                RequireUndoDeltaPositive(cp);
                cp.Evidence = $"SucceededCount={resp.Data!.SucceededCount}.";
            });
    }

    // ----- Cleanup + helpers ----------------------------------------------

    private void TryCleanupSmokeRoot(string sourceFilePath, string smokeRoot)
    {
        try
        {
            var tool = new PurgeLayersTool(_layerManagementService);
            var resp = tool.PurgeLayers(sourceFilePath, new List<string> { smokeRoot });
            if (!resp.Success)
            {
                RhinoApp.WriteLine($"[LiveDocumentSmoke] Cleanup purge of '{smokeRoot}' failed: {resp.Message}");
            }
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"[LiveDocumentSmoke] Cleanup purge threw: {ex.Message}");
        }
    }

    private static void RequireLiveSuccess<T>(OperationResponse<T> response, LiveSmokeCheckpoint cp)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"Expected Success=true with Data, got Success={response.Success}, Message={response.Message}.");
        }
        cp.ObservedDataSummary ??= "(success)";
    }

    private static void RequireUndoDeltaPositive(LiveSmokeCheckpoint cp)
    {
        if (cp.UndoDelta <= 0)
        {
            throw new InvalidOperationException(
                $"Expected UndoDelta > 0 for mutation checkpoint; got {cp.UndoDelta}. Live mutation likely did not run.");
        }
    }

    // Pulls the Nth created object ID from the given list or SKIPs the checkpoint
    // if the upstream creation stage did not produce enough objects. Call this from
    // inside a report.Run body — the LiveSmokeSkipException it throws is caught by
    // report.Run and recorded as SKIP for that single checkpoint.
    private static Guid RequireCreatedTarget(List<Guid> ids, int index, string reason)
    {
        if (ids.Count <= index)
        {
            throw new LiveSmokeSkipException($"{reason} (created count={ids.Count}, wanted index {index}).");
        }
        Guid value = ids[index];
        if (value == Guid.Empty)
        {
            throw new LiveSmokeSkipException($"{reason} (recorded id at index {index} is Guid.Empty — the create response did not carry an ObjectId).");
        }
        return value;
    }

    // Ensures at least one ID is present in the list, otherwise marks the
    // checkpoint SKIP. Use when the checkpoint acts on any available target
    // (e.g. SetUserText matched via layer filter).
    private static void RequireAnyCreated(List<Guid> ids, string reason)
    {
        if (ids.Count == 0)
        {
            throw new LiveSmokeSkipException(reason);
        }
    }

    private static string SafeFindFallbackLayer()
    {
        try
        {
            return FindFirstNonSmokeLayerFullPath();
        }
        catch (Exception)
        {
            return "Default";
        }
    }

    private static void RecordCreatedIds(LiveSmokeState state, GeometryCreationResponse response)
    {
        foreach (GeometryCreatedObjectResponse created in response.CreatedObjects)
        {
            switch (created.Primitive)
            {
                case GeometryPrimitiveKind.Point:
                    state.CreatedPointIds.Add(created.ObjectId);
                    break;
                case GeometryPrimitiveKind.Line:
                    state.CreatedLineIds.Add(created.ObjectId);
                    break;
                case GeometryPrimitiveKind.Arc:
                    state.CreatedArcIds.Add(created.ObjectId);
                    break;
                case GeometryPrimitiveKind.Surface:
                    state.CreatedSurfaceIds.Add(created.ObjectId);
                    break;
            }
        }
    }

    private sealed class LiveSmokeState
    {
        public required string SmokeRoot { get; init; }
        public required string GeomLayer { get; init; }
        public string DeleteLayer { get; set; } = string.Empty;
        public required string PurgeLayer { get; init; }
        public required string PurgeGrandchild { get; init; }
        public required string FallbackLayerFullPath { get; init; }
        public List<Guid> CreatedPointIds { get; } = new();
        public List<Guid> CreatedLineIds { get; } = new();
        public List<Guid> CreatedArcIds { get; } = new();
        public List<Guid> CreatedSurfaceIds { get; } = new();
    }
}
