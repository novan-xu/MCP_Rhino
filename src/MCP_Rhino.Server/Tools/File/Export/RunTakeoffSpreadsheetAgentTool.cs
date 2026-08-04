using System.ComponentModel;
using MCP_Rhino.Server.Agents.Takeoff;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.File.Export;

[McpServerToolType]
public sealed class RunTakeoffSpreadsheetAgentTool
{
    private readonly TakeoffSpreadsheetAgent _agent;

    public RunTakeoffSpreadsheetAgentTool(TakeoffSpreadsheetAgent agent)
    {
        _agent = agent;
    }

    [McpServerTool(ReadOnly = false, Destructive = true, OpenWorld = true)]
    [Description("Run the flexible TakeoffSpreadsheetAgent for user-defined Rhino quantity take-offs and spreadsheet export. The agent can inspect live attributes, ask clarification questions for uncertain mappings or missing output location/name, preview a structured spec, or export only when a safe explicit spec and CSV/XLSX destination are supplied.")]
    public OperationResponse<TakeoffSpreadsheetAgentResponse> RunTakeoffSpreadsheetAgent(
        string filePath,
        string userRequest,
        TakeoffScopeRequest? scopeHints = null,
        List<TakeoffConceptMappingRequest>? knownMappings = null,
        string outputDirectory = "",
        string outputFileName = "",
        bool overwriteExisting = false,
        TakeoffAgentMode mode = TakeoffAgentMode.Clarify,
        TakeoffScheduleSpecRequest? spec = null)
    {
        return _agent.Run(new TakeoffSpreadsheetAgentRequest
        {
            FilePath = filePath,
            UserRequest = userRequest,
            ScopeHints = scopeHints,
            KnownMappings = knownMappings ?? new List<TakeoffConceptMappingRequest>(),
            OutputDirectory = outputDirectory,
            OutputFileName = outputFileName,
            OverwriteExisting = overwriteExisting,
            Mode = mode,
            Spec = spec
        });
    }
}
