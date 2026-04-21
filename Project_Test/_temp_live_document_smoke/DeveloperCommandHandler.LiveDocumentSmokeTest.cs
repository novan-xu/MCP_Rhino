extern alias rhinocommon;

using System.Globalization;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Tools.Editing;
using MCP_Rhino.Server.Tools.File;
using MCP_Rhino.Server.Tools.Geometry;
using MCP_Rhino.Server.Tools.Layers;
using Rhino.FileIO;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Infrastructure.CLI;

// This partial-class file is the entry point for the temp live-document smoke
// unit. It registers the CLI command `live-document-smoke-test` via the
// partial-method extension hook in the main DeveloperCommandHandler.cs, and
// delegates the actual stages to DeveloperCommandHandler.LiveDocumentSmokeTest.LiveStages.cs.
public sealed partial class DeveloperCommandHandler
{
    private const string LiveSmokeSlug = "live-document-smoke";
    private const string LiveSmokeRootPrefix = "__CodexLiveSmoke_";

    // Wired from the main DeveloperCommandHandler.cs partial-method declaration.
    // Deleting this file also deletes this implementation, which makes the
    // partial-method call a no-op and leaves _extensionHandlers empty.
    partial void RegisterExtensionHandlers()
    {
        _extensionHandlers["live-document-smoke-test"] = HandleLiveDocumentSmokeTest;
    }

    private bool HandleLiveDocumentSmokeTest(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: dotnet run --project src/MCP_Rhino.Server -- live-document-smoke-test <3dm-file-path>");
            System.Environment.ExitCode = 1;
            return true;
        }

        string sourceFilePath;
        try
        {
            sourceFilePath = Path.GetFullPath(args[1]);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Invalid file path argument: {ex.Message}");
            System.Environment.ExitCode = 1;
            return true;
        }

        if (!File.Exists(sourceFilePath))
        {
            Console.Error.WriteLine($"Live-document smoke source file not found: {sourceFilePath}");
            System.Environment.ExitCode = 1;
            return true;
        }

        bool pluginMode = MCP_Rhino.Server.Infrastructure.Plugin.McpRhinoPlugin.Instance is not null;
        string reportDirectory = ResolveValidationDirectory(LiveSmokeSlug);
        var report = new LiveSmokeReport
        {
            Mode = pluginMode ? "Live" : "CliFallback",
            DocumentPath = sourceFilePath,
            RhinoVersion = pluginMode ? SafeGetRhinoVersion() : "n/a (CLI fallback)"
        };

        try
        {
            if (pluginMode)
            {
                RunLiveDocumentSmoke(sourceFilePath, report);
            }
            else
            {
                RunCliFallbackLiveDocumentSmoke(sourceFilePath, report);
            }
        }
        catch (Exception outerEx)
        {
            // Per-checkpoint bodies trap their own exceptions into the report.
            // If we land here, the orchestration layer itself blew up
            // (fixture-load / state-capture / cleanup). Emit a synthetic
            // failure checkpoint so the report still records the problem.
            Console.Error.WriteLine($"Live-document smoke orchestration failed: {outerEx}");
            report.Run(
                stage: "Orchestration",
                feature: "OuterOrchestration",
                codeLocations: new[]
                {
                    "Project_Test/_temp_live_document_smoke/DeveloperCommandHandler.LiveDocumentSmokeTest.cs"
                },
                input: $"filePath={sourceFilePath}; mode={report.Mode}",
                expected: "Orchestration should complete without uncaught exceptions.",
                suspects: new[]
                {
                    "File3dm.Read on fixture path",
                    "RhinoDoc.ActiveDoc resolution",
                    "Smoke-root cleanup PurgeLayers"
                },
                body: _ => throw new InvalidOperationException(outerEx.ToString()));
        }

        string reportPath;
        try
        {
            reportPath = report.WriteMarkdown(reportDirectory);
        }
        catch (Exception reportEx)
        {
            Console.Error.WriteLine($"Failed to write live-document smoke report: {reportEx}");
            System.Environment.ExitCode = 1;
            return true;
        }

        Console.WriteLine($"Live-document smoke report: {reportPath}");
        Console.WriteLine($"Mode: {report.Mode} | Totals: {report.PassCount} PASS / {report.FailCount} FAIL / {report.SkipCount} SKIP");
        if (report.HasFailures)
        {
            System.Environment.ExitCode = 1;
        }

