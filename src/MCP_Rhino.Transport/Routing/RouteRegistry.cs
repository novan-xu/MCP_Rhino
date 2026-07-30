using System.Text.Json;

namespace MCP_Rhino.Transport.Routing;

public sealed class RouteRegistry
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public RouteRegistry(string? directoryPath = null)
    {
        DirectoryPath = string.IsNullOrWhiteSpace(directoryPath)
            ? GetDefaultDirectoryPath()
            : Path.GetFullPath(directoryPath);
    }

    public string DirectoryPath { get; }

    public static string GetDefaultDirectoryPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MCP_Rhino",
            "Routing",
            RouteProtocol.RegistryDirectoryName);
    }

    public string GetDescriptorPath(int processId, uint runtimeSerialNumber)
    {
        if (processId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId));
        }

        return Path.Combine(DirectoryPath, $"{processId}_{runtimeSerialNumber}.json");
    }

    public void Write(RouteEndpointDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (!descriptor.IsStructurallyValid())
        {
            throw new InvalidDataException("The route descriptor is structurally invalid.");
        }

        Directory.CreateDirectory(DirectoryPath);
        string destination = GetDescriptorPath(descriptor.ProcessId, descriptor.RuntimeSerialNumber);
        string temporary = Path.Combine(DirectoryPath, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(descriptor, SerializerOptions));
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public IReadOnlyList<RouteRegistryEntry> ReadAll()
    {
        if (!Directory.Exists(DirectoryPath))
        {
            return Array.Empty<RouteRegistryEntry>();
        }

        var entries = new List<RouteRegistryEntry>();
        foreach (string path in Directory.EnumerateFiles(DirectoryPath, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                string json = File.ReadAllText(path);
                RouteEndpointDescriptor? descriptor = JsonSerializer.Deserialize<RouteEndpointDescriptor>(json, SerializerOptions);
                if (descriptor is null || !descriptor.IsStructurallyValid())
                {
                    entries.Add(new RouteRegistryEntry(path, null, "INVALID_DESCRIPTOR"));
                    continue;
                }

                entries.Add(new RouteRegistryEntry(path, descriptor, null));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
            {
                entries.Add(new RouteRegistryEntry(path, null, $"READ_FAILED: {ex.Message}"));
            }
        }

        return entries;
    }

    public bool DeleteIfGenerationMatches(RouteEndpointDescriptor expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        string path = GetDescriptorPath(expected.ProcessId, expected.RuntimeSerialNumber);

        try
        {
            if (!File.Exists(path))
            {
                return true;
            }

            RouteEndpointDescriptor? current = JsonSerializer.Deserialize<RouteEndpointDescriptor>(
                File.ReadAllText(path),
                SerializerOptions);
            if (current is null
                || !string.Equals(current.PluginInstanceGeneration, expected.PluginInstanceGeneration, StringComparison.Ordinal)
                || current.DocumentLifecycleGeneration != expected.DocumentLifecycleGeneration)
            {
                return false;
            }

            File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }
}

public sealed record RouteRegistryEntry(
    string DescriptorPath,
    RouteEndpointDescriptor? Descriptor,
    string? Diagnostic);
