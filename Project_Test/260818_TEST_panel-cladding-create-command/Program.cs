using System.Globalization;
using System.Reflection;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using Rhino.Commands;

namespace PanelCladdingCreateSmoke;

internal static class Program
{
    private static readonly Guid PanelOneId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PanelTwoId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static void Main()
    {
        var planner = new PanelCladdingCreatePlanningService(new PanelCladdingKeyService());
        VerifyGridPlanning(planner);
        VerifyUnitDimensions(planner);
        VerifyMultiPanelAndFailureBehavior(planner);
        VerifyCommandContract();

        Console.WriteLine("[OK] H/V guides create canonical bottom-up and left-right panel cells.");
        Console.WriteLine("[OK] PCCreate writes per-panel dimensions, refreshes stale keys, and uses invariant five-decimal values.");
        Console.WriteLine("[OK] Remote, exterior, diagonal, and duplicate guides are handled deterministically.");
        Console.WriteLine("[OK] Existing grid/type/signature metadata resets while unrelated panel metadata is preserved.");
        Console.WriteLine("[OK] PCCreate exposes the required panel-first, curve-second Rhino command flow.");
    }

    private static void VerifyGridPlanning(PanelCladdingCreatePlanningService planner)
    {
        var userText = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.MergeMaskKey] = "stale-merge",
            ["CW_2.03_OFFSET_H0"] = "10",
            ["CW_2.04_OFFSET_V0"] = "25",
            ["CW_4.00_CLADDING_0A"] = "GL01",
            ["CW_9.99_OFFSET_CUSTOM"] = "reset",
            [PanelCladdingKeyService.TypeCodeKey] = "WT01-P-2X2-ABCDEF12",
            [PanelCladdingKeyService.LegacyTypeCodeKey] = "LEGACY",
            [PanelCladdingKeyService.SignatureKey] = "v1:sha256:configured",
            [PanelCladdingKeyService.LegacySignatureKey] = "legacy-signature",
            ["CW_1.01_PID"] = "PANEL-01",
            ["CW_1.05_LOT"] = "R2",
            ["CW_1.07_WALL_TYPE"] = "WT01",
            ["CW_1.02_CID"] = "PANEL-01-CID",
            ["Custom"] = "preserve"
        };
        PanelCladdingCreatePanelSnapshot snapshot = Snapshot(
            PanelOneId,
            new[]
            {
                Guide("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1", (-5d, 20d, 0d), (105d, 20d, 0d)),
                Guide("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2", (-5d, 20d, 0d), (105d, 20d, 0d)),
                Guide("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1", (40d, -5d, 0d), (40d, 85d, 0d)),
                Guide("cccccccc-cccc-cccc-cccc-ccccccccccc1", (-5d, 60d, 100d), (105d, 60d, 100d)),
                Guide("dddddddd-dddd-dddd-dddd-ddddddddddd1", (-5d, 100d, 0d), (105d, 100d, 0d)),
                Guide("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeee1", (0d, 0d, 0d), (100d, 80d, 0d))
            },
            userText);

        PanelCladdingCreatePlan plan = RequireData(planner.CreatePlan(new[] { snapshot }), "Create grid plan");
        PanelCladdingCreatePanelPlan panel = plan.Panels.Single();
        Require(panel.HorizontalOffsets.SequenceEqual(new[] { 20d }), "H guides must deduplicate at bottom-up offset 20.");
        Require(panel.VerticalOffsets.SequenceEqual(new[] { 40d }), "V guide must resolve to left-right offset 40.");
        Require(panel.CellCount == 4, "One H and one V guide must create four cells.");
        Require(panel.Warnings.Count == 1 && panel.Warnings[0].Contains("GUIDE_DIAGONAL", StringComparison.Ordinal),
            "The diagonal guide must be skipped with one warning.");

        var expectedWrites = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CW_2.00_UNIT_DIMENSION"] = "100.00000x80.00000",
            ["CW_2.01_UNIT_WIDTH"] = "100.00000",
            ["CW_2.02_UNIT_HEIGHT"] = "80.00000",
            ["CW_2.03_OFFSET_H0"] = "20",
            ["CW_2.04_OFFSET_V0"] = "40",
            [PanelCladdingKeyService.CladdingLogicKey] =
                "{\"0A\":\"\",\"0B\":\"\",\"1A\":\"\",\"1B\":\"\"}",
            ["CW_4.00_CLADDING_0A"] = " ",
            ["CW_4.00_CLADDING_0B"] = " ",
            ["CW_4.01_CLADDING_1A"] = " ",
            ["CW_4.01_CLADDING_1B"] = " "
        };
        Require(panel.UserTextWrites.Count == expectedWrites.Count, "Grid write count is incorrect.");
        foreach ((string key, string value) in expectedWrites)
        {
            Require(panel.UserTextWrites.TryGetValue(key, out string? actual) && actual == value,
                $"Missing or incorrect canonical grid write: {key}");
        }

        foreach (string deleted in new[]
        {
            "CW_2.03_OFFSET_H0", "CW_2.04_OFFSET_V0", "CW_4.00_CLADDING_0A",
            "CW_9.99_OFFSET_CUSTOM", PanelCladdingKeyService.TypeCodeKey,
            PanelCladdingKeyService.LegacyTypeCodeKey, PanelCladdingKeyService.SignatureKey,
            PanelCladdingKeyService.LegacySignatureKey, PanelCladdingKeyService.MergeMaskKey
        })
        {
            Require(panel.UserTextDeletes.Contains(deleted, StringComparer.OrdinalIgnoreCase),
                $"Existing generated metadata was not reset: {deleted}");
        }
        foreach (string preserved in new[]
        {
            "CW_1.01_PID", "CW_1.05_LOT", "CW_1.07_WALL_TYPE", "CW_1.02_CID", "Custom"
        })
        {
            Require(!panel.UserTextDeletes.Contains(preserved, StringComparer.OrdinalIgnoreCase),
                $"Unrelated panel metadata must be preserved: {preserved}");
        }
    }

    private static void VerifyUnitDimensions(PanelCladdingCreatePlanningService planner)
    {
        var stale = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CW_2.00_UNIT_DIMENSION"] = "999x888",
            ["cw_2.01_unit_width"] = "999",
            ["CW_2.02_Unit_Height"] = "888",
            ["Custom"] = "preserve"
        };
        PanelCladdingCreatePanelSnapshot translated = Snapshot(
            PanelOneId,
            new[] { Guide("40000000-0000-0000-0000-000000000001", (-20d, 50d, 0d), (120d, 50d, 0d)) },
            stale,
            xMinimum: -12.5d,
            xMaximum: 110.956789d,
            yMinimum: 20d,
            yMaximum: 87.891234d);
        PanelCladdingCreatePanelSnapshot fresh = Snapshot(
            PanelTwoId,
            new[] { Guide("40000000-0000-0000-0000-000000000002", (-5d, 30d, 0d), (105d, 30d, 0d)) });

        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            PanelCladdingCreatePlan plan = RequireData(planner.CreatePlan(new[] { translated, fresh }),
                "Create dimensions under comma-decimal culture");
            PanelCladdingCreatePanelPlan first = plan.Panels[0];
            Require(first.UserTextWrites["CW_2.00_UNIT_DIMENSION"] == "123.45679x67.89123" &&
                    first.UserTextWrites["CW_2.01_UNIT_WIDTH"] == "123.45679" &&
                    first.UserTextWrites["CW_2.02_UNIT_HEIGHT"] == "67.89123",
                "Dimensions must use local extents and invariant five-decimal rounding, not absolute coordinates or guide lengths.");
            Require(first.UserTextWrites["CW_2.03_OFFSET_H0"] == "30",
                "Dimension writes must preserve the local H offset.");
            Require(plan.Panels[1].UserTextWrites["CW_2.00_UNIT_DIMENSION"] == "100.00000x80.00000" &&
                    plan.Panels[1].UserTextWrites["CW_2.01_UNIT_WIDTH"] == "100.00000" &&
                    plan.Panels[1].UserTextWrites["CW_2.02_UNIT_HEIGHT"] == "80.00000",
                "Each panel must receive its own dimensions, including panels without existing dimension keys.");

            var applied = new Dictionary<string, string>(stale, StringComparer.Ordinal);
            foreach (string key in first.UserTextDeletes)
            {
                applied.Remove(key);
            }
            foreach ((string key, string value) in first.UserTextWrites)
            {
                applied[key] = value;
            }
            Require(!applied.ContainsKey("cw_2.01_unit_width") && !applied.ContainsKey("CW_2.02_Unit_Height") &&
                    applied["CW_2.00_UNIT_DIMENSION"] == "123.45679x67.89123" &&
                    applied["CW_2.01_UNIT_WIDTH"] == "123.45679" &&
                    applied["CW_2.02_UNIT_HEIGHT"] == "67.89123" && applied["Custom"] == "preserve",
                "Applying the plan must replace stale dimensions/casing and preserve unrelated metadata.");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    private static void VerifyMultiPanelAndFailureBehavior(PanelCladdingCreatePlanningService planner)
    {
        PanelCladdingCreatePanelSnapshot first = Snapshot(
            PanelOneId,
            new[] { Guide("10000000-0000-0000-0000-000000000001", (-5d, 30d, 0d), (105d, 30d, 0d)) });
        PanelCladdingCreatePanelSnapshot second = Snapshot(
            PanelTwoId,
            new[] { Guide("20000000-0000-0000-0000-000000000001", (70d, -5d, 0d), (70d, 85d, 0d)) });
        PanelCladdingCreatePlan multi = RequireData(
            planner.CreatePlan(new[] { first, second, first }),
            "Create multi-panel plan");
        Require(multi.Panels.Select(panel => panel.ObjectId).SequenceEqual(new[] { PanelOneId, PanelTwoId }),
            "Panel order must be stable and duplicate selections must collapse.");
        Require(multi.Panels[0].HorizontalOffsets.SequenceEqual(new[] { 30d }) &&
                multi.Panels[0].VerticalOffsets.Count == 0 && multi.Panels[0].CellCount == 2,
            "The first panel did not receive its applicable H guide.");
        Require(multi.Panels[1].HorizontalOffsets.Count == 0 &&
                multi.Panels[1].VerticalOffsets.SequenceEqual(new[] { 70d }) && multi.Panels[1].CellCount == 2,
            "The second panel did not receive its applicable V guide.");

        RequireFailure(planner.CreatePlan(Array.Empty<PanelCladdingCreatePanelSnapshot>()),
            "PANEL_SELECTION_REQUIRED");
        RequireFailure(planner.CreatePlan(new[] { Snapshot(PanelOneId, Array.Empty<PanelCladdingCreateGuideSnapshot>()) }),
            "GUIDE_SELECTION_REQUIRED");
        RequireFailure(planner.CreatePlan(new[]
        {
            Snapshot(PanelOneId, new[]
            {
                Guide("30000000-0000-0000-0000-000000000001", (-5d, 20d, 100d), (105d, 20d, 100d))
            })
        }), "APPLICABLE_GUIDES_REQUIRED");
    }

    private static void VerifyCommandContract()
    {
        Assembly assembly = typeof(PanelCladdingCreatePlanningService).Assembly;
        Type createCommand = RequireType(assembly, "PanelCladdingEditor.UI.PanelCladdingCreateCommand");
        Require(typeof(Command).IsAssignableFrom(createCommand), "PCCreate is not a Rhino command.");
        Require(createCommand.GUID != Guid.Empty, "PCCreate command GUID must be explicit and non-empty.");

        Type[] commands = assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(Command).IsAssignableFrom(type))
            .ToArray();
        Require(commands.Select(type => type.GUID).All(guid => guid != Guid.Empty),
            "Every Rhino command must have an explicit non-empty GUID.");
        Require(commands.Select(type => type.GUID).Distinct().Count() == commands.Length,
            "All PanelCladdingEditor Rhino command GUIDs must be unique.");
        Require(assembly.GetType(
                "PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding.LivePanelCladdingCreateService") is not null,
            "Live create adapter is missing.");

        MethodInfo create = typeof(ILivePanelCladdingCreateService)
            .GetMethod(nameof(ILivePanelCladdingCreateService.Create)) ??
            throw new InvalidOperationException("Live create contract is missing.");
        ParameterInfo[] parameters = create.GetParameters();
        Require(parameters.Length == 3 && parameters[0].ParameterType == typeof(string) &&
                parameters[1].ParameterType == typeof(IReadOnlyList<Guid>) &&
                parameters[2].ParameterType == typeof(IReadOnlyList<Guid>),
            "Live create contract must accept a document path, panel ids, and curve ids.");

        string commandSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "PanelCladdingEditor", "UI", "PanelCladdingCreateCommand.cs"));
        Require(commandSource.Contains("EnglishName => \"PCCreate\"", StringComparison.Ordinal),
            "The exact requested Rhino command name is missing.");
        int panelPrompt = commandSource.IndexOf(
            "Select panel surfaces to create cladding grid attributes", StringComparison.Ordinal);
        int curvePrompt = commandSource.IndexOf(
            "Select curves to create panel H and V offsets", StringComparison.Ordinal);
        Require(panelPrompt >= 0 && curvePrompt > panelPrompt,
            "The command must prompt for panels before guide curves.");
        Require(Count(commandSource, "GetMultiple(1, 0)") == 2,
            "The command must use two separate non-empty object selection phases.");

        string[] forbidden = { "MCP_Rhino", "ModelContextProtocol", "Microsoft.Extensions.Hosting" };
        Require(!assembly.GetReferencedAssemblies().Any(reference => forbidden.Any(token =>
                (reference.Name ?? string.Empty).Contains(token, StringComparison.OrdinalIgnoreCase))),
            "PCCreate introduced an MCP_Rhino or MCP SDK dependency.");
    }

    private static PanelCladdingCreatePanelSnapshot Snapshot(
        Guid objectId,
        IReadOnlyList<PanelCladdingCreateGuideSnapshot> guides,
        IReadOnlyDictionary<string, string>? userText = null,
        double xMinimum = 0d,
        double xMaximum = 100d,
        double yMinimum = 0d,
        double yMaximum = 80d)
    {
        return new PanelCladdingCreatePanelSnapshot
        {
            ObjectId = objectId,
            XMinimum = xMinimum,
            XMaximum = xMaximum,
            YMinimum = yMinimum,
            YMaximum = yMaximum,
            ZMinimum = -1d,
            ZMaximum = 1d,
            Tolerance = 0.001d,
            Guides = guides,
            UserText = userText ?? new Dictionary<string, string>()
        };
    }

    private static PanelCladdingCreateGuideSnapshot Guide(
        string objectId,
        (double X, double Y, double Z) start,
        (double X, double Y, double Z) end)
    {
        return new PanelCladdingCreateGuideSnapshot
        {
            ObjectId = Guid.Parse(objectId),
            Samples = new[]
            {
                new PanelPoint3(start.X, start.Y, start.Z),
                new PanelPoint3((start.X + end.X) / 2d, (start.Y + end.Y) / 2d, (start.Z + end.Z) / 2d),
                new PanelPoint3(end.X, end.Y, end.Z)
            }
        };
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src", "PanelCladdingEditor")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root could not be located.");
    }

    private static int Count(string source, string token)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }
        return count;
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
