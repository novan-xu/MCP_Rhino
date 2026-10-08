using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed class PanelCladdingSaveService
{
    private readonly ILivePanelCladdingRepository _liveRepository;
    private readonly PanelCladdingKeyService _keys;
    private readonly PanelCladdingLogicService _claddingLogic;
    private readonly PanelCladdingLogicalCellService _logicalCells;
    private readonly PanelCladdingTopologyNormalizationService _topologyNormalizer;
    private readonly PanelCladdingRegionService _regions;
    private readonly PanelFrameAssignmentService _frameAssignments;

    public PanelCladdingSaveService(
        ILivePanelCladdingRepository liveRepository,
        PanelCladdingTypeSignatureService signatureService)
    {
        _liveRepository = liveRepository;
        _keys = new PanelCladdingKeyService();
        _claddingLogic = new PanelCladdingLogicService();
        _logicalCells = new PanelCladdingLogicalCellService();
        _topologyNormalizer = new PanelCladdingTopologyNormalizationService(_keys);
        _ = signatureService; // Retain constructor compatibility while type generation is suspended.
        _regions = new PanelCladdingRegionService(_keys);
        _frameAssignments = new PanelFrameAssignmentService();
    }

    public PanelCladdingSaveService(
        ILivePanelCladdingRepository liveRepository,
        IPanelCladdingWorkbookRepository workbookRepository,
        IPanelPreviewRenderer previewRenderer,
        PanelCladdingTypeSignatureService signatureService)
        : this(liveRepository, signatureService)
    {
        _ = workbookRepository;
        _ = previewRenderer;
    }

    public OperationResponse<PanelCladdingSaveResult> Save(PanelCladdingSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath) || request.ObjectId == Guid.Empty)
        {
            return OperationResponse<PanelCladdingSaveResult>.Fail("PANEL_CLADDING_TARGET_REQUIRED");
        }
        if (!Enum.IsDefined(request.Scope))
        {
            return OperationResponse<PanelCladdingSaveResult>.Fail("PANEL_CLADDING_SAVE_SCOPE_INVALID");
        }

        bool saveExtrusions = request.Scope is PanelCladdingSaveScope.Extrusions or PanelCladdingSaveScope.Both;
        bool saveCladding = request.Scope is PanelCladdingSaveScope.Cladding or PanelCladdingSaveScope.Both;

        OperationResponse<PanelCladdingLayout> read = _liveRepository.ReadLayout(request.FilePath, request.ObjectId);
        if (!read.Success || read.Data is null)
        {
            return OperationResponse<PanelCladdingSaveResult>.Fail(read.Message);
        }

        PanelCladdingLayout layout = read.Data;
        if (!string.Equals(layout.GeometryFingerprint, request.ExpectedGeometryFingerprint, StringComparison.Ordinal))
        {
            return OperationResponse<PanelCladdingSaveResult>.Fail(
                "PANEL_CLADDING_STALE_EDITOR: panel geometry or attributes changed; reload before saving.");
        }

        IReadOnlyList<double> horizontalOffsets = saveExtrusions
            ? request.HorizontalOffsets ?? layout.HorizontalOffsets
            : layout.HorizontalOffsets;
        IReadOnlyList<double> verticalOffsets = saveExtrusions
            ? request.VerticalOffsets ?? layout.VerticalOffsets
            : layout.VerticalOffsets;
        PanelCladdingTopologyState topology = saveExtrusions
            ? request.Topology ?? layout.Topology
            : layout.Topology;
        OperationResponse<PanelCladdingTopologyNormalizationResult> normalizationResponse =
            _topologyNormalizer.Normalize(
                horizontalOffsets,
                verticalOffsets,
                request.CellValues,
                topology,
                layout.Width,
                layout.Height,
                layout.ModelTolerance);
        if (!normalizationResponse.Success || normalizationResponse.Data is null)
        {
            return OperationResponse<PanelCladdingSaveResult>.Fail(normalizationResponse.Message);
        }
        PanelCladdingTopologyNormalizationResult normalizedEdit = normalizationResponse.Data;
        horizontalOffsets = normalizedEdit.HorizontalOffsets;
        verticalOffsets = normalizedEdit.VerticalOffsets;
        topology = normalizedEdit.Topology;
        PanelFrameAssignmentState requestedFrameAssignments = saveExtrusions
            ? request.FrameAssignments ?? layout.FrameAssignments
            : layout.FrameAssignments;
        OperationResponse<PanelFrameAssignmentState> normalizedFrameAssignments = _frameAssignments.Normalize(
            requestedFrameAssignments,
            horizontalOffsets.Count,
            verticalOffsets.Count,
            topology);
        if (!normalizedFrameAssignments.Success || normalizedFrameAssignments.Data is null)
        {
            return OperationResponse<PanelCladdingSaveResult>.Fail(normalizedFrameAssignments.Message);
        }
        requestedFrameAssignments = normalizedFrameAssignments.Data;
        OperationResponse<PanelCladdingKeySet> keySetResponse = _keys.CreateKeySet(
            horizontalOffsets,
            verticalOffsets,
            normalizedEdit.CellValues,
            layout.Width,
            layout.Height,
            layout.ModelTolerance);
        if (!keySetResponse.Success || keySetResponse.Data is null)
        {
            return OperationResponse<PanelCladdingSaveResult>.Fail(keySetResponse.Message);
        }
        var requestedLayout = new PanelCladdingLayout
        {
            ObjectId = layout.ObjectId,
            DocumentRuntimeSerialNumber = layout.DocumentRuntimeSerialNumber,
            DocumentPath = layout.DocumentPath,
            ObjectName = layout.ObjectName,
            LayerFullPath = layout.LayerFullPath,
            SystemCode = layout.SystemCode,
            GeometryFingerprint = layout.GeometryFingerprint,
            GeometryClass = layout.GeometryClass,
            GeometryDiagnostic = layout.GeometryDiagnostic,
            Width = layout.Width,
            Height = layout.Height,
            ModelTolerance = layout.ModelTolerance,
            ModelUnitScaleToMillimeters = layout.ModelUnitScaleToMillimeters,
            HorizontalOffsets = keySetResponse.Data.HorizontalOffsets,
            VerticalOffsets = keySetResponse.Data.VerticalOffsets,
            Cells = keySetResponse.Data.Cells,
            Topology = topology,
            FrameAssignments = requestedFrameAssignments,
            SourceUserText = layout.SourceUserText,
            Preview = layout.Preview,
            WorkbookPath = layout.WorkbookPath
        };

        var writes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        PanelCladdingCidService.AddPanelCidWrite(writes, layout.SourceUserText);
        if (saveCladding)
        {
            if (!requestedLayout.CanSave)
            {
                return OperationResponse<PanelCladdingSaveResult>.Fail(
                    $"PANEL_CLADDING_UNSUPPORTED_PROJECTION: {requestedLayout.GeometryDiagnostic}");
            }
            OperationResponse<PanelCladdingTopologyPayloads> validatedTopology = _keys.EncodeTopology(
                topology, requestedLayout.HorizontalOffsets.Count, requestedLayout.VerticalOffsets.Count);
            if (!validatedTopology.Success)
            {
                return OperationResponse<PanelCladdingSaveResult>.Fail(validatedTopology.Message);
            }
            IReadOnlyDictionary<string, string> expandedCellValues = _logicalCells.Expand(
                requestedLayout.Cells,
                topology,
                normalizedEdit.CellValues);
            OperationResponse<PanelCladdingRegionSet> regions = _regions.Resolve(
                requestedLayout.Cells, expandedCellValues, requirePopulatedCells: false);
            if (!regions.Success || regions.Data is null)
            {
                return OperationResponse<PanelCladdingSaveResult>.Fail(regions.Message);
            }

            IReadOnlyDictionary<string, string> logicalCellValues = _logicalCells.Collapse(
                requestedLayout.Cells,
                topology,
                regions.Data.NormalizedCellValues);
            foreach ((string key, string value) in logicalCellValues)
            {
                writes[key] = PanelCladdingKeyService.EncodeCellValueForStorage(value);
            }
            OperationResponse<string> claddingLogic = _claddingLogic.Encode(
                requestedLayout.Cells,
                regions.Data.NormalizedCellValues);
            if (!claddingLogic.Success || claddingLogic.Data is null)
            {
                return OperationResponse<PanelCladdingSaveResult>.Fail(claddingLogic.Message);
            }
            writes[PanelCladdingKeyService.CladdingLogicKey] = claddingLogic.Data;
        }

        if (saveExtrusions)
        {
            OperationResponse<IReadOnlyDictionary<string, string>> topologyResponse =
                _keys.EncodeFrameConfigurationUserText(
                    topology,
                    requestedLayout.HorizontalOffsets.Count,
                    requestedLayout.VerticalOffsets.Count);
            if (!topologyResponse.Success || topologyResponse.Data is null)
            {
                return OperationResponse<PanelCladdingSaveResult>.Fail(topologyResponse.Message);
            }

            foreach ((string key, string value) in topologyResponse.Data)
            {
                writes[key] = value;
            }
            OperationResponse<string> assignmentPayload = _frameAssignments.Encode(
                requestedFrameAssignments,
                requestedLayout.HorizontalOffsets.Count,
                requestedLayout.VerticalOffsets.Count,
                topology);
            if (!assignmentPayload.Success || assignmentPayload.Data is null)
            {
                return OperationResponse<PanelCladdingSaveResult>.Fail(assignmentPayload.Message);
            }
            if (!string.IsNullOrWhiteSpace(assignmentPayload.Data))
            {
                writes[PanelCladdingKeyService.FrameTypeKey] = assignmentPayload.Data;
            }
            writes[PanelCladdingKeyService.UnitWidthKey] =
                PanelCladdingKeyService.FormatUnitDimension(layout.Width);
            writes[PanelCladdingKeyService.UnitHeightKey] =
                PanelCladdingKeyService.FormatUnitDimension(layout.Height);
            writes[PanelCladdingKeyService.UnitDimensionKey] =
                $"{PanelCladdingKeyService.FormatUnitDimension(layout.Width)}x" +
                PanelCladdingKeyService.FormatUnitDimension(layout.Height);
            for (int index = 0; index < requestedLayout.HorizontalOffsets.Count; index++)
            {
                writes[PanelCladdingKeyService.GetHorizontalOffsetKey(index)] =
                    PanelCladdingKeyService.FormatOffset(requestedLayout.HorizontalOffsets[index]);
            }
            for (int index = 0; index < requestedLayout.VerticalOffsets.Count; index++)
            {
                writes[PanelCladdingKeyService.GetVerticalOffsetKey(index)] =
                    PanelCladdingKeyService.FormatOffset(requestedLayout.VerticalOffsets[index]);
            }
        }

        string[] obsoleteGridKeys = layout.SourceUserText.Keys
            .Where(key =>
                ((saveCladding && _keys.IsCladdingCellKey(key)) ||
                 (saveExtrusions && (
                     _keys.IsOffsetKey(key) ||
                     PanelCladdingKeyService.IsTopologyKey(key) ||
                     string.Equals(key, PanelCladdingKeyService.FrameTypeKey, StringComparison.OrdinalIgnoreCase) ||
                     PanelCladdingKeyService.IsRetiredFrameKey(key)))) &&
                !writes.ContainsKey(key))
            .ToArray();
        string[] userTextDeletes = obsoleteGridKeys
            .Concat(saveExtrusions ? layout.SourceUserText.Keys.Where(key =>
                string.Equals(key, PanelCladdingKeyService.FrameConfigKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, PanelCladdingKeyService.FrameTypeKey, StringComparison.OrdinalIgnoreCase)) : [])
            .Concat(layout.SourceUserText.Keys.Where(PanelCladdingKeyService.IsRetiredCladdingTypeKey))
            .Concat([
                PanelCladdingKeyService.SignatureKey,
                PanelCladdingKeyService.LegacySignatureKey
            ])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        OperationResponse<PanelAttributeCommitResult> committed = _liveRepository.CommitAttributes(
            new PanelAttributeCommitRequest
            {
                FilePath = request.FilePath,
                ObjectId = request.ObjectId,
                ExpectedGeometryFingerprint = request.ExpectedGeometryFingerprint,
                UserTextDeletes = userTextDeletes,
                UserTextWrites = writes,
                WorkbookPath = request.WorkbookPath
            },
            () => OperationResponse.Ok("Panel cladding key/value set prepared."));
        if (!committed.Success || committed.Data is null)
        {
            return OperationResponse<PanelCladdingSaveResult>.Fail(committed.Message);
        }

        return OperationResponse<PanelCladdingSaveResult>.Ok(new PanelCladdingSaveResult
        {
            ObjectId = request.ObjectId,
            FrameConfig = writes.GetValueOrDefault(PanelCladdingKeyService.FrameConfigKey) ??
                GetUserText(layout.SourceUserText, PanelCladdingKeyService.FrameConfigKey),
            StoredSignature = string.Empty,
            WorkbookPath = request.WorkbookPath,
            SheetName = string.Empty,
            ReusedExistingType = false
        }, request.Scope switch
        {
            PanelCladdingSaveScope.Extrusions => "Panel extrusion key/value set saved to Rhino.",
            PanelCladdingSaveScope.Cladding => "Panel cladding key/value set saved to Rhino.",
            _ => "Panel extrusion and cladding key/value sets saved to Rhino."
        });
    }

    private static string GetUserText(
        IReadOnlyDictionary<string, string> userText,
        string requestedKey) =>
        userText.FirstOrDefault(item =>
            string.Equals(item.Key, requestedKey, StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty;
}

