using System.Reflection;
using System.Text.Json;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using Rhino;
using Rhino.Runtime.InProcess;

namespace PCUpdateCommandUndoSmoke;

internal static class Program
{
    private const string NativeSwitch = "--rhino-undo";

    private static void Main(string[] args)
    {
        VerifySourceContract();
        VerifyPackageContract();

        if (!args.Contains(NativeSwitch, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine(
                "[SKIP] native ambient Undo verification requires an initialized Rhino runtime; " +
                $"pass {NativeSwitch} in a supported Rhino test host");
            return;
        }

        string[] coreArguments = Environment.GetCommandLineArgs()
            .Where(argument => !argument.Equals(NativeSwitch, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        using var core = new RhinoCore(coreArguments, WindowStyle.NoWindow);
        VerifyAmbientUndoRecordIsReused();
    }

    private static void VerifySourceContract()
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
            "uint undoRecord = document.CurrentUndoRecordSerialNumber;",
            "bool ownsUndoRecord = undoRecord == 0U;",
            "if (ownsUndoRecord)",
            "undoRecord = document.BeginUndoRecord(\"Update Panel Cladding Dependencies\");"
        };
        foreach (string token in required)
        {
            Require(source.Contains(token, StringComparison.Ordinal),
                $"PCUpdate ambient Undo contract is missing: {token}");
        }

        int beginIndex = source.IndexOf(
            "undoRecord = document.BeginUndoRecord(\"Update Panel Cladding Dependencies\");",
            StringComparison.Ordinal);
        int ownershipGuardIndex = source.LastIndexOf(
            "if (ownsUndoRecord)",
            beginIndex,
            StringComparison.Ordinal);
        Require(ownershipGuardIndex >= 0 && ownershipGuardIndex < beginIndex,
            "PCUpdate must guard BeginUndoRecord with service ownership.");

        int finallyIndex = source.IndexOf("finally", beginIndex, StringComparison.Ordinal);
        int endIndex = source.IndexOf("document.EndUndoRecord(undoRecord);", finallyIndex, StringComparison.Ordinal);
        int rollbackIndex = source.IndexOf("!document.Undo()", endIndex, StringComparison.Ordinal);
        int finalOwnershipGuardIndex = source.LastIndexOf(
            "if (ownsUndoRecord)",
            endIndex,
            StringComparison.Ordinal);
        Require(finalOwnershipGuardIndex > finallyIndex && finalOwnershipGuardIndex < endIndex,
            "PCUpdate must not end an ambient Rhino command Undo record.");
        Require(rollbackIndex > endIndex,
            "Service-owned failure rollback must remain after EndUndoRecord.");
        Console.WriteLine("[OK] PCUpdate opens, closes, and rolls back only service-owned Undo records");
    }

    private static void VerifyPackageContract()
    {
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "Packaging",
            "PanelCladdingEditor",
            "package-manifest.json")));
        Require(Version.TryParse(manifest.RootElement.GetProperty("version").GetString(), out Version? version) &&
                version >= new Version(1, 0, 73),
            "PCUpdate Undo support requires package 1.0.73 or later.");
        Console.WriteLine("[OK] package supports PCUpdate Undo handling");
    }

    private static void VerifyAmbientUndoRecordIsReused()
    {
        using RhinoDoc document = RhinoDoc.CreateHeadless(null);
        uint ambientUndoRecord = document.BeginUndoRecord("PCUpdate ambient command simulation");
        Require(ambientUndoRecord != 0U, "The Rhino test host did not open an ambient Undo record.");

        try
        {
            OperationResponse<PanelCladdingDependencyReconciliationPlan> planResponse =
                new PanelCladdingDependencyReconciliationService().CreatePlan([], []);
            Require(planResponse.Success && planResponse.Data is not null,
                $"The empty reconciliation plan failed: {planResponse.Message}");

            Type serviceType = typeof(PanelCladdingDependencyReconciliationService).Assembly.GetType(
                "PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding.LivePanelCladdingUpdateService") ??
                throw new InvalidOperationException("LivePanelCladdingUpdateService is missing.");
            MethodInfo apply = serviceType.GetMethod(
                "Apply",
                BindingFlags.Static | BindingFlags.NonPublic) ??
                throw new InvalidOperationException("LivePanelCladdingUpdateService.Apply is missing.");
            Type preparedType = apply.GetParameters()[2].ParameterType.GetGenericArguments()[0];
            Array prepared = Array.CreateInstance(preparedType, 0);

            object response = apply.Invoke(
                null,
                [document, Array.Empty<Guid>(), prepared, planResponse.Data]) ??
                throw new InvalidOperationException("PCUpdate Apply returned null.");
            bool success = (bool?)response.GetType().GetProperty("Success")?.GetValue(response) ?? false;
            string message = (string?)response.GetType().GetProperty("Message")?.GetValue(response) ?? string.Empty;

            Require(success, $"PCUpdate rejected Rhino's ambient Undo record: {message}");
            Require(document.CurrentUndoRecordSerialNumber == ambientUndoRecord,
                "PCUpdate replaced or closed Rhino's ambient Undo record.");
            Console.WriteLine(
                $"[OK] native Rhino ambient Undo record {ambientUndoRecord} was reused and left open");
        }
        finally
        {
            document.EndUndoRecord(ambientUndoRecord);
        }
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
