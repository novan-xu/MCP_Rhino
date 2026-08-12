using System.Reflection;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingMatchSmoke;

internal static class Program
{
    private static readonly Guid SourceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TargetOneId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TargetTwoId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static void Main()
    {
        var keys = new PanelCladdingKeyService();
        var planner = new PanelCladdingMatchPlanningService(keys);
        PanelCladdingMatchPanelSnapshot source = BuildConfiguredSource(SourceId, PlanarGeometry());
        PanelCladdingMatchPanelSnapshot targetOne = Snapshot(
            TargetOneId,
            PlanarGeometry(),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["CW_1.01_PID"] = "TARGET-01",
                ["CW_1.05_RELEASE"] = "R8",
                ["CW_1.07_WALL_TYPE"] = "WT-01",
                ["CW_1.02_CID"] = "TARGET-01-PANEL",
                ["Plane"] = "target-frame",
                ["CustomNote"] = "preserve me",
                ["CW_2.03_OFFSET_H0"] = "25",
                ["CW_2.04_OFFSET_V0"] = "20",
                ["CW_2.04_OFFSET_V1"] = "65",
                [PanelCladdingKeyService.GetCellKey(0, "A")] = " ",
                [PanelCladdingKeyService.TypeCodeKey] = string.Empty,
                [PanelCladdingKeyService.SignatureKey] = " "
            });
        PanelCladdingMatchPanelSnapshot targetTwo = Snapshot(
            TargetTwoId,
            PlanarGeometry(),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["CW_1.01_PID"] = "TARGET-02",
                ["Unrelated"] = "keep",
                ["CW_2.03_OFFSET_H0"] = "55",
                ["CW_2.04_OFFSET_V0"] = "35",
                ["CW_2.04_OFFSET_V1"] = "85"
            });

        PanelCladdingMatchPlan plan = RequireData(
            planner.CreatePlan(source, new[] { targetOne, targetTwo, targetOne }),
            "Create multi-target match plan");
        Require(plan.SourceObjectId == SourceId, "Source object id was not preserved.");
        Require(plan.Targets.Select(target => target.ObjectId).SequenceEqual(new[] { TargetOneId, TargetTwoId }),
            "Targets must remain ordered and duplicate ids must collapse.");
        VerifyConfigurationWrites(plan.Targets[0].UserTextWrites);
        Require(!plan.Targets[0].UserTextDeletes.Any(key => key.Contains("OFFSET", StringComparison.OrdinalIgnoreCase)) &&
                !plan.Targets[0].UserTextWrites.Keys.Any(key => key.Contains("OFFSET", StringComparison.OrdinalIgnoreCase)),
            "Match must neither delete nor write target offset keys.");
        Require(plan.Targets[0].UserTextDeletes.Contains(PanelCladdingKeyService.GetCellKey(0, "A"), StringComparer.OrdinalIgnoreCase),
            "Existing blank cladding keys must be deleted before replacement.");
        Require(plan.Targets[0].UserTextDeletes.Contains(PanelCladdingKeyService.LegacyTypeCodeKey, StringComparer.OrdinalIgnoreCase) &&
                plan.Targets[0].UserTextDeletes.Contains(PanelCladdingKeyService.LegacySignatureKey, StringComparer.OrdinalIgnoreCase),
            "Legacy type/signature keys must be included in cleanup.");
        foreach (string preserved in new[]
        {
            "CW_1.01_PID", "CW_1.05_RELEASE", "CW_1.07_WALL_TYPE", "CW_1.02_CID", "Plane", "CustomNote",
            "CW_2.03_OFFSET_H0", "CW_2.04_OFFSET_V0", "CW_2.04_OFFSET_V1"
        })
        {
            Require(!plan.Targets[0].UserTextDeletes.Contains(preserved, StringComparer.OrdinalIgnoreCase),
                $"Identity/unrelated key {preserved} must not be deleted.");
            Require(!plan.Targets[0].UserTextWrites.ContainsKey(preserved),
                $"Identity/unrelated key {preserved} must not be overwritten.");
        }
        Console.WriteLine("[OK] multi-target cladding transfer, offset preservation, stale cladding cleanup, and identity preservation");

        PanelCladdingMatchPanelSnapshot oneCellSource = Snapshot(
            SourceId,
            PlanarGeometry(),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [PanelCladdingKeyService.TypeCodeKey] = "WT01-CL-1X1-ABCDEF12",
                [PanelCladdingKeyService.SignatureKey] = "v2:sha256:onecell",
                [PanelCladdingKeyService.GetCellKey(0, "A")] = "gl01"
            });
        PanelCladdingMatchPanelSnapshot oneCellTarget = Snapshot(
            TargetTwoId,
            PlanarGeometry(),
            new Dictionary<string, string> { ["CW_1.01_PID"] = "TARGET-ONE-CELL" });
        PanelCladdingMatchPlan oneCellPlan = RequireData(
            planner.CreatePlan(oneCellSource, new[] { oneCellTarget }),
            "Create one-cell match plan");
        Require(!oneCellPlan.Targets[0].UserTextWrites.Keys.Any(key =>
                key.Contains("OFFSET", StringComparison.OrdinalIgnoreCase)),
            "A one-cell source must not invent divider offsets.");
        Require(oneCellPlan.Targets[0].UserTextWrites[PanelCladdingKeyService.GetCellKey(0, "A")] == "GL01",
            "One-cell material normalization failed.");
        Console.WriteLine("[OK] one-cell configuration without divider offsets");

        VerifyEligibilityFailures(planner, source, targetOne, targetTwo);
        Console.WriteLine("[OK] configured-target, source, overlap, and unsupported-target failures are fail-closed");

        VerifyTargetOwnedOffsetsAndTopology(planner);
        Console.WriteLine("[OK] target-owned offsets may differ while logical cladding topology remains enforced");

        VerifyAssemblyContract();
        Console.WriteLine("[OK] standalone PanelCladdingMatch command/service contract and unique GUID");
    }

    private static void VerifyConfigurationWrites(IReadOnlyDictionary<string, string> writes)
    {
        Require(!writes.Keys.Any(key => key.Contains("OFFSET", StringComparison.OrdinalIgnoreCase)),
            "Cladding configuration writes must not contain offsets.");
        string[] expectedMaterials = { "GL01", "GL02", "STN01", "STN02", "TER01", "TER02" };
        int materialIndex = 0;
        for (int column = 0; column < 3; column++)
        {
            for (int row = 0; row < 2; row++)
            {
                string key = PanelCladdingKeyService.GetCellKey(column, PanelCladdingKeyService.GetRowLabel(row));
                Require(writes[key] == expectedMaterials[materialIndex++], $"Material write mismatch for {key}.");
            }
        }
        Require(writes[PanelCladdingKeyService.TypeCodeKey] == "WT01-CL-3X2-ABCDEF12",
            "Canonical type code was not copied.");
        Require(writes[PanelCladdingKeyService.SignatureKey] == "v2:sha256:configured",
            "Canonical Signature was not copied.");
        Require(!writes.ContainsKey(PanelCladdingKeyService.LegacyTypeCodeKey) &&
                !writes.ContainsKey(PanelCladdingKeyService.LegacySignatureKey),
            "Legacy type/signature keys must not be written.");
    }

    private static void VerifyEligibilityFailures(
        PanelCladdingMatchPlanningService planner,
        PanelCladdingMatchPanelSnapshot source,
        PanelCladdingMatchPanelSnapshot targetOne,
        PanelCladdingMatchPanelSnapshot emptyTarget)
    {
        PanelCladdingMatchPanelSnapshot configuredTarget = Snapshot(
            TargetOneId,
            PlanarGeometry(),
            new Dictionary<string, string>
            {
                [PanelCladdingKeyService.GetCellKey(0, "A")] = "GL01"
            });
        RequireFailure(
            planner.CreatePlan(source, new[] { configuredTarget }),
            "TARGET_ALREADY_CONFIGURED");

        PanelCladdingMatchPanelSnapshot legacyConfiguredTarget = Snapshot(
            TargetOneId,
            PlanarGeometry(),
            new Dictionary<string, string>
            {
                [PanelCladdingKeyService.LegacyTypeCodeKey] = "LEGACY"
            });
        RequireFailure(
            planner.CreatePlan(source, new[] { legacyConfiguredTarget }),
            "TARGET_ALREADY_CONFIGURED");

        var missingSignature = new Dictionary<string, string>(source.UserText, StringComparer.OrdinalIgnoreCase);
        missingSignature.Remove(PanelCladdingKeyService.SignatureKey);
        RequireFailure(
            planner.CreatePlan(Snapshot(SourceId, PlanarGeometry(), missingSignature), new[] { emptyTarget }),
            "SOURCE_NOT_CONFIGURED");

        RequireFailure(
            planner.CreatePlan(source, new[] { Snapshot(SourceId, PlanarGeometry(), targetOne.UserText) }),
            "SOURCE_IS_TARGET");

        RequireFailure(
            planner.CreatePlan(
                source,
                new[] { Snapshot(TargetOneId, UnsupportedGeometry(), targetOne.UserText) }),
            "GEOMETRY_MISMATCH");
    }

    private static void VerifyTargetOwnedOffsetsAndTopology(PanelCladdingMatchPlanningService planner)
    {
        double[] sourceDepths = Enumerable.Range(0, 25).Select(index => index * 0.1d).ToArray();
        PanelCladdingMatchPanelSnapshot planarSource = BuildConfiguredSource(SourceId, PlanarGeometry());
        RequireData(
            planner.CreatePlan(
                planarSource,
                new[] { Snapshot(TargetOneId, PlanarGeometry(width: 140d, height: 80d), TargetGrid("20", "30", "100")) }),
            "Different-sized planar target plan");
        RequireData(
            planner.CreatePlan(
                planarSource,
                new[] { Snapshot(TargetOneId, CurvedGeometry(sourceDepths, width: 125d, height: 85d), TargetGrid("45", "25", "90")) }),
            "Different-profile curved target plan");

        PanelCladdingMatchPanelSnapshot curvedSource = BuildConfiguredSource(
            SourceId,
            CurvedGeometry(sourceDepths));
        RequireData(
            planner.CreatePlan(
                curvedSource,
                new[] { Snapshot(TargetOneId, PlanarGeometry(width: 50d, height: 30d), TargetGrid("10", "10", "35")) }),
            "Curved-source to planar-target plan");

        RequireFailure(
            planner.CreatePlan(
                planarSource,
                new[] { Snapshot(TargetOneId, PlanarGeometry(width: 70d, height: 100d), new Dictionary<string, string>()) }),
            "GEOMETRY_MISMATCH");
        RequireFailure(
            planner.CreatePlan(
                planarSource,
                new[] { Snapshot(TargetTwoId, PlanarGeometry(), TargetGrid("40", "30", "120")) }),
            "GEOMETRY_MISMATCH");
    }

    private static void VerifyAssemblyContract()
    {
        Assembly assembly = typeof(PanelCladdingMatchPlanningService).Assembly;
        Type matchCommand = RequireType(assembly, "PanelCladdingEditor.UI.PanelCladdingMatchCommand");
        Type editorCommand = RequireType(assembly, "PanelCladdingEditor.UI.PanelCladdingEditorCommand");
        Type spawnCommand = RequireType(assembly, "PanelCladdingEditor.UI.PanelCladdingSpawnCommand");
        Type smokeCommand = RequireType(assembly, "PanelCladdingEditor.UI.PanelCladdingEditorSmokeCommand");
        Require(matchCommand.GUID != Guid.Empty, "Match command GUID must be explicit and non-empty.");
        Require(new[] { matchCommand.GUID, editorCommand.GUID, spawnCommand.GUID, smokeCommand.GUID }
                .Distinct().Count() == 4,
            "All PanelCladdingEditor Rhino command GUIDs must be unique.");
        Require(assembly.GetType(
                "PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding.LivePanelCladdingMatchService") is not null,
            "Live match adapter is missing.");

        MethodInfo match = typeof(ILivePanelCladdingMatchService)
            .GetMethod(nameof(ILivePanelCladdingMatchService.Match)) ??
            throw new InvalidOperationException("Live match contract is missing.");
        ParameterInfo[] parameters = match.GetParameters();
        Require(parameters.Length == 3 &&
                parameters[1].ParameterType == typeof(Guid) &&
                parameters[2].ParameterType == typeof(IReadOnlyList<Guid>),
            "Live match contract must accept one source id and multiple target ids.");

        string[] forbidden = { "MCP_Rhino", "ModelContextProtocol", "Microsoft.Extensions.Hosting" };
        Require(!assembly.GetReferencedAssemblies().Any(reference => forbidden.Any(token =>
                (reference.Name ?? string.Empty).Contains(token, StringComparison.OrdinalIgnoreCase))),
            "Match capability introduced an MCP_Rhino or MCP SDK dependency.");
    }

    private static PanelCladdingMatchPanelSnapshot BuildConfiguredSource(
        Guid objectId,
        PanelCladdingMatchGeometryDescriptor geometry)
    {
        var userText = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CW_2.03_OFFSET_H0"] = "40",
            ["CW_2.04_OFFSET_V0"] = "30",
            ["CW_2.04_OFFSET_V1"] = "70",
            [PanelCladdingKeyService.TypeCodeKey] = "WT01-CL-3X2-ABCDEF12",
            [PanelCladdingKeyService.SignatureKey] = "v2:sha256:configured",
            ["CW_1.01_PID"] = "SOURCE"
        };
        string[] materials = { "gl01", "gl02", "stn01", "stn02", "ter01", "ter02" };
        int materialIndex = 0;
        for (int column = 0; column < 3; column++)
        {
            for (int row = 0; row < 2; row++)
            {
                userText[PanelCladdingKeyService.GetCellKey(column, PanelCladdingKeyService.GetRowLabel(row))] =
                    materials[materialIndex++];
            }
        }
        return Snapshot(objectId, geometry, userText);
    }

    private static IReadOnlyDictionary<string, string> TargetGrid(
        string horizontal,
        string verticalZero,
        string verticalOne)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CW_2.03_OFFSET_H0"] = horizontal,
            ["CW_2.04_OFFSET_V0"] = verticalZero,
            ["CW_2.04_OFFSET_V1"] = verticalOne
        };
    }

    private static PanelCladdingMatchPanelSnapshot Snapshot(
        Guid objectId,
        PanelCladdingMatchGeometryDescriptor geometry,
        IReadOnlyDictionary<string, string> userText)
    {
        return new PanelCladdingMatchPanelSnapshot
        {
            ObjectId = objectId,
            Geometry = geometry,
            UserText = new Dictionary<string, string>(userText, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static PanelCladdingMatchGeometryDescriptor PlanarGeometry(
        double width = 100d,
        double height = 100d)
    {
        return new PanelCladdingMatchGeometryDescriptor
        {
            GeometryClass = PanelGeometryClass.Planar,
            Width = width,
            Height = height,
            ModelTolerance = 0.001d,
            DepthSamples = Enumerable.Repeat(0d, 25).ToArray()
        };
    }

    private static PanelCladdingMatchGeometryDescriptor CurvedGeometry(
        IReadOnlyList<double> depths,
        double width = 100d,
        double height = 100d)
    {
        return new PanelCladdingMatchGeometryDescriptor
        {
            GeometryClass = PanelGeometryClass.Curved,
            Width = width,
            Height = height,
            ModelTolerance = 0.001d,
            DepthSamples = depths
        };
    }

    private static PanelCladdingMatchGeometryDescriptor UnsupportedGeometry()
    {
        return new PanelCladdingMatchGeometryDescriptor
        {
            GeometryClass = PanelGeometryClass.UnsupportedProjection,
            Width = 100d,
            Height = 100d,
            ModelTolerance = 0.001d
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
