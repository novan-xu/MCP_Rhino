extern alias rhinocommon;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Scripting;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live.Scripting;

internal static class LiveRhinoScriptCompiler
{
    internal static (ScriptRunner<object?> Runner, string[] Diagnostics) Compile(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(kind: SourceCodeKind.Script));
        var root = tree.GetRoot();
        if (root.DescendantTrivia(descendIntoTrivia: true).Any(t => t.GetStructure() is ReferenceDirectiveTriviaSyntax or LoadDirectiveTriviaSyntax))
            throw new InvalidOperationException("SCRIPT_DIRECTIVES_UNSUPPORTED: inline code only; #r and #load are not supported.");
        if (root.DescendantNodes().OfType<AwaitExpressionSyntax>().Any() || root.DescendantTokens().Any(t => t.IsKind(SyntaxKind.AsyncKeyword)))
            throw new InvalidOperationException("SCRIPT_ASYNC_UNSUPPORTED: Rhino code must run synchronously on its main thread.");
        var references = new[] { typeof(RhinoDoc).Assembly, typeof(LiveRhinoScriptGlobals).Assembly,
            typeof(JsonElement).Assembly, typeof(Enumerable).Assembly, typeof(System.Drawing.Color).Assembly };
        var options = ScriptOptions.Default
            .AddReferences(references)
            .AddImports("System", "System.Linq", "System.Collections.Generic", "System.Globalization", "System.Text.Json",
                "Rhino", "Rhino.Geometry", "Rhino.DocObjects");
        var script = CSharpScript.Create<object?>(source, options, typeof(LiveRhinoScriptGlobals));
        var compilation = script.GetCompilation().WithAssemblyName("MCP_Rhino.Script." + Guid.NewGuid().ToString("N"));
        using var stream = new MemoryStream();
        var emitted = compilation.Emit(stream);
        var diagnostics = emitted.Diagnostics;
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(16).ToArray();
        if (errors.Length > 0) throw new InvalidOperationException("SCRIPT_COMPILE_FAILED: " + string.Join("\n", errors.Select(d => d.ToString())));
        // Roslyn's default script loader creates a non-collectible assembly, which cannot
        // reference our collectible plug-in runtime. Emit into that runtime's own context
        // so the supplied globals, RhinoCommon and isolated JSON retain their identities.
        stream.Position = 0;
        var context = AssemblyLoadContext.GetLoadContext(typeof(LiveRhinoScriptGlobals).Assembly)!;
        var assembly = context.LoadFromStream(stream);
        var entry = compilation.GetEntryPoint(default)!;
        var factory = assembly.GetType(entry.ContainingType.MetadataName, true)!
            .GetMethod(entry.MetadataName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!
            .CreateDelegate<Func<object[], Task<object?>>>();
        ScriptRunner<object?> runner = (globals, cancellation) =>
        {
            cancellation.ThrowIfCancellationRequested();
            return factory([globals, null!]);
        };
        return (runner, diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).Take(16).Select(d => d.ToString()).ToArray());
    }
}
