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
        VerifyCidSelection();
        VerifySharedPidDependencyOwnership();
        VerifyCommandContract();
        VerifyLiveOwnershipAndMutationContract();
        VerifyPackageContract();
    }

    private static PanelCladdingUpdateSource Source(string pid, string role = "", string storedCid = "")
    {
        var text = new Dictionary<string, string>
        {
            [PanelCladdingSpawnPlanningService.PanelIdUserTextKey] = pid,
            [PanelCladdingSpawnPlanningService.CidUserTextKey] = storedCid
        };
        if (role.Length > 0) text["CW_1.06_UNIT_TYPE"] = role;
        text["parent"] = "1";
        text["child"] = "1";
        return new PanelCladdingUpdateSource(Guid.NewGuid(), pid,
            PanelCladdingCidService.ResolvePanelCid(pid, text));
    }

    private static void VerifyCidSelection()
    {
        var parent = Source("PID_A", "corner_parent", "CID_STALE");
        var child = Source("PID_A", "corner_child", "CID_STALE");
        var pair = PanelCladdingUpdateSelectionService.CreatePlan([parent, child, parent]);
        Require(parent.PanelCid == "CID_A-P" && child.PanelCid == "CID_A-C" &&
                pair.ProcessableSources.Count == 2 && pair.DuplicateCidGroups.Count == 0,
            "Role correction must split stale equal CIDs before collision detection; repeated object IDs are not duplicates.");

        var duplicateParent = Source("PID_A", "CORNER_PARENT");
        var independent = Source("PID_B");
        var mixed = PanelCladdingUpdateSelectionService.CreatePlan([parent, child, duplicateParent, independent]);
        Require(mixed.ProcessableSources.Select(source => source.ObjectId).ToHashSet()
                .SetEquals([child.ObjectId, independent.ObjectId]) &&
                mixed.DuplicateCidGroups.Single().PanelObjectIds.ToHashSet()
                .SetEquals([parent.ObjectId, duplicateParent.ObjectId]),
            "All duplicated parents must be skipped while the same-PID child and unrelated panel proceed.");

        var crossPid = PanelCladdingUpdateSelectionService.CreatePlan([
            Source("PID_A", storedCid: " CID_CUSTOM "), Source("PID_B", storedCid: "cid_custom")]);
        Require(crossPid.ProcessableSources.Count == 0 && crossPid.DuplicateCidGroups.Single().PanelObjectIds.Count == 2,
            "CID duplicates must compare case-insensitively across PIDs, ignoring surrounding whitespace.");
        var allDuplicate = PanelCladdingUpdateSelectionService.CreatePlan([parent, duplicateParent]);
        var emptyPlan = Required(new PanelCladdingDependencyReconciliationService().CreatePlan([], []), "empty update");
        Require(allDuplicate.ProcessableSources.Count == 0 && allDuplicate.DuplicateCidGroups.Count == 1 &&
                emptyPlan.Creates.Count + emptyPlan.Updates.Count + emptyPlan.Deletes.Count == 0,
            "An all-duplicate selection must permit an empty geometry plan.");
        Require(PanelCladdingUpdateSelectionService.CreatePlan([Source("PID_NO_CID")])
                .ProcessableSources.Single().PanelCid == "CID_NO_CID",
            "Missing unit types must retain the existing PID-derived identity fallback despite old flags.");
        var flat = Source("PID_A", " flat ", "CID_A-P");
        var typed = PanelCladdingUpdateSelectionService.CreatePlan([parent, child, flat]);
        Require(flat.PanelCid == "CID_A" && typed.ProcessableSources.Count == 3 && typed.DuplicateCidGroups.Count == 0,
            "Explicit flat must clear a stale role CID before duplicate detection.");
        Console.WriteLine("[OK] normalized parent/child CIDs, mixed/all duplicates, cross-PID collisions, case/whitespace, missing CID");
    }

    private static void VerifySharedPidDependencyOwnership()
    {
        var parent = Source("PID_A", "corner_parent");
        var child = Source("PID_A", "corner_child");
        foreach (string code in new[] { "0A", "INT_B1", "FRM_0", "OBSOLETE" })
        {
            Require(PanelCladdingUpdateSelectionService.FindDependencyOwners([parent, child], "pid_a", "cid_a-p-" + code)
                    .Single().ObjectId == parent.ObjectId,
                "Parent dependency must have only the parent owner.");
            Require(PanelCladdingUpdateSelectionService.FindDependencyOwners([parent, child], "PID_A", "CID_A-C-" + code)
                    .Single().ObjectId == child.ObjectId,
                "Child dependency must have only the child owner.");
        }
        var duplicateParent = Source("PID_A", "corner_parent");
        var sources = new[] { parent, child, duplicateParent };
        Require(PanelCladdingUpdateSelectionService.FindDependencyOwners(sources, "PID_A", "CID_A-P-0A").Count == 2,
            "Dependencies of skipped duplicate parents must remain ambiguous, never assigned to the unique child.");
        Require(PanelCladdingUpdateSelectionService.FindDependencyOwners(sources, "PID_A", "CID_A-0A").Count == 3,
            "Legacy unsuffixed dependencies must be preserved when selected ownership is ambiguous.");
        Require(PanelCladdingUpdateSelectionService.FindDependencyOwners([parent], "PID_A", "CID_A-0A").Count == 1,
            "Single-source legacy migration must remain supported.");
        Require(PanelCladdingUpdateSelectionService.FindDependencyOwners([parent], "PID_A", "CID_A-C-0A").Count == 0 &&
                PanelCladdingUpdateSelectionService.FindDependencyOwners(sources, "PID_OTHER", "CID_OTHER-0A").Count == 0,
            "Unselected opposite-role and unrelated PID dependencies must be excluded.");

        var parentDependency = Expected("PID_A", "CID_A-P-0A", PanelCladdingDependencyKind.Surface);
        var childDependency = new PanelCladdingExpectedDependency
        {
            SourcePanelObjectId = child.ObjectId, PanelId = "PID_A", Cid = "CID_A-C-0A",
            Kind = PanelCladdingDependencyKind.Surface
        };
        var plan = Required(new PanelCladdingDependencyReconciliationService().CreatePlan(
            [parentDependency, childDependency],
            [Existing(SurfaceKeep, "PID_A", "CID_A-P-0A", PanelCladdingDependencyKind.Surface),
             Existing(SurfaceDuplicate, "PID_A", "CID_A-C-0A", PanelCladdingDependencyKind.Surface)]), "shared PID roles");
        Require(plan.Updates.Count == 2 && plan.Creates.Count == 0 && plan.Deletes.Count == 0,
            "Parent and child dependencies with the same PID must both be retained.");
        Console.WriteLine("[OK] same-PID role ownership, skipped-panel isolation, legacy ambiguity, and shared-PID reconciliation");
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
            "PanelCladdingUpdateSelectionService.CreatePlan(sources.Data)",
            "PanelCladdingUpdateSelectionService.FindDependencyOwners(candidates, panelId, cid)",
            "owners.Count != 1 || !processableIds.Contains(owners[0].ObjectId)",
            "sourceIds.Contains(rhinoObject.Id)",
            "selection.ProcessableSources,",
            "document.Objects.UnselectAll()",
            "document.Objects.FindId(id)?.Select(true)",
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
        Require(!source.Contains("DUPLICATE_SELECTED_PID", StringComparison.Ordinal),
            "PCUpdate must not reject duplicate PIDs.");
        int normalization = source.IndexOf("LivePanelCladdingCidService.Apply(document, panelCidChanges.Data)", StringComparison.Ordinal);
        int dependencyUpdate = source.IndexOf("? UpdateDependencies(document, filePath, sourcePanelIds", StringComparison.Ordinal);
        Require(normalization >= 0 && dependencyUpdate > normalization,
            "CID writes must precede duplicate classification and dependency planning in the live update flow.");
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
