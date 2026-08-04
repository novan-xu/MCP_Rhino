using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Agents.Takeoff;

public sealed class TakeoffSpreadsheetAgent
{
    private readonly RhinoTakeoffScheduleService _takeoffService;

    public TakeoffSpreadsheetAgent(RhinoTakeoffScheduleService takeoffService)
    {
        _takeoffService = takeoffService;
    }

    public OperationResponse<TakeoffSpreadsheetAgentResponse> Run(TakeoffSpreadsheetAgentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return OperationResponse<TakeoffSpreadsheetAgentResponse>.Fail("FilePath is required.");
        }

        if (request.Spec is not null)
        {
            return RunWithSpec(request, request.Spec);
        }

        OperationResponse<TakeoffSourcesInspectionResponse> discovery = _takeoffService.InspectSources(new InspectTakeoffSourcesRequest
        {
            FilePath = request.FilePath,
            Scope = request.ScopeHints,
            SampleValueCount = 8
        });
        if (!discovery.Success || discovery.Data is null)
        {
            return OperationResponse<TakeoffSpreadsheetAgentResponse>.Fail(discovery.Message);
        }

        TakeoffScheduleSpecRequest? proposedSpec = BuildSpecFromKnownMappings(request);
        List<TakeoffClarificationQuestion> questions = BuildClarificationQuestions(request, discovery.Data, proposedSpec);
        if (request.Mode == TakeoffAgentMode.Preview && proposedSpec is not null && questions.Count == 0)
        {
            return RunWithSpec(request, proposedSpec);
        }

        if (request.Mode == TakeoffAgentMode.Export && proposedSpec is not null && questions.Count == 0)
        {
            return RunWithSpec(request, proposedSpec);
        }