        return true;
    }

    // ----- CLI fallback (no Rhino running) ---------------------------------

    private void RunCliFallbackLiveDocumentSmoke(string sourceFilePath, LiveSmokeReport report)
    {
        string validationDirectory = ResolveValidationDirectory(LiveSmokeSlug);
        string workingFilePath = Path.Combine(validationDirectory, "MCP_METtest.live-document-smoke.3dm");
        File.Copy(sourceFilePath, workingFilePath, overwrite: true);

        (int objects, int layers, string firstLayer, Guid? firstObject, uint undoSerial) =
            CaptureFixtureStateFromFile(workingFilePath);
        report.InitialObjectCount = objects;
        report.InitialLayerCount = layers;
        report.InitialUndoSerial = undoSerial;

        // Mutation tools (should all reject with LIVE_RHINO_REQUIRED).
        RunCliFallbackMutationChecks(workingFilePath, firstLayer, firstObject, report);

        // Read tools (must succeed without emitting stale warnings in CLI mode).
        RunCliFallbackReadChecks(workingFilePath, firstObject, report);

        (int finalObjects, int finalLayers, _, _, uint finalUndoSerial) =
            CaptureFixtureStateFromFile(workingFilePath);
        report.FinalObjectCount = finalObjects;
        report.FinalLayerCount = finalLayers;
        report.FinalUndoSerial = finalUndoSerial;

        report.Run(
            stage: "Orchestration",
            feature: "CliFallback/FixtureUnchanged",
            codeLocations: new[]
            {
                "Project_Test/_temp_live_document_smoke/DeveloperCommandHandler.LiveDocumentSmokeTest.cs"
            },
            input: $"before: objects={objects}, layers={layers}",
            expected: "Working copy object/layer counts must be unchanged in CLI fallback mode.",
            suspects: new[]
            {
                "Any tool incorrectly falling back to File3dm.Write in CLI mode"
            },
            body: cp =>
            {
                cp.ObservedDataSummary = $"after: objects={finalObjects}, layers={finalLayers}";
                if (finalObjects != objects || finalLayers != layers)
                {
                    throw new InvalidOperationException(
                        $"CLI fallback must be read-only. object_delta={finalObjects - objects}, layer_delta={finalLayers - layers}.");
                }
                cp.Evidence = "Working copy unchanged.";
            });
    }

    private void RunCliFallbackMutationChecks(
        string workingFilePath,
        string firstLayerPath,
        Guid? firstObjectId,
        LiveSmokeReport report)
    {
        string[] layerLocations =
        {
            "src/MCP_Rhino.Server/Tools/Layers/CreateLayersTool.cs",
            "src/MCP_Rhino.Server/Application/Services/RhinoLayerManagementService.cs",
            "src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoDocumentAccessor.cs"
        };
        string[] geomCreateLocations =
        {
            "src/MCP_Rhino.Server/Tools/Geometry/CreatePointsTool.cs",
            "src/MCP_Rhino.Server/Application/Services/RhinoGeometryCreationService.cs",
            "src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoGeometryBuilder.cs"
        };
        string[] geomModifyLocations =
        {
            "src/MCP_Rhino.Server/Tools/Geometry/TransformObjectsTool.cs",
            "src/MCP_Rhino.Server/Application/Services/RhinoGeometryModificationService.cs",
            "src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoGeometryMutator.cs"
        };
        string[] objectEditLocations =
        {
            "src/MCP_Rhino.Server/Tools/Editing/ApplyObjectEditsTool.cs",
            "src/MCP_Rhino.Server/Skills/Editing/ObjectEditApplySkill.cs",
            "src/MCP_Rhino.Server/Application/Services/RhinoObjectEditingService.cs",
            "src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoObjectEditOperationApplier.cs"
        };
        string[] userTextLocations =
        {
            "src/MCP_Rhino.Server/Tools/Editing/ApplyObjectUserTextWritesTool.cs",
            "src/MCP_Rhino.Server/Application/Services/RhinoObjectUserTextService.cs",
            "src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoObjectEditOperationApplier.cs"
        };
        string[] docUserStringLocations =
        {
            "src/MCP_Rhino.Server/Tools/File/SetDocumentUserStringsTool.cs",
            "src/MCP_Rhino.Server/Application/Services/RhinoDocumentUserStringService.cs",
            "src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoDocumentAccessor.cs"
        };

        string[] genericSuspects =
        {
            "LiveRhinoDocumentAccessor.Execute / ExecuteWithUndo should reject when no RhinoDoc.ActiveDoc is available.",
            "DI wiring in DependencyInjection.AddCliFallbackLiveRhinoAdapters"
        };

        var createLayersTool = new CreateLayersTool(_layerManagementService);
        report.Run(
            stage: "CliFallback",
            feature: "CreateLayers rejected",
            codeLocations: layerLocations,
            input: $"FullPath={LiveSmokeRootPrefix}cli__::Leaf",
            expected: "response.Success=false and Message=LIVE_RHINO_REQUIRED",
            suspects: genericSuspects,
            body: cp =>
            {
                var resp = createLayersTool.CreateLayers(
                    workingFilePath,
                    new List<LayerCreationEntryRequest>
                    {
                        new() { FullPath = $"{LiveSmokeRootPrefix}cli__::Leaf" }
                    });
                cp.RecordResponse(resp);
                RequireExpectedRejection(resp, "LIVE_RHINO_REQUIRED", cp);
                cp.Evidence = "Rejected as expected in CLI fallback.";
            });

        var createPointsTool = new CreatePointsTool(_geometryCreationSkill);
        report.Run(
            stage: "CliFallback",
            feature: "CreatePoints rejected",
            codeLocations: geomCreateLocations,
            input: $"layer={firstLayerPath}, point=(1,2,3)",
            expected: "LIVE_RHINO_REQUIRED",
            suspects: genericSuspects,
            body: cp =>
            {
                var resp = createPointsTool.CreatePoints(
                    workingFilePath,
                    new List<PointItemRequest> { new() { X = 1, Y = 2, Z = 3 } },
                    new GeometryCreationCommonOptions { LayerFullPath = firstLayerPath });
                cp.RecordResponse(resp);
                RequireExpectedRejection(resp, "LIVE_RHINO_REQUIRED", cp);
                cp.Evidence = "Rejected as expected in CLI fallback.";
            });

        var transformObjectsTool = new TransformObjectsTool(_geometryModificationSkill);
        report.Run(
            stage: "CliFallback",
            feature: "TransformObjects rejected",
            codeLocations: geomModifyLocations,
            input: "Translate vx=1, confirmedObjectIds=[newGuid]",
            expected: "LIVE_RHINO_REQUIRED",
            suspects: genericSuspects,
            body: cp =>
            {
                var resp = transformObjectsTool.TransformObjects(
                    workingFilePath,
                    new MCP_Rhino.Server.Domain.Models.GeometryTransformSpec
                    {
                        Kind = MCP_Rhino.Server.Domain.Enums.GeometryTransformKind.Translate,
                        VectorX = 1, VectorY = 0, VectorZ = 0
                    },
                    confirmedObjectIds: new List<Guid> { firstObjectId ?? Guid.NewGuid() });
                cp.RecordResponse(resp);
                RequireExpectedRejection(resp, "LIVE_RHINO_REQUIRED", cp);
                cp.Evidence = "Rejected as expected in CLI fallback.";
            });

        report.Run(
            stage: "CliFallback",
            feature: "ApplyObjectEdits rejected",
            codeLocations: objectEditLocations,
            input: "SetUserText smoke=cli, confirmedLayerPath=<first>",
            expected: "LIVE_RHINO_REQUIRED",
            suspects: genericSuspects,
            body: cp =>
            {
                var resp = _editingAgent.Apply(new ApplyObjectEditsRequest
                {
                    FilePath = workingFilePath,
                    ConfirmedLayerFullPaths = new List<string> { firstLayerPath },
                    Operations = new List<ObjectEditOperationRequest>
                    {
                        new()
                        {
                            OperationType = MCP_Rhino.Server.Domain.Enums.ObjectEditOperationType.SetUserText,
                            Key = "cli_smoke_probe",
                            Value = "should_be_rejected"
                        }
                    }
                });
                cp.RecordResponse(resp);
                RequireExpectedRejection(resp, "LIVE_RHINO_REQUIRED", cp);
                cp.Evidence = "Rejected as expected in CLI fallback.";
            });

        var applyUserTextTool = new ApplyObjectUserTextWritesTool(_userTextService);
        report.Run(
            stage: "CliFallback",
            feature: "ApplyObjectUserTextWrites rejected",
            codeLocations: userTextLocations,
            input: $"objectId={firstObjectId ?? Guid.Empty}, key=cli_smoke, value=rejected",
            expected: "LIVE_RHINO_REQUIRED",
            suspects: genericSuspects,
            body: cp =>
            {
                var resp = applyUserTextTool.ApplyObjectUserTextWrites(
                    workingFilePath,
                    new List<ObjectScopedUserTextEntryRequest>
                    {
                        new()
                        {
                            ObjectId = firstObjectId ?? Guid.NewGuid(),
                            Key = "cli_smoke_user_text",
                            Value = "rejected"
                        }
                    });
                cp.RecordResponse(resp);
                RequireExpectedRejection(resp, "LIVE_RHINO_REQUIRED", cp);
                cp.Evidence = "Rejected as expected in CLI fallback.";
            });

        var setDocUserStringsTool = new SetDocumentUserStringsTool(_documentUserStringService);
        report.Run(
            stage: "CliFallback",
            feature: "SetDocumentUserStrings rejected",
            codeLocations: docUserStringLocations,
            input: "key=cli_smoke_doc_string, value=rejected",
            expected: "LIVE_RHINO_REQUIRED",
            suspects: genericSuspects,
            body: cp =>
            {
                var resp = setDocUserStringsTool.SetDocumentUserStrings(
                    workingFilePath,
                    new List<DocumentUserStringEntryRequest>
                    {
                        new() { Key = "cli_smoke_doc_string", Value = "rejected" }
                    });
                cp.RecordResponse(resp);
                RequireExpectedRejection(resp, "LIVE_RHINO_REQUIRED", cp);
                cp.Evidence = "Rejected as expected in CLI fallback.";
            });
    }

    private void RunCliFallbackReadChecks(
        string workingFilePath,
        Guid? firstObjectId,
        LiveSmokeReport report)
    {
        var getLayersTool = new GetLayersTool(_layerManagementService);
        report.Run(
            stage: "CliFallback",
            feature: "GetLayers offline read",
            codeLocations: new[]
            {
                "src/MCP_Rhino.Server/Tools/Layers/GetLayersTool.cs",
                "src/MCP_Rhino.Server/Application/Services/RhinoLayerManagementService.cs",
                "src/MCP_Rhino.Server/Infrastructure/Rhino/Offline/"
            },
            input: $"filePath={workingFilePath}",
            expected: "Success=true, Warnings empty (no live Rhino → no stale warning).",
            suspects: new[]
            {
                "RhinoLayerManagementService.Get offline path",
                "TryGetActiveDocumentState returning false should not attach stale warning."
            },
            body: cp =>
            {
                var resp = getLayersTool.GetLayers(workingFilePath);
                cp.RecordResponse(resp);
                if (!resp.Success || resp.Data is null)
                {
                    throw new InvalidOperationException($"GetLayers failed: {resp.Message}");
                }
                cp.ObservedDataSummary = $"TotalCount={resp.Data.TotalCount}, Warnings={resp.Data.Warnings.Count}";
                if (resp.Data.Warnings.Count != 0)
                {
                    throw new InvalidOperationException($"Expected no warnings in CLI mode, got {resp.Data.Warnings.Count}.");
                }
                cp.Evidence = $"TotalCount={resp.Data.TotalCount}, no stale warnings.";
            });

        var getDocumentUserStringsTool = new GetDocumentUserStringsTool(_documentUserStringService);
        report.Run(
            stage: "CliFallback",
            feature: "GetDocumentUserStrings offline read",
            codeLocations: new[]
            {
                "src/MCP_Rhino.Server/Tools/File/GetDocumentUserStringsTool.cs",
                "src/MCP_Rhino.Server/Application/Services/RhinoDocumentUserStringService.cs",
                "src/MCP_Rhino.Server/Infrastructure/Rhino/Offline/"
            },
            input: $"filePath={workingFilePath}",
            expected: "Success=true, Warnings empty.",
            suspects: new[]
            {
                "RhinoDocumentUserStringService.Read offline path",
                "TryGetActiveDocumentState gating of OFFLINE_READ_STALE"
            },
            body: cp =>
            {
                var resp = getDocumentUserStringsTool.GetDocumentUserStrings(workingFilePath);
                cp.RecordResponse(resp);
                if (!resp.Success || resp.Data is null)
                {
                    throw new InvalidOperationException($"GetDocumentUserStrings failed: {resp.Message}");
                }
                cp.ObservedDataSummary = $"Entries={resp.Data.Entries.Count}, Warnings={resp.Data.Warnings.Count}";
                if (resp.Data.Warnings.Count != 0)
                {
                    throw new InvalidOperationException($"Expected no warnings in CLI mode, got {resp.Data.Warnings.Count}.");
                }
                cp.Evidence = $"Entries={resp.Data.Entries.Count}, no stale warnings.";
            });

        var getObjectUserStringsTool = new GetObjectUserStringsTool(_userTextService);
        report.Run(
            stage: "CliFallback",
            feature: "GetObjectUserStrings offline read",
            codeLocations: new[]
            {
                "src/MCP_Rhino.Server/Tools/Editing/GetObjectUserStringsTool.cs",
                "src/MCP_Rhino.Server/Application/Services/RhinoObjectUserTextService.cs",
                "src/MCP_Rhino.Server/Infrastructure/Rhino/Offline/"
            },
            input: firstObjectId is null ? "(no objects in fixture)" : $"objectIds=[{firstObjectId}]",
            expected: "Success=true (or skipped when fixture has no objects).",
            suspects: new[]
            {
                "RhinoObjectUserTextService.Read offline path"
            },
            body: cp =>
            {
                if (firstObjectId is null)
                {
                    throw new LiveSmokeSkipException("Fixture contains no objects — cannot probe GetObjectUserStrings.");
                }
                var resp = getObjectUserStringsTool.GetObjectUserStrings(
                    workingFilePath,
                    new List<Guid> { firstObjectId.Value });
                cp.RecordResponse(resp);
                if (!resp.Success || resp.Data is null)
                {
                    throw new InvalidOperationException($"GetObjectUserStrings failed: {resp.Message}");
                }
                cp.ObservedDataSummary = $"Records={resp.Data.Records.Count}";
                cp.Evidence = $"Read ok for objectId={firstObjectId.Value}.";
            });
    }

    // ----- Shared helpers --------------------------------------------------

    private static void RequireExpectedRejection<T>(OperationResponse<T> response, string expectedMessage, LiveSmokeCheckpoint checkpoint)
    {
        if (response.Success)
        {
            throw new InvalidOperationException($"Expected failure with '{expectedMessage}' but got Success=true.");
        }

        if (!string.Equals(response.Message, expectedMessage, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected failure message '{expectedMessage}' but got '{response.Message}'.");
        }

        checkpoint.ObservedDataSummary = "(rejected, no data)";
    }

    private static (int objects, int layers, string firstLayer, Guid? firstObject, uint undoSerial)
        CaptureFixtureStateFromFile(string filePath)
    {
        using File3dm? model = File3dm.Read(filePath);
        if (model is null)
        {
            throw new InvalidOperationException($"Failed to read fixture file: {filePath}");
        }

        int objects = model.Objects.Count;
        int layers = model.AllLayers.Count(layer => !layer.IsDeleted);
        global::Rhino.DocObjects.Layer? firstLayer = model.AllLayers.FirstOrDefault(layer => !layer.IsDeleted);
        if (firstLayer is null)
        {
            throw new InvalidOperationException("Fixture must contain at least one active layer.");
        }

        Guid? firstObject = null;
        foreach (File3dmObject obj in model.Objects)
        {
            firstObject = obj.Attributes.ObjectId;
            break;
        }

        return (objects, layers, firstLayer.FullPath, firstObject, 0u);
    }

    private static (int objects, int layers, uint undoSerial) CaptureLiveState()
    {
        RhinoDoc? doc = RhinoDoc.ActiveDoc;
        if (doc is null)
        {
            return (0, 0, 0u);
        }

        int layers = 0;
        for (int i = 0; i < doc.Layers.Count; i++)
        {
            rhinocommon::Rhino.DocObjects.Layer layer = doc.Layers[i];
            if (!layer.IsDeleted)
            {
                layers++;
            }
        }

        return (doc.Objects.Count, layers, doc.CurrentUndoRecordSerialNumber);
    }

    private static uint CaptureUndoSerial()
    {
        return RhinoDoc.ActiveDoc?.CurrentUndoRecordSerialNumber ?? 0u;
    }

    private static string FindFirstNonSmokeLayerFullPath()
    {
        RhinoDoc? doc = RhinoDoc.ActiveDoc;
        if (doc is null)
        {
            throw new InvalidOperationException("No active document — live smoke requires Rhino to be running.");
        }

        for (int i = 0; i < doc.Layers.Count; i++)
        {
            rhinocommon::Rhino.DocObjects.Layer layer = doc.Layers[i];
            if (!layer.IsDeleted && !layer.FullPath.StartsWith(LiveSmokeRootPrefix, StringComparison.Ordinal))
            {
                return layer.FullPath;
            }
        }

        throw new InvalidOperationException("Active document has no non-smoke layer available.");
    }

    private static string SafeGetRhinoVersion()
    {
        try
        {
            return RhinoApp.Version.ToString();
        }
        catch (Exception)
        {
            return "unknown";
        }
    }

    private static string FormatGuid(Guid g) => g.ToString("N").Substring(0, 8);

    private static string FormatCoord(double x, double y, double z) =>
        string.Format(CultureInfo.InvariantCulture, "({0:F3},{1:F3},{2:F3})", x, y, z);
}
