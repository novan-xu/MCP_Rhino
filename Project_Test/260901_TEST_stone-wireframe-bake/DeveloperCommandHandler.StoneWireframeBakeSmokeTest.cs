using System.Reflection;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using MCP_Rhino.Server.Infrastructure.Plugin;
using MCP_Rhino.Server.Tools.Blocks;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string StoneWireframeBakeSlug = "stone-wireframe-bake-smoke-test";

    partial void RegisterStoneWireframeBakeHandlers()
    {
        _extensionHandlers[StoneWireframeBakeSlug] = HandleStoneWireframeBakeSmokeTest;
    }

    private bool HandleStoneWireframeBakeSmokeTest(string[] args)
    {
        try
        {
            VerifyStoneWireframeNaming();
            VerifyStoneAssemblyGaHyphenService();
            VerifyStoneWireframeToolSafety();

            string filePath = args.Length > 1 ? args[1] : "C:/mcp-rhino/stone-wireframe-bake-smoke.3dm";
            if (McpRhinoPlugin.Instance is null)
            {
                RequireStoneWireframeLiveRequired(_stoneWireframeBakeSkill.Preview(new StoneWireframeBakeRequest
                {
                    FilePath = filePath
                }), "PreviewStoneWireframeBake");
                RequireStoneWireframeLiveRequired(_stoneWireframeBakeSkill.Apply(new StoneWireframeBakeRequest
                {
                    FilePath = filePath
                }), "ApplyStoneWireframeBake");
                Console.WriteLine("[OK] stone-wireframe-bake CLI fallback smoke completed.");
            }
            else
            {
                OperationResponse<StoneWireframeBakeResponse> result = _stoneWireframeBakeSkill.Preview(
                    new StoneWireframeBakeRequest { FilePath = filePath });
                if (!result.Success || result.Data is null)
                {
                    throw new InvalidOperationException($"Live preview failed: {result.Message}");
                }

                if (result.Data.FailedCount > 0 || result.Data.AssemblyInstanceCount == 0 || result.Data.StoneDefinitionCount == 0)
                {
                    throw new InvalidOperationException(
                        $"Live preview was not clean: assemblies={result.Data.AssemblyInstanceCount}, "
                        + $"stoneDefinitions={result.Data.StoneDefinitionCount}, failures={result.Data.FailedCount}.");
                }

                Console.WriteLine(
                    $"[OK] stone-wireframe-bake live preview: assemblies={result.Data.AssemblyInstanceCount}, "
                    + $"stoneDefinitions={result.Data.StoneDefinitionCount}, occurrences={result.Data.StoneOccurrenceCount}.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Stone wireframe bake smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private static void VerifyStoneWireframeNaming()
    {
        RequireStoneWireframe(StoneWireframeBakeNaming.IsAssemblyDefinitionName("STN-MET-GA-2010S-001"), "Assembly name was rejected.");
        RequireStoneWireframe(StoneWireframeBakeNaming.IsAssemblyDefinitionName("stn-met-ga-2010s-001"), "Case-insensitive assembly name was rejected.");
        foreach (string? invalid in new string?[] { null, "", "STN-MET-GA2010S-001", "STN-MET-GA-2010S-001_EL", "STN-MET-GA-2010S-001_V", "STN-MET-GA--2010S-001", "STN-MET-GA-2010S-", "STN-MET-GA-2010S-001 01" })
        {
            RequireStoneWireframe(!StoneWireframeBakeNaming.IsAssemblyDefinitionName(invalid), $"Invalid assembly name was accepted: {invalid}");
        }
        RequireStoneWireframe(StoneWireframeBakeNaming.IsFlatStoneDefinitionName("STN-MET-1010S-001"), "Flat stone name was rejected.");
        RequireStoneWireframe(StoneWireframeBakeNaming.IsVerticalStoneDefinitionName("STN-MET-2010S-001"), "Vertical stone name was rejected.");
        RequireStoneWireframe(!StoneWireframeBakeNaming.IsStoneDefinitionName("STN-MET-6020M-008"), "Unsupported stone family was accepted.");
        RequireStoneWireframe(
            StoneWireframeBakeNaming.OutputDefinitionName("STN-MET-2010S-001") == "WF_STN-MET-2010S-001",
            "Output definition name was incorrect.");
    }

    private static void VerifyStoneWireframeToolSafety()
    {
        RequireToolSafety(
            typeof(PreviewStoneWireframeBakeTool),
            nameof(PreviewStoneWireframeBakeTool.PreviewStoneWireframeBake),
            readOnly: true);
        RequireToolSafety(
            typeof(ApplyStoneWireframeBakeTool),
            nameof(ApplyStoneWireframeBakeTool.ApplyStoneWireframeBake),
            readOnly: false);
    }

    private static void RequireToolSafety(Type type, string methodName, bool readOnly)
    {
        RequireStoneWireframe(type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null, $"{type.Name} is missing [McpServerToolType].");
        MethodInfo method = type.GetMethod(methodName) ?? throw new InvalidOperationException($"Missing method: {methodName}");
        McpServerToolAttribute tool = method.GetCustomAttribute<McpServerToolAttribute>()
            ?? throw new InvalidOperationException($"{methodName} is missing [McpServerTool].");
        RequireStoneWireframe(tool.ReadOnly == readOnly, $"{methodName} ReadOnly mismatch.");
        RequireStoneWireframe(!tool.Destructive, $"{methodName} must be non-destructive.");
        RequireStoneWireframe(!tool.OpenWorld, $"{methodName} must be closed-world.");
    }

    private static void RequireStoneWireframeLiveRequired<T>(OperationResponse<T> response, string label)
    {
        RequireStoneWireframe(
            !response.Success && string.Equals(response.Message, "LIVE_RHINO_REQUIRED", StringComparison.Ordinal),
            $"{label} should return LIVE_RHINO_REQUIRED; got Success={response.Success}, Message={response.Message}");
    }

    private static void RequireStoneWireframe(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