        return OperationResponse<TakeoffSpreadsheetAgentResponse>.Ok(new TakeoffSpreadsheetAgentResponse
        {
            FilePath = discovery.Data.FilePath,
            Status = TakeoffAgentStatus.NeedsClarification,
            StatusReason = "TAKEOFF_CLARIFICATION_REQUIRED: The request needs confirmed mappings, quantity source, grouping/sorting, or output destination before calculation/export.",
            ProposedSpec = proposedSpec,
            Discovery = discovery.Data,
            ClarificationQuestions = questions,
            Warnings = discovery.Data.Warnings
        }, "Take-off spreadsheet agent needs clarification.");
    }

    private OperationResponse<TakeoffSpreadsheetAgentResponse> RunWithSpec(
        TakeoffSpreadsheetAgentRequest request,
        TakeoffScheduleSpecRequest spec)
    {
        if (request.Mode == TakeoffAgentMode.Export)
        {
            List<TakeoffClarificationQuestion> outputQuestions = BuildOutputQuestions(request);
            if (outputQuestions.Count > 0)
            {
                return OperationResponse<TakeoffSpreadsheetAgentResponse>.Ok(new TakeoffSpreadsheetAgentResponse
                {
                    FilePath = request.FilePath,
                    Status = TakeoffAgentStatus.NeedsClarification,
                    StatusReason = "TAKEOFF_OUTPUT_REQUIRED: Export requires explicit outputDirectory and outputFileName.",
                    ProposedSpec = spec,
                    ClarificationQuestions = outputQuestions
                }, "Take-off export needs output destination.");
            }

            OperationResponse<TakeoffScheduleExportResponse> export = _takeoffService.Export(new ExportTakeoffScheduleRequest
            {
                FilePath = request.FilePath,
                OutputDirectory = request.OutputDirectory,
                OutputFileName = request.OutputFileName,
                OverwriteExisting = request.OverwriteExisting,
                Spec = spec
            });

            if (!export.Success || export.Data is null)
            {
                return OperationResponse<TakeoffSpreadsheetAgentResponse>.Fail(export.Message);
            }

            return OperationResponse<TakeoffSpreadsheetAgentResponse>.Ok(new TakeoffSpreadsheetAgentResponse
            {
                FilePath = export.Data.FilePath,
                Status = TakeoffAgentStatus.Exported,
                StatusReason = "Take-off spreadsheet exported.",
                ProposedSpec = spec,
                Export = export.Data,
                Warnings = export.Data.Warnings
            }, "Take-off spreadsheet exported.");
        }

        if (request.Mode == TakeoffAgentMode.Preview)
        {
            OperationResponse<TakeoffSchedulePreviewResponse> preview = _takeoffService.Preview(new PreviewTakeoffScheduleRequest
            {
                FilePath = request.FilePath,
                Spec = spec
            });

            if (!preview.Success || preview.Data is null)
            {
                return OperationResponse<TakeoffSpreadsheetAgentResponse>.Fail(preview.Message);
            }

            return OperationResponse<TakeoffSpreadsheetAgentResponse>.Ok(new TakeoffSpreadsheetAgentResponse
            {
                FilePath = preview.Data.FilePath,
                Status = TakeoffAgentStatus.PreviewReady,
                StatusReason = "Take-off preview is ready.",
                ProposedSpec = spec,
                Preview = preview.Data,
                Warnings = preview.Data.Warnings
            }, "Take-off preview is ready.");
        }

        return OperationResponse<TakeoffSpreadsheetAgentResponse>.Ok(new TakeoffSpreadsheetAgentResponse
        {
            FilePath = request.FilePath,
            Status = TakeoffAgentStatus.NeedsClarification,
            StatusReason = "Review the proposed spec and request Preview or Export when ready.",
            ProposedSpec = spec,
            ClarificationQuestions = BuildClarificationQuestions(request, null, spec)
        }, "Take-off spec proposed.");
    }

    private static TakeoffScheduleSpecRequest? BuildSpecFromKnownMappings(TakeoffSpreadsheetAgentRequest request)
    {
        List<TakeoffConceptMappingRequest> mappings = request.KnownMappings
            .Where(mapping => !string.IsNullOrWhiteSpace(mapping.Concept))
            .ToList();
        if (mappings.Count == 0)
        {
            return null;
        }

        var columns = mappings.Select(mapping => new TakeoffColumnSpecRequest
        {
            Name = NormalizeColumnName(mapping.Concept),
            Source = mapping.Source
        }).ToList();

        var groupBy = columns
            .Where(column => column.Source.Kind is not TakeoffColumnSourceKind.GeometryMetric and not TakeoffColumnSourceKind.Expression)
            .Select(column => column.Name)
            .Take(4)
            .ToList();

        var aggregates = new List<TakeoffAggregateSpecRequest>
        {
            new() { Name = "Count", Function = TakeoffAggregateFunction.Count }
        };
        foreach (TakeoffColumnSpecRequest metricColumn in columns.Where(column =>
            column.Source.Kind is TakeoffColumnSourceKind.GeometryMetric or TakeoffColumnSourceKind.Expression))
        {
            aggregates.Add(new TakeoffAggregateSpecRequest
            {
                Name = $"Total{metricColumn.Name}",
                Function = TakeoffAggregateFunction.Sum,
                Column = metricColumn.Name
            });
        }

        return new TakeoffScheduleSpecRequest
        {
            Sheets = new List<TakeoffSheetSpecRequest>
            {
                new()
                {
                    Name = "Takeoff",
                    Scope = request.ScopeHints,
                    Columns = columns,
                    GroupBy = groupBy,
                    Aggregates = aggregates,
                    RowMode = TakeoffRowMode.Aggregate,
                    SortBy = groupBy.Select(column => new TakeoffSortSpecRequest
                    {
                        Column = column,
                        Direction = TakeoffSortDirection.Ascending
                    }).ToList()
                }
            }
        };
    }

    private static List<TakeoffClarificationQuestion> BuildClarificationQuestions(
        TakeoffSpreadsheetAgentRequest request,
        TakeoffSourcesInspectionResponse? discovery,
        TakeoffScheduleSpecRequest? proposedSpec)
    {
        var questions = new List<TakeoffClarificationQuestion>();
        if (request.Mode == TakeoffAgentMode.Export)
        {
            questions.AddRange(BuildOutputQuestions(request));
        }

        if (proposedSpec is null)
        {
            questions.Add(new TakeoffClarificationQuestion
            {
                Id = "takeoff-spec-or-mappings",
                Prompt = "Provide a structured take-off spec or confirmed concept mappings for the schedule columns.",
                Reason = "Attribute meanings differ by Rhino file; the server should not guess which keys represent panel type, finish, level, mark, or quantity.",
                Options = ToOptions(discovery?.UserTextKeys.Select(item => item.Key))
            });
        }

        if (RequiresQuantitySource(request.UserRequest, proposedSpec))
        {
            questions.Add(new TakeoffClarificationQuestion
            {
                Id = "quantity-source",
                Prompt = "Confirm whether the take-off quantity should come from geometry metrics, bounding-box dimensions, or user text dimensions.",
                Reason = "Panel quantity rules vary by model; exporting without this choice can produce the wrong take-off.",
                Options = Enum.GetNames<TakeoffGeometryMetric>()
            });
        }

        if (proposedSpec is null || proposedSpec.Sheets.Any(sheet => sheet.GroupBy.Count == 0 && sheet.RowMode == TakeoffRowMode.Aggregate))
        {
            questions.Add(new TakeoffClarificationQuestion
            {
                Id = "grouping-and-sorting",
                Prompt = "Confirm whether rows should be detailed per object or grouped, and which columns should define grouping and sort order.",
                Reason = "The spreadsheet shape is user-defined and should not be silently fixed by the server.",
                Options = ToOptions(proposedSpec?.Sheets.SelectMany(sheet => sheet.Columns.Select(column => column.Name)).Distinct(StringComparer.OrdinalIgnoreCase)
                    ?? discovery?.UserTextKeys.Select(item => item.Key))
            });
        }

        return questions
            .GroupBy(question => question.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static List<TakeoffClarificationQuestion> BuildOutputQuestions(TakeoffSpreadsheetAgentRequest request)
    {
        var questions = new List<TakeoffClarificationQuestion>();
        if (string.IsNullOrWhiteSpace(request.OutputDirectory))
        {
            questions.Add(new TakeoffClarificationQuestion
            {
                Id = "output-directory",
                Prompt = "What absolute existing folder should receive the spreadsheet?",
                Reason = "The server must not invent an output location for open-world file writes."
            });
        }

        if (string.IsNullOrWhiteSpace(request.OutputFileName))
        {
            questions.Add(new TakeoffClarificationQuestion
            {
                Id = "output-file-name",
                Prompt = "What output file name should be used, including .csv or .xlsx?",
                Reason = "The server must not invent output file names or spreadsheet formats."
            });
        }

        return questions;
    }

    private static bool RequiresQuantitySource(string userRequest, TakeoffScheduleSpecRequest? proposedSpec)
    {
        if (string.IsNullOrWhiteSpace(userRequest))
        {
            return false;
        }

        bool asksForQuantity = userRequest.Contains("area", StringComparison.OrdinalIgnoreCase)
            || userRequest.Contains("length", StringComparison.OrdinalIgnoreCase)
            || userRequest.Contains("volume", StringComparison.OrdinalIgnoreCase)
            || userRequest.Contains("quantity", StringComparison.OrdinalIgnoreCase)
            || userRequest.Contains("take-off", StringComparison.OrdinalIgnoreCase)
            || userRequest.Contains("takeoff", StringComparison.OrdinalIgnoreCase);
        if (!asksForQuantity)
        {
            return false;
        }

        return proposedSpec is null || !proposedSpec.Sheets
            .SelectMany(sheet => sheet.Columns)
            .Any(column => column.Source.Kind is TakeoffColumnSourceKind.GeometryMetric or TakeoffColumnSourceKind.Expression);
    }

    private static IReadOnlyList<string> ToOptions(IEnumerable<string>? options)
    {
        return options?.ToList() ?? new List<string>();
    }

    private static string NormalizeColumnName(string concept)
    {
        string trimmed = concept.Trim();
        return string.IsNullOrWhiteSpace(trimmed)
            ? "Column"
            : string.Concat(trimmed.Select(character => char.IsLetterOrDigit(character) ? character : '_')).Trim('_');
    }
}
