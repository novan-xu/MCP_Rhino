using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoStoneWireframeBakeService
{
    private readonly ILiveStoneWireframeBakeOperator _operator;

    public RhinoStoneWireframeBakeService(ILiveStoneWireframeBakeOperator bakeOperator)
    {
        _operator = bakeOperator;
    }

    public OperationResponse<StoneWireframeBakeResponse> Preview(
        string filePath,
        IEnumerable<string>? assemblyDefinitionNames,
        StoneBakeOutputMode outputMode = StoneBakeOutputMode.Curve,
        double surfaceHeightInches = 5)
    {
        string? optionError = ValidateOptions(outputMode, surfaceHeightInches);
        if (optionError is not null) return OperationResponse<StoneWireframeBakeResponse>.Fail(optionError);
        OperationResponse<IReadOnlyCollection<string>> validation = Validate(filePath, assemblyDefinitionNames);
        return validation.Success && validation.Data is not null
            ? _operator.Preview(filePath, validation.Data, outputMode, surfaceHeightInches)
            : OperationResponse<StoneWireframeBakeResponse>.Fail(validation.Message);
    }

    public OperationResponse<StoneWireframeBakeResponse> Apply(
        string filePath,
        IEnumerable<string>? assemblyDefinitionNames,
        StoneBakeOutputMode outputMode = StoneBakeOutputMode.Curve,
        double surfaceHeightInches = 5)
    {
        string? optionError = ValidateOptions(outputMode, surfaceHeightInches);
        if (optionError is not null) return OperationResponse<StoneWireframeBakeResponse>.Fail(optionError);
        OperationResponse<IReadOnlyCollection<string>> validation = Validate(filePath, assemblyDefinitionNames);
        return validation.Success && validation.Data is not null
            ? _operator.Apply(filePath, validation.Data, outputMode, surfaceHeightInches)
            : OperationResponse<StoneWireframeBakeResponse>.Fail(validation.Message);
    }

    private static string? ValidateOptions(StoneBakeOutputMode outputMode, double surfaceHeightInches)
    {
        if (!Enum.IsDefined(outputMode)) return "Unknown stone bake outputMode.";
        if (outputMode == StoneBakeOutputMode.WorldZSurface
            && (!double.IsFinite(surfaceHeightInches) || surfaceHeightInches <= 0))
            return "surfaceHeightInches must be finite and greater than zero for WorldZSurface.";
        return null;
    }

    private static OperationResponse<IReadOnlyCollection<string>> Validate(
        string filePath,
        IEnumerable<string>? assemblyDefinitionNames)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return OperationResponse<IReadOnlyCollection<string>>.Fail("filePath is required.");
        }

        List<string> names = (assemblyDefinitionNames ?? Array.Empty<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        string? invalid = names.FirstOrDefault(name => !StoneWireframeBakeNaming.IsAssemblyDefinitionName(name));
        if (invalid is not null)
        {
            return OperationResponse<IReadOnlyCollection<string>>.Fail(
                $"Assembly definition name does not match STN-MET-GA-...-...: {invalid}");
        }

        return OperationResponse<IReadOnlyCollection<string>>.Ok(names);
    }
}
