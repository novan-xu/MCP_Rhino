using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using Rhino.Commands;

namespace PanelCladdingUpdateCommandSmoke;

internal static class Program
{
    private static readonly Guid PanelId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid SurfaceKeep = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid SurfaceDuplicate = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid SurfaceStale = Guid.Parse("20000000-0000-0000-0000-000000000003");
    private static readonly Guid CurveKeep = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid WrongKind = Guid.Parse("40000000-0000-0000-0000-000000000001");

    private static void Main()
    {
        VerifyReconciliation();
        VerifyDuplicateExpectedRejection();
        VerifyEmptySurfaceReconciliation();
        VerifyCommandContract();
        VerifyLiveOwnershipAndMutationContract();
        VerifyPackageContract();
    }

    private static void VerifyReconciliation()
    {
        PanelCladdingExpectedDependency surfaceA = Expected(
            "PID_A",
            "CID_A-0A",
            PanelCladdingDependencyKind.Surface);
        PanelCladdingExpectedDependency surfaceB = Expected(
            "PID_A",
            "CID_A-1A",
            PanelCladdingDependencyKind.Surface);
        PanelCladdingExpectedDependency curve = Expected(
            "PID_A",
            "CID_A-FRM_0",
            PanelCladdingDependencyKind.Curve);
        PanelCladdingDependencyReconciliationPlan plan = Required(
            new PanelCladdingDependencyReconciliationService().CreatePlan(
                [surfaceA, surfaceB, curve],
                [
                    Existing(SurfaceKeep, "PID_A", "CID_A-0A", PanelCladdingDependencyKind.Surface),
                    Existing(SurfaceDuplicate, "PID_A", "CID_A-0A", PanelCladdingDependencyKind.Surface),
                    Existing(SurfaceStale, "PID_A", "CID_A-OLD", PanelCladdingDependencyKind.Surface),
                    Existing(CurveKeep, "PID_A", "CID_A-FRM_0", PanelCladdingDependencyKind.Curve),
                    Existing(WrongKind, "PID_A", "CID_A-FRM_0", PanelCladdingDependencyKind.Surface)
                ]),
            "create dependency reconciliation");

        Require(plan.Creates.Count == 1 && ReferenceEquals(plan.Creates[0], surfaceB),
            "The missing surface was not the only create action.");
        Require(plan.Updates.Select(action => action.ObjectId).ToHashSet().SetEquals(
                [SurfaceKeep, CurveKeep]),
            "Existing canonical surface/curve objects were not retained for replacement.");
        Require(plan.Deletes.ToHashSet().SetEquals([SurfaceDuplicate, SurfaceStale, WrongKind]),
            "Duplicate, obsolete, or wrong-kind managed objects were not scheduled for deletion.");
        Console.WriteLine("[OK] reconciliation creates missing, updates retained, and deletes stale/duplicate CIDs");
    }

    private static void VerifyDuplicateExpectedRejection()
    {
        PanelCladdingExpectedDependency expected = Expected(
            "PID_A",
            "CID_A-0A",
            PanelCladdingDependencyKind.Surface);
        var duplicate = new PanelCladdingExpectedDependency
        {
            SourcePanelObjectId = Guid.Parse("10000000-0000-0000-0000-000000000002"),
            PanelId = "pid_a",
            Cid = "cid_a-0a",
            Kind = PanelCladdingDependencyKind.Surface
        };
        var result = new PanelCladdingDependencyReconciliationService().CreatePlan(
            [expected, duplicate],
            []);
        Require(!result.Success && result.Message.Contains(
                "PANEL_CLADDING_UPDATE_DUPLICATE_EXPECTED_CID",
                StringComparison.Ordinal),
            "Duplicate expected identities must fail before mutation.");
        Console.WriteLine("[OK] ambiguous expected CID ownership fails before mutation");
    }

