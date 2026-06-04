using System.Diagnostics;
using System.Globalization;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoTakeoffScheduleService
{
    private readonly ILiveRhinoDocumentAccessor _documentAccessor;
    private readonly ILiveTakeoffMetricReader _metricReader;
    private readonly TakeoffScheduleValidationService _validationService;
    private readonly TakeoffScheduleCalculationService _calculationService;
    private readonly IEnumerable<ISpreadsheetWorkbookWriter> _writers;

    public RhinoTakeoffScheduleService(
        ILiveRhinoDocumentAccessor documentAccessor,
        ILiveTakeoffMetricReader metricReader,
        TakeoffScheduleValidationService validationService,
        TakeoffScheduleCalculationService calculationService,
        IEnumerable<ISpreadsheetWorkbookWriter> writers)
    {
        _documentAccessor = documentAccessor;
        _metricReader = metricReader;
        _validationService = validationService;
        _calculationService = calculationService;
        _writers = writers;
    }

    public OperationResponse<TakeoffSourcesInspectionResponse> InspectSources(InspectTakeoffSourcesRequest request)
    {
        OperationResponse filePathValidation = _validationService.ValidateFilePath(request.FilePath);
        if (!filePathValidation.Success)
        {
            return OperationResponse<TakeoffSourcesInspectionResponse>.Fail(filePathValidation.Message);
        }

        int sampleValueCount = Math.Clamp(request.SampleValueCount, 1, 20);
        OperationResponse<TakeoffLiveSnapshot> snapshot = ReadSnapshot(request.FilePath, request.Scope);
        if (!snapshot.Success || snapshot.Data is null)
        {
            return OperationResponse<TakeoffSourcesInspectionResponse>.Fail(snapshot.Message);
        }

        TakeoffSourcesInspectionResponse response = BuildInspectionResponse(snapshot.Data, sampleValueCount);
        return OperationResponse<TakeoffSourcesInspectionResponse>.Ok(
            response,
            $"Inspected take-off sources for {response.MatchedObjectCount} live objects.");
    }

    public OperationResponse<TakeoffSchedulePreviewResponse> Preview(PreviewTakeoffScheduleRequest request)
    {
        OperationResponse filePathValidation = _validationService.ValidateFilePath(request.FilePath);
        if (!filePathValidation.Success)
        {
            return OperationResponse<TakeoffSchedulePreviewResponse>.Fail(filePathValidation.Message);
        }

        OperationResponse specValidation = _validationService.ValidateSpec(request.Spec);
        if (!specValidation.Success)
        {
            return OperationResponse<TakeoffSchedulePreviewResponse>.Fail(specValidation.Message);
        }

        OperationResponse<IReadOnlyList<TakeoffLiveSnapshot>> snapshots = ReadSnapshots(request.FilePath, request.Spec);
        if (!snapshots.Success || snapshots.Data is null)
        {
            return OperationResponse<TakeoffSchedulePreviewResponse>.Fail(snapshots.Message);
        }

        OperationResponse<TakeoffScheduleCalculationResult> calculated = _calculationService.Calculate(
            request.FilePath,
            request.Spec,
            snapshots.Data,
            request.SampleRowCount);
        if (!calculated.Success || calculated.Data is null)
        {
            return OperationResponse<TakeoffSchedulePreviewResponse>.Fail(calculated.Message);
        }

        return OperationResponse<TakeoffSchedulePreviewResponse>.Ok(calculated.Data.Preview, "Take-off preview calculated.");
    }

    public OperationResponse<TakeoffScheduleExportResponse> Export(ExportTakeoffScheduleRequest request)
    {
        OperationResponse outputValidation = _validationService.ValidateFilePath(request.FilePath);
        if (!outputValidation.Success)
        {
            return OperationResponse<TakeoffScheduleExportResponse>.Fail(outputValidation.Message);
        }

        OperationResponse<TakeoffValidatedOutput> output = _validationService.ValidateOutput(
            request.FilePath,
            request.OutputDirectory,
            request.OutputFileName,
            request.OverwriteExisting);
        if (!output.Success || output.Data is null)
        {
            return OperationResponse<TakeoffScheduleExportResponse>.Fail(output.Message);
        }

        OperationResponse specValidation = _validationService.ValidateSpec(request.Spec);
        if (!specValidation.Success)
        {
            return OperationResponse<TakeoffScheduleExportResponse>.Fail(specValidation.Message);
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        OperationResponse<IReadOnlyList<TakeoffLiveSnapshot>> snapshots = ReadSnapshots(request.FilePath, request.Spec);
        if (!snapshots.Success || snapshots.Data is null)
        {
            return OperationResponse<TakeoffScheduleExportResponse>.Fail(snapshots.Message);
        }

        string activeFilePath = snapshots.Data.FirstOrDefault()?.FilePath ?? request.FilePath;
        OperationResponse<TakeoffValidatedOutput> activeOutputValidation = _validationService.ValidateOutput(
            activeFilePath,
            request.OutputDirectory,
            request.OutputFileName,
            request.OverwriteExisting);
        if (!activeOutputValidation.Success || activeOutputValidation.Data is null)
        {
            return OperationResponse<TakeoffScheduleExportResponse>.Fail(activeOutputValidation.Message);
        }

        OperationResponse<TakeoffScheduleCalculationResult> calculated = _calculationService.Calculate(
            activeFilePath,
            request.Spec,
            snapshots.Data,
            sampleRowCount: 0);
        if (!calculated.Success || calculated.Data is null)
        {
            return OperationResponse<TakeoffScheduleExportResponse>.Fail(calculated.Message);
        }

        ISpreadsheetWorkbookWriter? writer = _writers.FirstOrDefault(item => item.Supports(activeOutputValidation.Data.Format));
        if (writer is null)
        {
            return OperationResponse<TakeoffScheduleExportResponse>.Fail(
                $"TAKEOFF_WRITER_UNAVAILABLE: No spreadsheet writer is registered for {activeOutputValidation.Data.Format}.");
        }

        OperationResponse<TakeoffSpreadsheetWriteResult> written = writer.Write(
            calculated.Data.Workbook,
            activeOutputValidation.Data.Format,
            activeOutputValidation.Data.OutputPath,
            request.OverwriteExisting);
        if (!written.Success || written.Data is null)
        {
            return OperationResponse<TakeoffScheduleExportResponse>.Fail(written.Message);
        }

        stopwatch.Stop();
        var warnings = calculated.Data.Warnings.Concat(written.Data.Warnings).ToList();
        int rowCount = calculated.Data.Workbook.Worksheets.Sum(sheet => sheet.Rows.Count);

        return OperationResponse<TakeoffScheduleExportResponse>.Ok(new TakeoffScheduleExportResponse
        {
            FilePath = activeFilePath,
            OutputPath = written.Data.OutputPath,
            Format = activeOutputValidation.Data.Format,
            SheetCount = calculated.Data.Workbook.Worksheets.Count,
            RowCount = rowCount,
            OutputFileSizeBytes = written.Data.ByteCount,
            DurationMs = stopwatch.ElapsedMilliseconds,
            Warnings = warnings
        }, "Take-off spreadsheet exported.");
    }

    private OperationResponse<IReadOnlyList<TakeoffLiveSnapshot>> ReadSnapshots(string filePath, TakeoffScheduleSpecRequest spec)
    {
        var snapshots = new List<TakeoffLiveSnapshot>(spec.Sheets.Count);
        foreach (TakeoffSheetSpecRequest sheet in spec.Sheets)
        {
            OperationResponse<TakeoffLiveSnapshot> snapshot = ReadSnapshot(filePath, sheet.Scope);
            if (!snapshot.Success || snapshot.Data is null)
            {
                return OperationResponse<IReadOnlyList<TakeoffLiveSnapshot>>.Fail(snapshot.Message);
            }

            snapshots.Add(snapshot.Data);
        }

        return OperationResponse<IReadOnlyList<TakeoffLiveSnapshot>>.Ok(snapshots);
    }

    private OperationResponse<TakeoffLiveSnapshot> ReadSnapshot(string filePath, TakeoffScopeRequest? scope)
    {
        return _documentAccessor.Execute(filePath, document => _metricReader.Read(document, document.Path, scope));
    }

    private static TakeoffSourcesInspectionResponse BuildInspectionResponse(
        TakeoffLiveSnapshot snapshot,
        int sampleValueCount)
    {
        IReadOnlyList<TakeoffObjectSnapshot> objects = snapshot.Objects;
        var warnings = snapshot.Warnings.ToList();
        if (objects.Count > sampleValueCount)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "TAKEOFF_SAMPLE_TRUNCATED",
                Message = $"Object samples were limited to {sampleValueCount}."
            });
        }

        var userTextKeys = objects
            .SelectMany(item => item.UserText.Select(entry => new { entry.Key, entry.Value }))
            .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                List<string> values = group
                    .Select(item => item.Value)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(sampleValueCount)
                    .ToList();
                return new TakeoffUserTextKeySummaryResponse
                {
                    Key = group.Key,
                    ObjectCount = group.Count(),
                    NumericValueCount = group.Count(item => double.TryParse(item.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)),
                    SampleValues = values
                };
            })
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var response = new TakeoffSourcesInspectionResponse
        {
            FilePath = snapshot.FilePath,
            TotalObjectCount = snapshot.TotalObjectCount,
            MatchedObjectCount = snapshot.MatchedObjectCount,
            Layers = objects
                .GroupBy(item => item.LayerFullPath, StringComparer.OrdinalIgnoreCase)
                .Select(group => new TakeoffLayerSummaryResponse { FullPath = group.Key, ObjectCount = group.Count() })
                .OrderBy(item => item.FullPath, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            ObjectTypes = objects
                .GroupBy(item => item.ObjectType, StringComparer.OrdinalIgnoreCase)
                .Select(group => new TakeoffObjectTypeSummaryResponse { ObjectType = group.Key, ObjectCount = group.Count() })
                .OrderBy(item => item.ObjectType, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            UserTextKeys = userTextKeys,
            MetricAvailability = BuildMetricAvailability(objects),
            SampleObjects = objects
                .Take(sampleValueCount)
                .Select(item => new TakeoffObjectSampleResponse
                {
                    ObjectId = item.ObjectId,
                    ObjectName = item.ObjectName,
                    ObjectType = item.ObjectType,
                    GeometryType = item.GeometryType,
                    LayerFullPath = item.LayerFullPath,
                    UserText = item.UserText
                })
                .ToList(),
            ClarificationQuestions = BuildInspectionQuestions(userTextKeys),
            Warnings = warnings
        };

        return response;
    }

    private static IReadOnlyList<TakeoffMetricAvailabilityResponse> BuildMetricAvailability(
        IReadOnlyList<TakeoffObjectSnapshot> objects)
    {
        return new List<TakeoffMetricAvailabilityResponse>
        {
            new() { Metric = TakeoffGeometryMetric.Count, AvailableObjectCount = objects.Count },
            new() { Metric = TakeoffGeometryMetric.Length, AvailableObjectCount = objects.Count(item => item.Metrics.Length.HasValue) },
            new() { Metric = TakeoffGeometryMetric.Area, AvailableObjectCount = objects.Count(item => item.Metrics.Area.HasValue) },
            new() { Metric = TakeoffGeometryMetric.Volume, AvailableObjectCount = objects.Count(item => item.Metrics.Volume.HasValue) },
            new() { Metric = TakeoffGeometryMetric.BoundingBoxSizeX, AvailableObjectCount = objects.Count(item => item.Metrics.BoundingBoxSizeX.HasValue) },
            new() { Metric = TakeoffGeometryMetric.BoundingBoxSizeY, AvailableObjectCount = objects.Count(item => item.Metrics.BoundingBoxSizeY.HasValue) },
            new() { Metric = TakeoffGeometryMetric.BoundingBoxSizeZ, AvailableObjectCount = objects.Count(item => item.Metrics.BoundingBoxSizeZ.HasValue) }
        };
    }

    private static IReadOnlyList<TakeoffClarificationQuestion> BuildInspectionQuestions(
        IReadOnlyList<TakeoffUserTextKeySummaryResponse> userTextKeys)
    {
        var questions = new List<TakeoffClarificationQuestion>();
        if (userTextKeys.Count > 0)
        {
            questions.Add(new TakeoffClarificationQuestion
            {
                Id = "attribute-mapping",
                Prompt = "Which discovered user text keys should map to the requested schedule concepts?",
                Reason = "User text key meanings vary by Rhino file and should be confirmed before take-off calculation.",
                Options = userTextKeys.Select(item => item.Key).ToList()
            });
        }

        questions.Add(new TakeoffClarificationQuestion
        {
            Id = "quantity-source",
            Prompt = "Which quantity source should be used for measured values such as panel area, length, volume, or bounding-box dimensions?",
            Reason = "The server cannot assume whether the user's quantity should come from geometry metrics or user text dimensions.",
            Options = Enum.GetNames<TakeoffGeometryMetric>()
        });

        return questions;
    }
}
