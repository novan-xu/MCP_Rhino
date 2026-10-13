namespace MCP_Rhino.Server.Domain.Models;

public static class StoneWireframeBakeNaming
{
    public const string OutputPrefix = "WF_";
    public const string AssemblyAttributeKey = "Assembly_ID";

    public static bool IsAssemblyDefinitionName(string? value)
    {
        return Matches(value, "STN-MET-GA-");
    }

    public static bool IsFlatStoneDefinitionName(string? value)
    {
        return Matches(value, "STN-MET-1");
    }

    public static bool IsVerticalStoneDefinitionName(string? value)
    {
        return Matches(value, "STN-MET-2");
    }

    public static bool IsStoneDefinitionName(string? value)
    {
        return IsFlatStoneDefinitionName(value) || IsVerticalStoneDefinitionName(value);
    }

    public static string OutputDefinitionName(string sourceStoneDefinitionName)
    {
        return OutputPrefix + sourceStoneDefinitionName;
    }

    private static bool Matches(string? value, string prefix)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string suffix = value[prefix.Length..];
        int separator = suffix.IndexOf('-');
        if (separator <= 0 || separator == suffix.Length - 1 || suffix.IndexOf('-', separator + 1) >= 0)
        {
            return false;
        }

        return suffix.All(character => character == '-' || IsAsciiAlphaNumeric(character));
    }

    private static bool IsAsciiAlphaNumeric(char character)
    {
        return (character >= '0' && character <= '9')
            || (character >= 'A' && character <= 'Z')
            || (character >= 'a' && character <= 'z');
    }
}
