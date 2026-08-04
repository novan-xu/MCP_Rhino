using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Infrastructure.Plugin;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string DocumentVisualStateToolsSlug = "document-visual-state-tools-smoke-test";

    partial void RegisterDocumentVisualStateToolsHandlers()
    {
        _extensionHandlers[DocumentVisualStateToolsSlug] = HandleDocumentVisualStateToolsSmokeTest;
    }

    private bool HandleDocumentVisualStateToolsSmokeTest(string[] args)
    {
        try
        {
            if (McpRhinoPlugin.Instance is null)
            {
                RunDocumentVisualStateCliFallbackSmoke();
            }
            else
            {
                string filePath = args.Length > 1 ? args[1] : string.Empty;
                RunDocumentVisualStateLiveSmoke(filePath);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Document visual state tools smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunDocumentVisualStateCliFallbackSmoke()
    {
        string filePath = "C:/mcp-rhino/document-visual-state-smoke.3dm";

        RequireDocumentVisualStateLiveRequired(_documentStateService.GetSummary(new GetDocumentSummaryRequest
        {
            FilePath = filePath
        }), "GetDocumentSummary");

        RequireDocumentVisualStateLiveRequired(_selectionService.GetSelectedObjects(new GetSelectedObjectsInLiveRequest
        {
            FilePath = filePath
        }), "GetSelectedObjectsInLive");

        RequireDocumentVisualStateLiveRequired(_selectionService.SelectObjects(new SelectObjectsInLiveRequest
        {
            FilePath = filePath,
            SelectionMode = RhinoSelectionMode.Clear
        }), "SelectObjectsInLive");

        RequireDocumentVisualStateLiveRequired(_documentStateService.GetCurrentLayer(new GetCurrentLayerInLiveRequest
        {
            FilePath = filePath
        }), "GetCurrentLayerInLive");

        RequireDocumentVisualStateLiveRequired(_documentStateService.SetCurrentLayer(new SetCurrentLayerInLiveRequest
        {
            FilePath = filePath,
            FullPath = "Default"
        }), "SetCurrentLayerInLive");

        RequireDocumentVisualStateLiveRequired(_viewportCaptureService.Capture(new CaptureViewportImageRequest
        {
            FilePath = filePath,
            ImageSizePx = new ImageSizePxRequest { Width = 64, Height = 64 }
        }), "CaptureViewportImage");

        Console.WriteLine("[OK] document-visual-state-tools CLI fallback returned LIVE_RHINO_REQUIRED for live-only paths.");
    }

    private void RunDocumentVisualStateLiveSmoke(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("Live document visual state smoke requires a saved active document path.");
        }

        DocumentSummaryResponse beforeSummary = RequireDocumentVisualStateSuccess(_documentStateService.GetSummary(new GetDocumentSummaryRequest
        {
            FilePath = filePath,
            MaxObjectSummaries = 5,
            MaxLayerSummaries = 20,
            MaxNamedViews = 10,
            MaxMaterials = 10
        }), "GetDocumentSummary");

        CurrentLayerResponse originalLayer = RequireDocumentVisualStateSuccess(_documentStateService.GetCurrentLayer(new GetCurrentLayerInLiveRequest
        {
            FilePath = filePath
        }), "GetCurrentLayerInLive");

        string suffix = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        string smokeLayer = $"MCP::DOCUMENT_VISUAL_STATE_SMOKE_{suffix}";
        List<Guid> createdObjectIds = new();

        try
        {
            RequireDocumentVisualStateSuccess(_layerManagementService.Create(new CreateLayersRequest
            {
                FilePath = filePath,
                Entries = new List<LayerCreationEntryRequest>
                {
                    new() { FullPath = smokeLayer }
                }
            }), "Create smoke layer");

            CurrentLayerMutationResponse setLayer = RequireDocumentVisualStateSuccess(_documentStateService.SetCurrentLayer(new SetCurrentLayerInLiveRequest
            {
                FilePath = filePath,
                FullPath = smokeLayer
            }), "SetCurrentLayerInLive");

            if (setLayer.CurrentLayer is null || !string.Equals(setLayer.CurrentLayer.FullPath, smokeLayer, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("SetCurrentLayerInLive did not set the smoke layer.");
            }

            GeometryCreationResponse created = RequireDocumentVisualStateSuccess(_geometryCreationSkill.Create(new CreatePointsRequest
            {
                FilePath = filePath,
                Items = new List<PointItemRequest>
                {
                    new() { X = 0, Y = 0, Z = 0 },
                    new() { X = 1, Y = 0, Z = 0 }
                },
                Common = new GeometryCreationCommonOptions { LayerFullPath = smokeLayer, Name = "document visual state smoke point" }
            }), "Create smoke points");

            createdObjectIds = created.CreatedObjects.Select(item => item.ObjectId).ToList();
            if (createdObjectIds.Count < 2)
            {
                throw new InvalidOperationException($"Expected at least 2 created smoke points, got {createdObjectIds.Count}.");
            }

            SelectionMutationResponse selected = RequireDocumentVisualStateSuccess(_selectionService.SelectObjects(new SelectObjectsInLiveRequest
            {
                FilePath = filePath,
                ObjectIds = createdObjectIds,
                SelectionMode = RhinoSelectionMode.Replace
            }), "SelectObjectsInLive replace");

            if (selected.CurrentSelectedCount < createdObjectIds.Count)
            {
                throw new InvalidOperationException($"Expected selected count >= {createdObjectIds.Count}, got {selected.CurrentSelectedCount}.");
            }

            SelectedObjectsResponse readSelection = RequireDocumentVisualStateSuccess(_selectionService.GetSelectedObjects(new GetSelectedObjectsInLiveRequest
            {
                FilePath = filePath
            }), "GetSelectedObjectsInLive");

            if (readSelection.Objects.Count < createdObjectIds.Count)
            {
                throw new InvalidOperationException($"Selection read returned too few objects: {readSelection.Objects.Count}.");
            }

            DocumentSummaryResponse afterSummary = RequireDocumentVisualStateSuccess(_documentStateService.GetSummary(new GetDocumentSummaryRequest
            {
                FilePath = filePath,
                MaxObjectSummaries = 5
            }), "GetDocumentSummary after selection");

            if (afterSummary.ObjectCount < beforeSummary.ObjectCount + createdObjectIds.Count)
            {
                throw new InvalidOperationException("Document summary object count did not reflect created smoke objects.");
            }

            ViewportCaptureResponse capture = RequireDocumentVisualStateSuccess(_viewportCaptureService.Capture(new CaptureViewportImageRequest
            {
                FilePath = filePath,
                ImageSizePx = new ImageSizePxRequest { Width = 320, Height = 200 }
            }), "CaptureViewportImage");

            if (capture.Width != 320 || capture.Height != 200 || capture.ByteCount <= 0 || string.IsNullOrWhiteSpace(capture.DataBase64))
            {
                throw new InvalidOperationException("Viewport capture did not return a non-empty 320x200 base64 PNG payload.");
            }

            Console.WriteLine("[OK] document-visual-state-tools live smoke completed.");
            Console.WriteLine($"[OK] Summary objects before={beforeSummary.ObjectCount}, after={afterSummary.ObjectCount}; selected={readSelection.SelectedCount}; captureBytes={capture.ByteCount}.");
        }
        finally
        {
            _selectionService.SelectObjects(new SelectObjectsInLiveRequest
            {
                FilePath = filePath,
                SelectionMode = RhinoSelectionMode.Clear
            });

            _documentStateService.SetCurrentLayer(new SetCurrentLayerInLiveRequest
            {
                FilePath = filePath,
                LayerId = originalLayer.LayerId
            });
        }
    }

    private static void RequireDocumentVisualStateLiveRequired<T>(OperationResponse<T> response, string label)
    {
        if (response.Success || !string.Equals(response.Message, "LIVE_RHINO_REQUIRED", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{label} should return LIVE_RHINO_REQUIRED in CLI fallback mode; got Success={response.Success}, Message={response.Message}");
        }
    }

    private static T RequireDocumentVisualStateSuccess<T>(OperationResponse<T> response, string label)
        where T : class
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{label} failed: {response.Message}");
        }

        return response.Data;
    }
}
