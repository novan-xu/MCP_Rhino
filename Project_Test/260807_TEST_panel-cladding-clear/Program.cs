using System.Reflection;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingClearSmoke;

internal static class Program
{
    private static readonly Guid PanelOneId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PanelTwoId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static void Main()
    {
        var keys = new PanelCladdingKeyService();
        VerifyKeyClassifier(keys);
        Console.WriteLine("[OK] exact generic cell/type/signature key classification");

        var planner = new PanelCladdingClearPlanningService(keys);
        PanelCladdingClearPanelSnapshot panelOne = Snapshot(
            PanelOneId,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["CW_4.00_CLADDING_0A"] = "GL01",
                ["cw_12.34_cladding_27zz"] = string.Empty,
                [PanelCladdingKeyService.TypeCodeKey] = "WT01-P-2X1-ABCDEF12",
                [PanelCladdingKeyService.SignatureKey] = "v1:sha256:configured",
                [PanelCladdingKeyService.LegacyTypeCodeKey] = "LEGACY",
                [PanelCladdingKeyService.LegacySignatureKey] = "legacy-signature",
                ["CW_2.03_OFFSET_H0"] = "40",
                ["CW_2.04_OFFSET_V0"] = "30",
                ["CW_9.99_OFFSET_CUSTOM"] = "preserve",
                ["CW_1.01_PID"] = "PANEL-01",
                ["CW_1.05_LOT"] = "R2",
                ["CW_1.07_WALL_TYPE"] = "WT01",
                ["CW_1.02_CID"] = "PANEL-01-CID",
                ["CW_4.00_CLADDING_NOTE"] = "not a cell",
                ["Custom"] = "preserve"
            });
        PanelCladdingClearPanelSnapshot panelTwo = Snapshot(
            PanelTwoId,
            new Dictionary<string, string>
            {
                ["CW_2.03_OFFSET_H0"] = "25",
                ["CW_1.01_PID"] = "PANEL-02"
            });

        PanelCladdingClearPlan plan = RequireData(
            planner.CreatePlan(new[] { panelOne, panelTwo, panelOne }),
            "Create clear plan");
        Require(plan.Panels.Select(panel => panel.ObjectId).SequenceEqual(new[] { PanelOneId, PanelTwoId }),
            "Input order must be preserved and duplicate panel ids must collapse.");
        Require(plan.Panels[0].UserTextDeletes.Count == 6,
            "Configured panel should delete two cells, two type keys, and two signature keys.");
        Require(plan.Panels[1].UserTextDeletes.Count == 0,
            "Unconfigured panel should remain a no-op.");
        Require(plan.RemovedKeyCount == 6, "Removed key total is incorrect.");
        foreach (string expected in new[]
        {
            "CW_4.00_CLADDING_0A",
            "cw_12.34_cladding_27zz",
            PanelCladdingKeyService.TypeCodeKey,
            PanelCladdingKeyService.SignatureKey,
            PanelCladdingKeyService.LegacyTypeCodeKey,
            PanelCladdingKeyService.LegacySignatureKey
        })
        {
            Require(plan.Panels[0].UserTextDeletes.Contains(expected, StringComparer.OrdinalIgnoreCase),
                $"Expected clear key was omitted: {expected}");
        }
        foreach (string preserved in new[]
        {
            "CW_2.03_OFFSET_H0", "CW_2.04_OFFSET_V0", "CW_9.99_OFFSET_CUSTOM",
            "CW_1.01_PID", "CW_1.05_LOT", "CW_1.07_WALL_TYPE", "CW_1.02_CID",
            "CW_4.00_CLADDING_NOTE", "Custom"
        })
        {
            Require(!plan.Panels[0].UserTextDeletes.Contains(preserved, StringComparer.OrdinalIgnoreCase),
                $"Offset, identity, or unrelated key must be preserved: {preserved}");
        }
        Console.WriteLine("[OK] multi-panel clear plan, deduplication, no-op panel, and metadata preservation");

        RequireFailure(planner.CreatePlan(Array.Empty<PanelCladdingClearPanelSnapshot>()),
            "CLEAR_SELECTION_REQUIRED");
        RequireFailure(planner.CreatePlan(new[] { Snapshot(Guid.Empty, new Dictionary<string, string>()) }),
            "CLEAR_SELECTION_REQUIRED");
        Console.WriteLine("[OK] empty selection fails without a mutation plan");

