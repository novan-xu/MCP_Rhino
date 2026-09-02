using System.Text.RegularExpressions;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingTypeCodeFormatSmoke;

internal static class Program
{
    private static int Main()
    {
        var keys = new PanelCladdingKeyService();
        var signatures = new PanelCladdingTypeSignatureService(keys);
        PanelCladdingLayout layout = BuildLayout(keys);
        IReadOnlyDictionary<string, string> values = layout.Cells.ToDictionary(
            cell => cell.UserTextKey,
            cell => cell.Value,
            StringComparer.OrdinalIgnoreCase);

        PanelCladdingTypeIdentity identity = Required(
            signatures.Create(layout, values, "WT-01"),
            "Create standard type identity");
        Require(Regex.IsMatch(identity.TypeCode, "^WT01-2X2-[0-9A-F]{8}$"),
            $"Unexpected marker-free type code: {identity.TypeCode}");
        Require(!identity.TypeCode.Contains("-CL-", StringComparison.Ordinal),
            "Generated type code still contains the cladding marker.");

        PanelCladdingTypeIdentity otherSystem = Required(
            signatures.Create(layout, values, "CW-02"),
            "Create alternate-system identity");
        Require(Regex.IsMatch(otherSystem.TypeCode, "^CW02-2X2-[0-9A-F]{8}$") &&
                otherSystem.FullDigest == identity.FullDigest &&
                otherSystem.CanonicalPayload == identity.CanonicalPayload,
            "Changing only the readable system prefix changed the canonical panel identity.");

        PanelCladdingTypeIdentity fallback = Required(
            signatures.Create(layout, values, string.Empty),
            "Create fallback-system identity");
        Require(Regex.IsMatch(fallback.TypeCode, "^PANEL-2X2-[0-9A-F]{8}$"),
            $"Fallback type code is not marker-free: {fallback.TypeCode}");

        PanelCladdingTypeIdentity longSystem = Required(
            signatures.Create(layout, values, "CURTAIN-WALL-TYPE-123"),
            "Create long-system identity");
        Require(longSystem.TypeCode.StartsWith("CURTAINWAL-2X2-", StringComparison.Ordinal) &&
                longSystem.TypeCode.Length <= 31,
            $"Long system prefix was not normalized within the type-code limit: {longSystem.TypeCode}");

        string collisionExpanded = PanelCladdingTypeSignatureService.WithDigestLength(identity, 10);
        Require(Regex.IsMatch(collisionExpanded, "^WT01-2X2-[0-9A-F]{10}$") &&
                collisionExpanded.EndsWith(
                    identity.FullDigest[..10].ToUpperInvariant(),
                    StringComparison.Ordinal),
            $"Collision expansion did not retain the marker-free format: {collisionExpanded}");

        string root = FindRepositoryRoot();
        string editorSource = File.ReadAllText(Path.Combine(
            root, "src", "PanelCladdingEditor", "UI", "PanelCladdingEditorWindow.xaml.cs"));
        Require(!editorSource.Contains("-CL-", StringComparison.Ordinal) &&
                editorSource.Contains(
                    "-{_layout.ColumnCount}X{_layout.RowCount}-PENDING",
                    StringComparison.Ordinal),
            "The editor pending preview does not use the marker-free type-code shape.");

        Console.WriteLine($"[OK] standard marker-free type code: {identity.TypeCode}");
        Console.WriteLine($"[OK] fallback marker-free type code: {fallback.TypeCode}");
        Console.WriteLine($"[OK] collision-expanded type code: {collisionExpanded}");
        Console.WriteLine("[OK] v4 canonical payload and full digest remain unchanged by the system prefix.");
        return 0;
    }

    private static PanelCladdingLayout BuildLayout(PanelCladdingKeyService keys)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetCellKey(0, "A")] = "GL-01",
            [PanelCladdingKeyService.GetCellKey(0, "B")] = "MPL-02",
            [PanelCladdingKeyService.GetCellKey(1, "A")] = "GL-01",
            [PanelCladdingKeyService.GetCellKey(1, "B")] = "MPL-02"
        };
        PanelCladdingKeySet keySet = Required(
            keys.CreateKeySet([40d], [50d], values, 100d, 80d, 0.001d),
            "Build type-code fixture");
        return new PanelCladdingLayout
        {
            ObjectId = Guid.Parse("A8200000-0000-0000-0000-000000000301"),
            DocumentRuntimeSerialNumber = 1,
            DocumentPath = "C:/test/panel-type-code-format.3dm",
            ObjectName = "PID_TYPE_CODE",
            LayerFullPath = "01_CW Panels::WT-01",
            SystemCode = string.Empty,
            GeometryFingerprint = "type-code-format-fixture",
            GeometryClass = PanelGeometryClass.Planar,
            GeometryDiagnostic = "Planar fixture.",
            Width = 100d,
            Height = 80d,
            ModelTolerance = 0.001d,
            ModelUnitScaleToMillimeters = 1d,
            HorizontalOffsets = keySet.HorizontalOffsets,
            VerticalOffsets = keySet.VerticalOffsets,
            Cells = keySet.Cells,
            Topology = new PanelCladdingTopologyState()
        };
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static T Required<T>(OperationResponse<T> response, string operation) where T : class =>
        response.Success && response.Data is not null
            ? response.Data
            : throw new InvalidOperationException($"{operation} failed: {response.Message}");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
