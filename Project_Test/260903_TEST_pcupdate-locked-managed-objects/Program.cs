using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Rhino.DocObjects;
using Rhino.DocObjects.Tables;
using Rhino.Geometry;

namespace PCUpdateLockedManagedObjectsSmoke;

internal static class Program
{
    private static void Main()
    {
        VerifyRhinoCommonModeBypassOverloads();
        VerifyModeAwareMutationContract();
        VerifyPackageContract();
    }

    private static void VerifyRhinoCommonModeBypassOverloads()
    {
        MethodInfo? replace = typeof(ObjectTable).GetMethod(
            nameof(ObjectTable.Replace),
            [typeof(Guid), typeof(GeometryBase), typeof(bool)]);
        Require(replace is not null && replace.ReturnType == typeof(bool),
            "RhinoCommon lacks Replace(Guid, GeometryBase, bool ignoreModes).");

        MethodInfo? delete = typeof(ObjectTable).GetMethod(
            nameof(ObjectTable.Delete),
            [typeof(RhinoObject), typeof(bool), typeof(bool)]);
        Require(delete is not null && delete.ReturnType == typeof(bool),
            "RhinoCommon lacks Delete(RhinoObject, bool quiet, bool ignoreModes).");
        Console.WriteLine("[OK] RhinoCommon exposes mode-bypassing replace and delete overloads");
    }

    private static void VerifyModeAwareMutationContract()
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
            "LockedObjects = true",
            "HiddenObjects = true",
            "attributes.Data.Mode = existingObject.Attributes.Mode;"
        };
        foreach (string token in required)
        {
            Require(source.Contains(token, StringComparison.Ordinal),
                $"PCUpdate protected-object contract is missing: {token}");
        }

        Require(Regex.IsMatch(
                source,
                @"document\.Objects\.Replace\(\s*objectId,\s*dependency\.Geometry,\s*ignoreModes:\s*true\)",
                RegexOptions.CultureInvariant),
            "PCUpdate retained dependencies must use mode-bypassing geometry replacement.");
        Require(Regex.IsMatch(
                source,
                @"document\.Objects\.Delete\(\s*existingObject,\s*quiet:\s*true,\s*ignoreModes:\s*true\)",
                RegexOptions.CultureInvariant),
            "PCUpdate stale/duplicate dependencies must use mode-bypassing deletion.");

        string[] forbidden =
        {
            "document.Objects.Replace(objectId, brep)",
            "document.Objects.Replace(objectId, curve)",
            "document.Objects.Delete(objectId, quiet: true)",
            "document.Objects.Unlock(",
            "document.Objects.Show("
        };
        foreach (string token in forbidden)
        {
            Require(!source.Contains(token, StringComparison.Ordinal),
                $"PCUpdate retained a mode-sensitive or state-changing mutation: {token}");
        }
        Console.WriteLine(
            "[OK] PCUpdate bypasses lock/visibility modes while preserving retained object mode");
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
            "Protected-object PCUpdate support requires package 1.0.73 or later.");
        Console.WriteLine("[OK] package supports protected-object PCUpdate");
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
