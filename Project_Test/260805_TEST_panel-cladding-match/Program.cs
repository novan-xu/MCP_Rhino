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
        PanelCladdingTopologyPayloads targetTopology = RequireData(
            keys.EncodeTopology(new PanelCladdingTopologyState(), 1, 2),
            "Encode target topology");
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
                [PanelCladdingKeyService.SignatureKey] = " ",
                [PanelCladdingKeyService.SegmentMaskKey] = targetTopology.SegmentMask,
                [PanelCladdingKeyService.MergeMaskKey] = targetTopology.MergeMask
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
        Require(!plan.Targets[0].UserTextDeletes.Any(key =>
                PanelCladdingKeyService.IsTopologyKey(key) ||
                string.Equals(key, PanelCladdingKeyService.TypeCodeKey, StringComparison.OrdinalIgnoreCase)),
            "PCMatchSrf must not clean up target topology or type metadata.");
        Require(plan.Targets[0].UserTextDeletes.Contains(
                PanelCladdingKeyService.SignatureKey,
                StringComparer.OrdinalIgnoreCase),
            "PCMatchSrf must retire the persisted Signature key on a touched target.");
        foreach (string preserved in new[]
        {
            "CW_1.01_PID", "CW_1.05_RELEASE", "CW_1.07_WALL_TYPE", "CW_1.02_CID", "Plane", "CustomNote",
            "CW_2.03_OFFSET_H0", "CW_2.04_OFFSET_V0", "CW_2.04_OFFSET_V1",
            PanelCladdingKeyService.TypeCodeKey,
            PanelCladdingKeyService.SegmentMaskKey, PanelCladdingKeyService.MergeMaskKey
        })
        {
            Require(!plan.Targets[0].UserTextDeletes.Contains(preserved, StringComparer.OrdinalIgnoreCase),
                $"Identity/unrelated key {preserved} must not be deleted.");
            Require(!plan.Targets[0].UserTextWrites.ContainsKey(preserved),
                $"Identity/unrelated key {preserved} must not be overwritten.");
        }
        Console.WriteLine("[OK] multi-target transfer preserves offsets, masks, type metadata, and retires Signature");

        PanelCladdingMatchPanelSnapshot oneCellSource = Snapshot(
            SourceId,
            PlanarGeometry(),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [PanelCladdingKeyService.TypeCodeKey] = "WT01-1X1-ABCDEF12",
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
        Require(!oneCellPlan.Targets[0].UserTextWrites.Keys.Any(PanelCladdingKeyService.IsTopologyKey),
            "A one-cell source must not invent topology writes.");
        Console.WriteLine("[OK] one-cell configuration transfers only its logical cell");

        VerifyParentCellTransfer(planner, keys);
        Console.WriteLine("[OK] parent-cell assignments remain references instead of resolved materials");

        VerifyEligibilityFailures(planner, source, targetOne, targetTwo);
        Console.WriteLine("[OK] populated targets overwrite while source/target and unsupported failures remain fail-closed");

        VerifyTargetOwnedOffsetsAndTopology(planner);
        Console.WriteLine("[OK] target-owned offset distances may differ while logical grid dimensions remain enforced");

        VerifyAssemblyContract();
        Console.WriteLine("[OK] standalone PCMatchSrf command/service contract and unique GUID");
    }

    private static void VerifyConfigurationWrites(IReadOnlyDictionary<string, string> writes)
    {
        Require(!writes.Keys.Any(key => key.Contains("OFFSET", StringComparison.OrdinalIgnoreCase)),
            "Cladding configuration writes must not contain offsets.");
        var expectedMaterials = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetCellKey(0, "A")] = "GL01",
            [PanelCladdingKeyService.GetCellKey(1, "A")] = "STN01",
            [PanelCladdingKeyService.GetCellKey(1, "B")] = "STN02",
            [PanelCladdingKeyService.GetCellKey(2, "A")] = "TER01",
            [PanelCladdingKeyService.GetCellKey(2, "B")] = "TER02"
        };
        foreach ((string key, string expected) in expectedMaterials)
        {
            Require(writes[key] == expected, $"Material write mismatch for {key}.");
        }
        Require(writes[PanelCladdingKeyService.GetCellKey(0, "B")] == "0A",
            "A target-visible cell did not inherit the source region through a parent reference.");
        Require(writes.Keys.All(key => new PanelCladdingKeyService().IsCladdingCellKey(key) ||
                string.Equals(
                    key,
                    PanelCladdingKeyService.CladdingLogicKey,
                    StringComparison.OrdinalIgnoreCase)) &&
                writes.ContainsKey(PanelCladdingKeyService.CladdingLogicKey),
            "PCMatchSrf writes must be limited to cladding cells and their derived logic.");
    }

    private static void VerifyEligibilityFailures(
        PanelCladdingMatchPlanningService planner,
        PanelCladdingMatchPanelSnapshot source,
        PanelCladdingMatchPanelSnapshot targetOne,
        PanelCladdingMatchPanelSnapshot emptyTarget)
    {
        var configuredText = new Dictionary<string, string>(
            targetOne.UserText,
            StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetCellKey(0, "A")] = "GL01"
        };
        PanelCladdingMatchPanelSnapshot configuredTarget = Snapshot(
            TargetOneId,
            PlanarGeometry(),
            configuredText);
        PanelCladdingMatchTargetPlan overwrite = RequireData(
            planner.CreatePlan(source, new[] { configuredTarget }),
            "Configured target overwrite plan").Targets.Single();
        VerifyConfigurationWrites(overwrite.UserTextWrites);
        Require(overwrite.UserTextDeletes.Contains(
                PanelCladdingKeyService.GetCellKey(0, "A"),
                StringComparer.OrdinalIgnoreCase),
            "Configured target cladding keys must be replaced by the source graph.");

        PanelCladdingMatchPanelSnapshot metadataOnlyTarget = Snapshot(
            TargetOneId,
            PlanarGeometry(),
            new Dictionary<string, string>
            {
                [PanelCladdingKeyService.LegacyTypeCodeKey] = "LEGACY",
                [PanelCladdingKeyService.GetHorizontalOffsetKey(0)] = "60",
                [PanelCladdingKeyService.GetVerticalOffsetKey(0)] = "25",
                [PanelCladdingKeyService.GetVerticalOffsetKey(1)] = "75"
            });
        RequireData(
            planner.CreatePlan(source, new[] { metadataOnlyTarget }),
            "Metadata-only target plan");

        var missingSignature = new Dictionary<string, string>(source.UserText, StringComparer.OrdinalIgnoreCase);
        missingSignature.Remove(PanelCladdingKeyService.SignatureKey);
        RequireData(
            planner.CreatePlan(Snapshot(SourceId, PlanarGeometry(), missingSignature), new[] { emptyTarget }),
            "Source without signature plan");

        RequireFailure(
            planner.CreatePlan(source, new[] { Snapshot(SourceId, PlanarGeometry(), targetOne.UserText) }),
            "SOURCE_IS_TARGET");

        RequireFailure(
            planner.CreatePlan(
                source,
                new[] { Snapshot(TargetOneId, UnsupportedGeometry(), targetOne.UserText) }),
            "GEOMETRY_MISMATCH");
    }

    private static void VerifyParentCellTransfer(
        PanelCladdingMatchPlanningService planner,
        PanelCladdingKeyService keys)
    {
        PanelCladdingTopologyPayloads masks = RequireData(
            keys.EncodeTopology(new PanelCladdingTopologyState(), 1, 1),
            "Encode parent-cell topology");
        PanelCladdingMatchPanelSnapshot source = Snapshot(
            SourceId,
            PlanarGeometry(),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [PanelCladdingKeyService.GetHorizontalOffsetKey(0)] = "40",
                [PanelCladdingKeyService.GetVerticalOffsetKey(0)] = "50",
                [PanelCladdingKeyService.GetCellKey(0, "A")] = "MPL-001",
                [PanelCladdingKeyService.GetCellKey(0, "B")] = "MPL-001",
                [PanelCladdingKeyService.GetCellKey(1, "A")] = "0A",
                [PanelCladdingKeyService.GetCellKey(1, "B")] = "0B",
                [PanelCladdingKeyService.TypeCodeKey] = "WT01-2X2-PARENT",
                [PanelCladdingKeyService.SignatureKey] = "v4:sha256:parent",
                [PanelCladdingKeyService.SegmentMaskKey] = masks.SegmentMask,
                [PanelCladdingKeyService.MergeMaskKey] = masks.MergeMask
            });
        PanelCladdingMatchPlan plan = RequireData(
            planner.CreatePlan(
                source,
                new[] { Snapshot(TargetOneId, PlanarGeometry(), TargetGrid("25", "65")) }),
            "Create parent-cell match plan");
        IReadOnlyDictionary<string, string> writes = plan.Targets.Single().UserTextWrites;
        Require(writes[PanelCladdingKeyService.GetCellKey(0, "A")] == "MPL-001" &&
                writes[PanelCladdingKeyService.GetCellKey(0, "B")] == "MPL-001" &&
                writes[PanelCladdingKeyService.GetCellKey(1, "A")] == "0A" &&
                writes[PanelCladdingKeyService.GetCellKey(1, "B")] == "0B",
            "Parent-cell references were flattened to their resolved material.");
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
        Type spawnCommand = RequireType(assembly, "PanelCladdingEditor.UI.PanelCladdingSpawnSrfCommand");
        Require(matchCommand.GUID != Guid.Empty, "Match command GUID must be explicit and non-empty.");
        Require(new[] { matchCommand.GUID, editorCommand.GUID, spawnCommand.GUID }
                .Distinct().Count() == 3,
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
            [PanelCladdingKeyService.TypeCodeKey] = "WT01-3X2-ABCDEF12",
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
        userText.Remove(PanelCladdingKeyService.GetCellKey(0, "B"));
        PanelCladdingTopologyPayloads topology = RequireData(
            new PanelCladdingKeyService().EncodeTopology(
                new PanelCladdingTopologyState
                {
                    MissingSegments = new[]
                    {
                        new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Horizontal, 0, 0)
                    },
                    MergeRuns = new[]
                    {
                        new PanelCladdingMergeRun(PanelCladdingTopologyAxis.Vertical, 0, 0, 1)
                    }
                },
                horizontalTrackCount: 1,
                verticalTrackCount: 2),
            "Encode configured source topology");
        userText[PanelCladdingKeyService.SegmentMaskKey] = topology.SegmentMask;
        userText[PanelCladdingKeyService.MergeMaskKey] = topology.MergeMask;
        return Snapshot(objectId, geometry, userText);
    }

    private static IReadOnlyDictionary<string, string> TargetGrid(
        string horizontal,
        string verticalZero,
        string? verticalOne = null)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CW_2.03_OFFSET_H0"] = horizontal,
            ["CW_2.04_OFFSET_V0"] = verticalZero
        };
        if (verticalOne is not null)
        {
            result["CW_2.04_OFFSET_V1"] = verticalOne;
        }
        return result;
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
