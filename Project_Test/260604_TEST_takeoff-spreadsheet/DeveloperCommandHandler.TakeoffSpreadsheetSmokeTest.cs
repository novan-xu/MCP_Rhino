using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using MCP_Rhino.Server.Infrastructure.Plugin;
using MCP_Rhino.Server.Infrastructure.Spreadsheet;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string TakeoffSpreadsheetSlug = "takeoff-spreadsheet-smoke-test";

    partial void RegisterTakeoffSpreadsheetHandlers()
    {
        _extensionHandlers[TakeoffSpreadsheetSlug] = HandleTakeoffSpreadsheetSmokeTest;
    }

    private bool HandleTakeoffSpreadsheetSmokeTest(string[] args)
    {
        try
        {
            string filePath = args.Length > 1
                ? Path.GetFullPath(args[1])
                : "C:/mcp-rhino/takeoff-spreadsheet-smoke.3dm";

            if (McpRhinoPlugin.Instance is null)
            {
                RunTakeoffSpreadsheetCliFallbackSmoke(filePath);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath))
                {
                    throw new InvalidOperationException("Live takeoff spreadsheet smoke requires a saved active document path.");
                }

                RunTakeoffSpreadsheetLiveSmoke(filePath);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Takeoff spreadsheet smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunTakeoffSpreadsheetCliFallbackSmoke(string filePath)
    {
        string outputRoot = PrepareTakeoffSpreadsheetOutputRoot();
        TakeoffScheduleSpecRequest spec = BuildTakeoffSmokeSpec("MCP::TAKEOFF_SPREADSHEET_SMOKE");

        RequireTakeoffSpreadsheetFailure(
            _takeoffScheduleService.InspectSources(new InspectTakeoffSourcesRequest
            {
                FilePath = filePath,
                Scope = spec.Sheets[0].Scope
            }),
            "LIVE_RHINO_REQUIRED",
            "InspectTakeoffSources should require live Rhino in CLI fallback mode.");

        RequireTakeoffSpreadsheetFailure(
            _takeoffScheduleService.Preview(new PreviewTakeoffScheduleRequest
            {
                FilePath = filePath,
                Spec = spec
            }),
            "LIVE_RHINO_REQUIRED",
            "PreviewTakeoffSchedule should require live Rhino in CLI fallback mode.");

        RequireTakeoffSpreadsheetFailure(
            _takeoffScheduleService.Export(new ExportTakeoffScheduleRequest
            {
                FilePath = filePath,
                Spec = spec
            }),
            "TAKEOFF_OUTPUT_REQUIRED",
            "ExportTakeoffSchedule should require explicit outputDirectory and outputFileName before live execution.");

        RequireTakeoffSpreadsheetFailure(
            _takeoffScheduleService.Export(new ExportTakeoffScheduleRequest
            {
                FilePath = filePath,
                OutputDirectory = outputRoot,
                OutputFileName = "cli-fallback.csv",
                OverwriteExisting = true,
                Spec = spec
            }),
            "LIVE_RHINO_REQUIRED",
            "ExportTakeoffSchedule should require live Rhino after output validation.");

        RequireTakeoffSpreadsheetFailure(
            _takeoffSpreadsheetAgent.Run(new TakeoffSpreadsheetAgentRequest
            {
                FilePath = filePath,
                UserRequest = "Preview a panel take-off grouped by panel type and finish.",
                Mode = TakeoffAgentMode.Preview,
                Spec = spec
            }),
            "LIVE_RHINO_REQUIRED",
            "RunTakeoffSpreadsheetAgent preview should require live Rhino in CLI fallback mode.");

        RunTakeoffSpreadsheetWriterSmoke(outputRoot);

        Console.WriteLine("[OK] takeoff-spreadsheet CLI fallback smoke completed.");
    }

    private void RunTakeoffSpreadsheetLiveSmoke(string filePath)
    {
        string outputRoot = PrepareTakeoffSpreadsheetOutputRoot();
        CleanupTakeoffSpreadsheetOutput(outputRoot);

        string suffix = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        string smokeLayer = $"MCP::TAKEOFF_SPREADSHEET_SMOKE_{suffix}";

        RequireTakeoffSpreadsheetSuccess(_layerManagementService.Create(new CreateLayersRequest
        {
            FilePath = filePath,
            Entries = new List<LayerCreationEntryRequest>
            {
                new() { FullPath = smokeLayer }
            }
        }), "Create takeoff spreadsheet smoke layer");

        ArchitecturalCreationResponse boxes = RequireTakeoffSpreadsheetSuccess(
            _architecturalPrimitiveCreationSkill.CreateBoxes(new CreateBoxesRequest
            {
                FilePath = filePath,
                AutoCreateLayers = false,
                Common = new GeometryCreationCommonOptions
                {
                    LayerFullPath = smokeLayer,
                    Name = "takeoff spreadsheet smoke panel",
                    UserText = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["PanelType"] = "SmokePanel",
                        ["Finish"] = "Anodized"
                    }
                },
                Metadata = new ArchitecturalMetadataRequest
                {
                    Category = "Smoke",
                    SystemName = "TakeoffSpreadsheet",
                    SourceTag = suffix
                },
                Items = new List<BoxItemRequest>
                {
                    new()
                    {
                        OriginX = 0d,
                        OriginY = 0d,
                        OriginZ = 0d,
                        SizeX = 2d,
                        SizeY = 1d,
                        SizeZ = 1d,
                        Representation = ArchitecturalRepresentationKind.Brep,
                        Name = "takeoff_spreadsheet_panel_a"
                    },
                    new()
                    {
                        OriginX = 3d,
                        OriginY = 0d,
                        OriginZ = 0d,
                        SizeX = 4d,
                        SizeY = 1d,
                        SizeZ = 1d,
                        Representation = ArchitecturalRepresentationKind.Brep,
                        Name = "takeoff_spreadsheet_panel_b"
                    }
                }
            }),
            "Create takeoff spreadsheet smoke panels");

        RequireTakeoffSpreadsheet(boxes.CreatedObjects.Count == 2, $"Expected two smoke panel objects, got {boxes.CreatedObjects.Count}.");

        TakeoffScheduleSpecRequest spec = BuildTakeoffSmokeSpec(smokeLayer);
        TakeoffSourcesInspectionResponse discovery = RequireTakeoffSpreadsheetSuccess(
            _takeoffScheduleService.InspectSources(new InspectTakeoffSourcesRequest
            {
                FilePath = filePath,
                Scope = spec.Sheets[0].Scope,
                SampleValueCount = 5
            }),
            "Inspect takeoff sources");
        RequireTakeoffSpreadsheet(discovery.MatchedObjectCount >= 2, "Discovery should match the smoke panels.");
        RequireTakeoffSpreadsheet(discovery.UserTextKeys.Any(key => string.Equals(key.Key, "PanelType", StringComparison.OrdinalIgnoreCase)), "Discovery should include PanelType user text.");

        TakeoffSchedulePreviewResponse preview = RequireTakeoffSpreadsheetSuccess(
            _takeoffScheduleService.Preview(new PreviewTakeoffScheduleRequest
            {
                FilePath = filePath,
                Spec = spec,
                SampleRowCount = 5
            }),
            "Preview takeoff schedule");
        RequireTakeoffSpreadsheet(preview.Sheets.Count == 1, "Preview should contain one sheet.");
        RequireTakeoffSpreadsheet(preview.Sheets[0].RowCount == 1, $"Expected one grouped row, got {preview.Sheets[0].RowCount}.");
        TakeoffPreviewRowResponse row = preview.Sheets[0].SampleRows[0];
        RequireTakeoffSpreadsheet(row.NumericValues.TryGetValue("PanelCount", out double panelCount) && Math.Abs(panelCount - 2d) < 0.001d, "PanelCount aggregate should be 2.");
        RequireTakeoffSpreadsheet(row.NumericValues.TryGetValue("TotalWidth", out double totalWidth) && Math.Abs(totalWidth - 6d) < 0.001d, "TotalWidth aggregate should be 6.");

        TakeoffSpreadsheetAgentResponse agentPreview = RequireTakeoffSpreadsheetSuccess(
            _takeoffSpreadsheetAgent.Run(new TakeoffSpreadsheetAgentRequest
            {
                FilePath = filePath,
                UserRequest = "Preview a panel take-off grouped by panel type and finish.",
                Mode = TakeoffAgentMode.Preview,
                Spec = spec
            }),
            "Run takeoff spreadsheet agent preview");
        RequireTakeoffSpreadsheet(agentPreview.Status == TakeoffAgentStatus.PreviewReady, $"Agent preview status should be PreviewReady, got {agentPreview.Status}.");

        TakeoffScheduleExportResponse csv = RequireTakeoffSpreadsheetSuccess(
            _takeoffScheduleService.Export(new ExportTakeoffScheduleRequest
            {
                FilePath = filePath,
                OutputDirectory = outputRoot,
                OutputFileName = $"takeoff-spreadsheet-{suffix}.csv",
                OverwriteExisting = true,
                Spec = spec
            }),
            "Export takeoff CSV");
        RequireTakeoffSpreadsheetOutput(csv.OutputPath, "CSV takeoff export should produce a non-empty file.");

        TakeoffScheduleExportResponse xlsx = RequireTakeoffSpreadsheetSuccess(
            _takeoffScheduleService.Export(new ExportTakeoffScheduleRequest
            {
                FilePath = filePath,
                OutputDirectory = outputRoot,
                OutputFileName = $"takeoff-spreadsheet-{suffix}.xlsx",
                OverwriteExisting = true,
                Spec = spec
            }),
            "Export takeoff XLSX");
        RequireTakeoffSpreadsheetOutput(xlsx.OutputPath, "XLSX takeoff export should produce a non-empty file.");

        Console.WriteLine("[OK] takeoff-spreadsheet live smoke completed.");
        Console.WriteLine($"[OK] outputRoot={outputRoot}");
        Console.WriteLine($"[OK] rowCount={preview.Sheets[0].RowCount}; csvBytes={csv.OutputFileSizeBytes}; xlsxBytes={xlsx.OutputFileSizeBytes}");
    }

    private static TakeoffScheduleSpecRequest BuildTakeoffSmokeSpec(string layerFullPath)
    {
        return new TakeoffScheduleSpecRequest
        {
            Sheets = new List<TakeoffSheetSpecRequest>
            {
                new()
                {
                    Name = "Panel Takeoff",
                    Scope = new TakeoffScopeRequest
                    {
                        ConfirmedLayerFullPaths = new List<string> { layerFullPath },
                        ObjectTypes = new List<string> { "Brep" }
                    },
                    Columns = new List<TakeoffColumnSpecRequest>
                    {
                        new()
                        {
                            Name = "PanelType",
                            Source = new TakeoffColumnSourceRequest
                            {
                                Kind = TakeoffColumnSourceKind.UserText,
                                Key = "PanelType"
                            },
                            NullPolicy = TakeoffNullPolicy.Fail
                        },
                        new()
                        {
                            Name = "Finish",
                            Source = new TakeoffColumnSourceRequest
                            {
                                Kind = TakeoffColumnSourceKind.UserText,
                                Key = "Finish"
                            },
                            NullPolicy = TakeoffNullPolicy.Fail
                        },
                        new()
                        {
                            Name = "Width",
                            Source = new TakeoffColumnSourceRequest
                            {
                                Kind = TakeoffColumnSourceKind.GeometryMetric,
                                Metric = TakeoffGeometryMetric.BoundingBoxSizeX
                            },
                            NullPolicy = TakeoffNullPolicy.Fail
                        }
                    },
                    GroupBy = new List<string> { "PanelType", "Finish" },
                    Aggregates = new List<TakeoffAggregateSpecRequest>
                    {
                        new() { Name = "PanelCount", Function = TakeoffAggregateFunction.Count },
                        new() { Name = "TotalWidth", Function = TakeoffAggregateFunction.Sum, Column = "Width" }
                    },
                    SortBy = new List<TakeoffSortSpecRequest>
                    {
                        new() { Column = "PanelType", Direction = TakeoffSortDirection.Ascending }
                    },
                    RowMode = TakeoffRowMode.Aggregate
                }
            }
        };
    }

    private static string PrepareTakeoffSpreadsheetOutputRoot()
    {
        string outputRoot = ResolveValidationDirectory(TakeoffSpreadsheetSlug);
        Directory.CreateDirectory(outputRoot);
        return outputRoot;
    }

    private static void RunTakeoffSpreadsheetWriterSmoke(string outputRoot)
    {
        var workbook = new TakeoffWorkbook
        {
            Worksheets = new List<TakeoffWorksheet>
            {
                new()
                {
                    Name = "Writer Smoke",
                    Columns = new List<string> { "PanelType", "PanelCount", "FormulaText" },
                    Rows = new List<TakeoffWorkbookRow>
                    {
                        new()
                        {
                            Cells = new Dictionary<string, TakeoffCellValue>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["PanelType"] = TakeoffCellValue.FromString("SmokePanel"),
                                ["PanelCount"] = TakeoffCellValue.FromNumber(2d),
                                ["FormulaText"] = TakeoffCellValue.FromString("=not-a-formula")
                            }
                        }
                    }
                }
            }
        };

        string csvPath = Path.Combine(outputRoot, "takeoff-spreadsheet-writer-smoke.csv");
        string xlsxPath = Path.Combine(outputRoot, "takeoff-spreadsheet-writer-smoke.xlsx");
        if (System.IO.File.Exists(csvPath))
        {
            System.IO.File.Delete(csvPath);
        }

        if (System.IO.File.Exists(xlsxPath))
        {
            System.IO.File.Delete(xlsxPath);
        }

        var csvWriter = new CsvSpreadsheetWorkbookWriter();
        TakeoffSpreadsheetWriteResult csv = RequireTakeoffSpreadsheetSuccess(
            csvWriter.Write(workbook, TakeoffSpreadsheetFormat.Csv, csvPath, overwriteExisting: true),
            "Write takeoff CSV smoke");
        RequireTakeoffSpreadsheetOutput(csv.OutputPath, "CSV writer smoke should produce a non-empty file.");
        RequireTakeoffSpreadsheet(csv.Warnings.Any(warning => string.Equals(warning.Code, "TAKEOFF_FORMULA_TEXT_ESCAPED", StringComparison.Ordinal)), "CSV writer should escape formula-like text.");

        var xlsxWriter = new XlsxSpreadsheetWorkbookWriter();
        TakeoffSpreadsheetWriteResult xlsx = RequireTakeoffSpreadsheetSuccess(
            xlsxWriter.Write(workbook, TakeoffSpreadsheetFormat.Xlsx, xlsxPath, overwriteExisting: true),
            "Write takeoff XLSX smoke");
        RequireTakeoffSpreadsheetOutput(xlsx.OutputPath, "XLSX writer smoke should produce a non-empty file.");
        RequireTakeoffSpreadsheet(xlsx.Warnings.Any(warning => string.Equals(warning.Code, "TAKEOFF_FORMULA_TEXT_ESCAPED", StringComparison.Ordinal)), "XLSX writer should escape formula-like text.");
    }

    private static void CleanupTakeoffSpreadsheetOutput(string outputRoot)
    {
        foreach (string filePath in Directory.EnumerateFiles(outputRoot, "takeoff-spreadsheet*.*"))
        {
            System.IO.File.Delete(filePath);
        }
    }

    private static void RequireTakeoffSpreadsheetOutput(string outputPath, string message)
    {
        RequireTakeoffSpreadsheet(System.IO.File.Exists(outputPath), message);
        RequireTakeoffSpreadsheet(new FileInfo(outputPath).Length > 0L, message);
    }

    private static T RequireTakeoffSpreadsheetSuccess<T>(OperationResponse<T> response, string label)
        where T : class
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{label} failed: {response.Message}");
        }

        return response.Data;
    }

    private static void RequireTakeoffSpreadsheetFailure<T>(
        OperationResponse<T> response,
        string expectedMessageFragment,
        string message)
    {
        if (response.Success || !response.Message.Contains(expectedMessageFragment, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{message} Actual: Success={response.Success}, Message={response.Message}");
        }
    }

    private static void RequireTakeoffSpreadsheet(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
