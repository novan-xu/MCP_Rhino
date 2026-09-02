namespace ExtrusionAssignmentDropZoneSmoke;

internal static class Program
{
    private static int Main()
    {
        string root = FindRepositoryRoot();
        string xaml = Read(root, "src", "PanelCladdingEditor", "UI", "PanelCladdingEditorWindow.xaml");
        string window = Read(root, "src", "PanelCladdingEditor", "UI", "PanelCladdingEditorWindow.xaml.cs");
        string canvas = Read(root, "src", "PanelCladdingEditor", "UI", "PanelCladdingGridCanvas.cs");
        string cards = Read(root, "src", "PanelCladdingEditor", "UI", "PanelFrameExtrusion.cs");

        Require(xaml.Contains("x:Name=\"ExtrusionAssignmentSection\"", StringComparison.Ordinal) &&
                xaml.Contains("PreviewDrop=\"OnExtrusionAssignmentDrop\"", StringComparison.Ordinal),
            "The extrusion assignment section is not the declared drop target.");
        Require(!xaml.Contains("ExtrusionCodeDropped=", StringComparison.Ordinal) &&
                !canvas.Contains("ExtrusionCodeDropped", StringComparison.Ordinal),
            "The canvas still exposes extrusion-profile drop assignment.");
        Require(!xaml.Contains("Click=\"OnExtrusionTileClick\"", StringComparison.Ordinal) &&
                !window.Contains("OnExtrusionTileClick", StringComparison.Ordinal),
            "Catalogue tiles still support click-to-assign.");
        Require(xaml.Contains("AssignedExtrusionTileTemplate", StringComparison.Ordinal) &&
                xaml.Contains("OnAssignedModifierLostKeyboardFocus", StringComparison.Ordinal) &&
                cards.Contains("PanelExtrusionAssignmentCard", StringComparison.Ordinal),
            "Assigned extrusion preview cards or inline modifiers are missing.");
        Require(window.Contains("_availableFrameExtrusions", StringComparison.Ordinal) &&
                window.Contains("RefreshAvailableExtrusions(assignedCodes)", StringComparison.Ordinal),
            "The ready catalogue is not filtered against current assignment cards.");
        Require(window.Contains("ResolveDependencyChain", StringComparison.Ordinal) &&
                window.Contains("if (codes.Contains(code))", StringComparison.Ordinal),
            "Parent dependency expansion/manual-removal behavior was not preserved.");

        Console.WriteLine("[OK] assignment panel is the only extrusion-profile drop target.");
        Console.WriteLine("[OK] catalogue tiles are drag-only and assigned cards include previews and modifiers.");
        Console.WriteLine("[OK] available profiles are filtered against the selected curves' assignments.");
        return 0;
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(parts.Aggregate(root, Path.Combine));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