        VerifyAssemblyContract();
        Console.WriteLine("[OK] standalone PanelCladdingClear command/service contract and unique GUID");
    }

    private static void VerifyKeyClassifier(PanelCladdingKeyService keys)
    {
        foreach (string clearable in new[]
        {
            "CW_4.00_CLADDING_0A", "cw_12.34_cladding_27zz",
            PanelCladdingKeyService.TypeCodeKey, PanelCladdingKeyService.SignatureKey,
            "cw_2.13_cladding_type", "CW_1.10_CLADDING_TYPE",
            PanelCladdingKeyService.LegacyTypeCodeKey, PanelCladdingKeyService.LegacySignatureKey
        })
        {
            Require(keys.IsClearableCladdingAssignmentKey(clearable),
                $"Expected clearable key was rejected: {clearable}");
        }
        foreach (string preserved in new[]
        {
            "", "CW_2.03_OFFSET_H0", "CW_4.0_CLADDING_0A", "CW_4.000_CLADDING_0A",
            "CW_4.00_CLADDING_A0", "CW_4.00_CLADDING_0", "CW_4.00_CLADDING_NOTE",
            "CW_1.01_PID", "CW_1.05_LOT", "CW_1.07_WALL_TYPE", "CW_1.02_CID",
            "CW_1.10_CAD_TYPE", "CW_2.14_CLADDING_TYPE_NOTE"
        })
        {
            Require(!keys.IsClearableCladdingAssignmentKey(preserved),
                $"Non-assignment key was classified as clearable: {preserved}");
        }
    }

    private static void VerifyAssemblyContract()
    {
        Assembly assembly = typeof(PanelCladdingClearPlanningService).Assembly;
        Type clearCommand = RequireType(assembly, "PanelCladdingEditor.UI.PanelCladdingClearCommand");
        Type editorCommand = RequireType(assembly, "PanelCladdingEditor.UI.PanelCladdingEditorCommand");
        Type spawnCommand = RequireType(assembly, "PanelCladdingEditor.UI.PanelCladdingSpawnSrfCommand");
        Type matchCommand = RequireType(assembly, "PanelCladdingEditor.UI.PanelCladdingMatchCommand");
        Require(clearCommand.GUID != Guid.Empty, "Clear command GUID must be explicit and non-empty.");
        Require(new[]
            {
                clearCommand.GUID, editorCommand.GUID, spawnCommand.GUID, matchCommand.GUID
            }.Distinct().Count() == 4,
            "All PanelCladdingEditor Rhino command GUIDs must be unique.");
        Require(assembly.GetType(
                "PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding.LivePanelCladdingClearService") is not null,
            "Live clear adapter is missing.");

        MethodInfo clear = typeof(ILivePanelCladdingClearService)
            .GetMethod(nameof(ILivePanelCladdingClearService.Clear)) ??
            throw new InvalidOperationException("Live clear contract is missing.");
        ParameterInfo[] parameters = clear.GetParameters();
        Require(parameters.Length == 2 &&
                parameters[0].ParameterType == typeof(string) &&
                parameters[1].ParameterType == typeof(IReadOnlyList<Guid>),
            "Live clear contract must accept a document path and multiple panel ids.");

        string[] forbidden = { "MCP_Rhino", "ModelContextProtocol", "Microsoft.Extensions.Hosting" };
        Require(!assembly.GetReferencedAssemblies().Any(reference => forbidden.Any(token =>
                (reference.Name ?? string.Empty).Contains(token, StringComparison.OrdinalIgnoreCase))),
            "Clear capability introduced an MCP_Rhino or MCP SDK dependency.");
    }

    private static PanelCladdingClearPanelSnapshot Snapshot(
        Guid objectId,
        IReadOnlyDictionary<string, string> userText)
    {
        return new PanelCladdingClearPanelSnapshot
        {
            ObjectId = objectId,
            UserText = new Dictionary<string, string>(userText, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static Type RequireType(Assembly assembly, string name)
    {
        return assembly.GetType(name) ?? throw new InvalidOperationException($"Type is missing: {name}");
    }

    private static T RequireData<T>(OperationResponse<T> response, string operation)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{operation} failed: {response.Message}");
        }
        return response.Data;
    }

    private static void RequireFailure<T>(OperationResponse<T> response, string token)
    {
        Require(!response.Success && response.Message.Contains(token, StringComparison.Ordinal),
            $"Expected failure containing {token}, got: {response.Message}");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
