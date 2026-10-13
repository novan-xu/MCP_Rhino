using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Scripting;
using Rhino;
using System.Text.Json;
using MCP_Rhino.Offline.Contracts;

namespace MCP_Rhino.RhinoWorker.Infrastructure;

public sealed class RhinoScriptGlobals(RhinoDoc document, JsonElement parameters)
{
    public RhinoDoc doc { get; } = document;
    public JsonElement Parameters { get; } = parameters;
    public object? Result { get; set; }
    public List<string> Messages { get; } = [];
    public void Log(string message)
    {
        if (Messages.Count >= 100 || message.Length > 4096) throw new InvalidOperationException("Script log limit exceeded.");
        Messages.Add(message);
    }
}

internal static class RhinoScriptCompiler
{
    public static RhinoScriptGlobals Execute(RhinoDoc doc, RhinoWorkerRequest request)
    {
        string library = "";
        if (request.LibrarySource is not null)
        {
            var tree = CSharpSyntaxTree.ParseText(request.LibrarySource, new CSharpParseOptions(kind: SourceCodeKind.Script));
            var matches = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
                .Where(node => node.Identifier.ValueText == request.LibraryType).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("Library must contain exactly one class named " + request.LibraryType);
            // Do not execute a GH top-level entry point or its component setup class.
            library = matches[0].ToFullString();
        }
        string code = library + "\n" + request.Script;
        var syntax = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(kind: SourceCodeKind.Script));
        if (syntax.GetRoot().DescendantNodes().OfType<AwaitExpressionSyntax>().Any())
            throw new InvalidOperationException("Rhino scripts must be synchronous on the worker's STA thread; await is unsupported.");
        var options = ScriptOptions.Default
            .AddReferences(typeof(RhinoDoc).Assembly, typeof(RhinoScriptGlobals).Assembly,
                typeof(JsonElement).Assembly, typeof(Enumerable).Assembly, typeof(System.Drawing.Color).Assembly)
            .AddImports("System", "System.Linq", "System.Collections.Generic", "System.Globalization", "System.Text.Json",
                "Rhino", "Rhino.Geometry", "Rhino.DocObjects");
        var script = CSharpScript.Create(code, options, typeof(RhinoScriptGlobals));
        var errors = script.Compile().Where(d => d.Severity == DiagnosticSeverity.Error).Take(12).ToArray();
        if (errors.Length > 0) throw new InvalidOperationException(string.Join("\n", errors.Select(e => e.ToString())));
        var globals = new RhinoScriptGlobals(doc, request.Parameters);
        script.RunAsync(globals).GetAwaiter().GetResult();
        return globals;
    }
}