    private static void VerifyEmptySurfaceReconciliation()
    {
        var keys = new PanelCladdingKeyService();
        var panelText = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingSpawnPlanningService.PanelIdUserTextKey] = "PID_EMPTY",
            [PanelCladdingSpawnPlanningService.ReleaseUserTextKey] = "R1",
            [PanelCladdingSpawnPlanningService.WallTypeUserTextKey] = "WT-00"
        };
        PanelCladdingKeySet keySet = Required(
            keys.CreateKeySet([], [], panelText, 100d, 80d, 0.001d),
            "create empty cladding grid");
        var planner = new PanelCladdingSpawnPlanningService(keys);
        var spawnPlan = planner.CreatePlan(panelText, keySet);
        Require(!spawnPlan.Success, "PCSpawnSrf must retain its no-populated-cells guard.");
        PanelCladdingSpawnPlan updatePlan = Required(
            planner.CreateUpdatePlan(panelText, keySet),
            "create empty update surface plan");
        Require(updatePlan.Regions.Count == 0,
            "A panel with blank cladding must produce an empty authoritative surface set.");
        Console.WriteLine("[OK] PCUpdate supports an empty expected surface set without weakening PCSpawnSrf");
    }

    private static void VerifyCommandContract()
    {
        Assembly assembly = typeof(PanelCladdingDependencyReconciliationService).Assembly;
        Type commandType = assembly.GetType("PanelCladdingEditor.UI.PanelCladdingUpdateCommand") ??
            throw new InvalidOperationException("PanelCladdingUpdateCommand is missing.");
        Require(typeof(Command).IsAssignableFrom(commandType), "PCUpdate is not a Rhino command.");
        Require(commandType.GUID != Guid.Empty, "PCUpdate command GUID must be explicit and non-empty.");
        object command = RuntimeHelpers.GetUninitializedObject(commandType);
        string englishName = (string?)commandType.GetProperty("EnglishName")?.GetValue(command) ?? string.Empty;
        Require(englishName == "PCUpdate", $"Unexpected command name: {englishName}");

        MethodInfo update = typeof(ILivePanelCladdingUpdateService).GetMethod(
            nameof(ILivePanelCladdingUpdateService.Update)) ??
            throw new InvalidOperationException("Live PCUpdate contract is missing.");
        ParameterInfo[] parameters = update.GetParameters();
        Require(parameters.Length == 2 &&
                parameters[0].ParameterType == typeof(string) &&
                parameters[1].ParameterType == typeof(IReadOnlyList<Guid>),
            "PCUpdate must expose one batch service call for selected panel IDs.");
        Console.WriteLine("[OK] PCUpdate command and batch service contract are registered");
    }

    private static void VerifyLiveOwnershipAndMutationContract()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "PanelCladdingEditor",
            "Infrastructure",
            "Rhino",
            "Live",
            "PanelCladding",
            "LivePanelCladdingUpdateService.cs"));
        string[] required =
        {
            "IsManagedSurfaceLayerPath(layerPath)",
            "IsManagedExtrusionLayerPath(layerPath)",
            "selectedPanels.TryGetValue(panelId, out PanelSource? source)",
            "PanelCladdingCidService.IncludesDependency(panelId, source.PanelCid, cid)",
            "cid.Length == 0",
            "BeginUndoRecord(\"Update Panel Cladding Dependencies\")",
            "dependency.Geometry,",
            "ignoreModes: true",
            "document.Objects.AddBrep(brep, attributes.Data)",
            "document.Objects.AddCurve(curve, attributes.Data)",
            "document.Objects.Delete("
        };
        foreach (string token in required)
        {
            Require(source.Contains(token, StringComparison.Ordinal),
                $"Live PCUpdate implementation is missing contract token: {token}");
        }
        Console.WriteLine("[OK] live mutation is PID/CID/root constrained and uses mode-aware replace/create/delete in one Undo record");
    }

    private static void VerifyPackageContract()
    {
        string repositoryRoot = FindRepositoryRoot();
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Packaging",
            "PanelCladdingEditor",
            "package-manifest.json")));
        Require(Version.TryParse(manifest.RootElement.GetProperty("version").GetString(), out Version? version) &&
                version >= new Version(1, 0, 73),
            "PCUpdate requires package version 1.0.73 or later.");
        string installer = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Packaging",
            "PanelCladdingEditor",
            "Install-PanelCladdingEditor.ps1"));
        Require(installer.Contains("'PCUpdate'", StringComparison.Ordinal),
            "The exact installed Rhino CommandList omits PCUpdate.");
        Console.WriteLine("[OK] supported package registers PCUpdate");
    }

    private static PanelCladdingExpectedDependency Expected(
        string panelId,
        string cid,
        PanelCladdingDependencyKind kind) => new()
        {
            SourcePanelObjectId = PanelId,
            PanelId = panelId,
            Cid = cid,
            Kind = kind
        };

    private static PanelCladdingExistingDependency Existing(
        Guid objectId,
        string panelId,
        string cid,
        PanelCladdingDependencyKind kind) => new()
        {
            ObjectId = objectId,
            PanelId = panelId,
            Cid = cid,
            Kind = kind
        };

    private static T Required<T>(PanelCladdingEditor.Contracts.Responses.OperationResponse<T> response, string operation)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{operation} failed: {response.Message}");
        }
        return response.Data;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
