using System.Reflection;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using Rhino.Commands;

namespace PanelCladdingSplitSpawnSyncSmoke;

internal static class Program
{
    private static void Main()
    {
        Assembly assembly = typeof(PanelCladdingKeyService).Assembly;
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PanelCladdingSpawnSrfCommand"] = "PCSpawnSrf",
            ["PanelCladdingSpawnCrvCommand"] = "PCSpawnCrv",
            ["PanelCladdingSyncSrfCommand"] = "PCSyncSrf",
            ["PanelCladdingSyncCrvCommand"] = "PCSyncCrv"
        };
        foreach ((string typeName, string commandName) in expected)
        {
            Type type = assembly.GetType($"PanelCladdingEditor.UI.{typeName}") ??
                throw new InvalidOperationException($"Missing command type {typeName}.");
            var command = (Command)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
            Require(command.EnglishName == commandName, $"{typeName} registered {command.EnglishName}.");
        }
        Require(assembly.GetType("PanelCladdingEditor.UI.PanelCladdingSpawnCommand") is null &&
                assembly.GetType("PanelCladdingEditor.UI.PanelCladdingSyncCommand") is null,
            "A combined spawn or sync command remains compiled.");

        var keys = new PanelCladdingKeyService();
        PanelCladdingKeySet blankGrid = Required(keys.CreateKeySet(
            [30d], [40d], new Dictionary<string, string>(), 100d, 80d, 0.001d));
        IReadOnlyList<PanelCladdingExtrusionCurvePlan> curves = Required(
            new PanelCladdingExtrusionPlanningService().CreatePlan(
                "PID_SAMPLE", "CID_SAMPLE", 100d, 80d, blankGrid,
                "01_CW Panels::Surfaces-PNL::WT-01"));
        Require(curves.Count > 4 && curves.All(curve =>
                curve.LayerPath == "02_CW Extrusions::Curves-PNL::WT-01"),
            "Curve planning did not work independently of cladding assignments.");

        Require(Enum.GetValues<PanelCladdingObjectScope>().SequenceEqual(
                [PanelCladdingObjectScope.Surfaces, PanelCladdingObjectScope.Curves]),
            "Object-family scope contract changed unexpectedly.");
        Console.WriteLine("[OK] legacy combined spawn/sync commands are absent and four scoped commands are registered.");
        Console.WriteLine("[OK] curve spawn planning succeeds with blank cladding assignments.");
        Console.WriteLine("[OK] surface/curve scope is explicit in the shared application contract.");
    }

    private static T Required<T>(PanelCladdingEditor.Contracts.Responses.OperationResponse<T> response)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException(response.Message);
        }
        return response.Data;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
