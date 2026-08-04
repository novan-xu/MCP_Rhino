using System.ComponentModel;
using MCP_Rhino.Server.Infrastructure.Reference;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Resources;

[McpServerResourceType]
public sealed class RhinoReferenceResource
{
    [McpServerResource(UriTemplate = "rhino-reference://modules", Name = "Rhino Reference Modules", MimeType = "text/markdown")]
    [Description("List curated MCP_Rhino RhinoCommon reference modules. Reference-only; does not inspect or mutate a live Rhino document.")]
    public static string ListRhinoReferenceModulesResource()
    {
        return RhinoReferenceIndex.FormatModulesMarkdown();
    }

    [McpServerResource(UriTemplate = "rhino-reference://module/{moduleName}", Name = "Rhino Reference Module", MimeType = "text/markdown")]
    [Description("Get a curated MCP_Rhino RhinoCommon reference module by name. Reference-only; does not inspect or mutate a live Rhino document.")]
    public static string GetRhinoReferenceModuleResource(string moduleName)
    {
        return RhinoReferenceIndex.FormatModuleMarkdown(moduleName);
    }

    [McpServerResource(UriTemplate = "rhino-reference://function/{functionName}", Name = "Rhino Reference Function", MimeType = "text/markdown")]
    [Description("Get curated MCP_Rhino RhinoCommon function notes by function name. Reference-only; does not inspect or mutate a live Rhino document.")]
    public static string GetRhinoReferenceFunctionResource(string functionName)
    {
        return RhinoReferenceIndex.FormatFunctionMarkdown(functionName);
    }
}
