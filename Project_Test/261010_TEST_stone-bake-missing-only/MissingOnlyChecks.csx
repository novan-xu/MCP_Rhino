using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public static class MissingOnlyChecks
{
    public static void Run(byte[] compiledScript, string source)
    {
        Type type = Assembly.Load(compiledScript).GetTypes().Single(t => t.Name == "StoneSurfaceBake");
        var matches = type.GetMethod("ExistingAssemblyMatches").CreateDelegate<
            Func<string, string, string, string, Transform, Guid, string, Transform, double, bool>>();
        var unused = type.GetMethod("UnusedAssemblyName").CreateDelegate<Func<string, Func<string, bool>, string>>();
        var scope = type.GetMethod("ScopedAssemblyName").CreateDelegate<Func<string, IEnumerable<Guid>, string>>();
        const string assembly = "STN-MET-GA-2010C-026", block = "WF_" + assembly;
        const string owner = "GrasshopperStoneSurfaceBake/assembly-surfaces-v2";
        Guid sourceId = Guid.NewGuid(), secondSource = Guid.NewGuid();
        var sourceTransform = Transform.Identity;
        sourceTransform.M03 = 30; sourceTransform.M13 = -12; sourceTransform.M23 = 7;
        var moved = sourceTransform; moved.M03 += 100;
        string key = "assembly/" + sourceId.ToString("D");

        Check(matches(block, assembly, key, owner, sourceTransform, sourceId, assembly, sourceTransform, .001),
            "Previously tracked assembly was not skipped.");
        Check(matches("User renamed", "User edited", key, "User edited", moved, sourceId, assembly, sourceTransform, .001),
            "Tracked identity must still skip a moved/renamed/edited output.");
        Check(!matches(block, assembly, key, owner, sourceTransform, secondSource, assembly, sourceTransform, .001),
            "A new source with the same block name and placement was incorrectly suppressed.");
        Check(!matches(block, assembly, sourceId + "/member", owner, sourceTransform, sourceId, assembly, sourceTransform, .001),
            "Old individual stone tracking was mistaken for an assembly occurrence.");

        foreach (string outputName in new[] { block, scope(block, new[] { sourceId }), block + "__" + sourceId.ToString("N") })
        foreach (string tool in new[] { null, "", owner })
            Check(matches(outputName, assembly, null, tool, sourceTransform, sourceId, assembly, sourceTransform, .001),
                "Exact untracked assembly at the source placement was not skipped.");
        Check(!matches(block + "_OTHER", assembly, null, null, sourceTransform, sourceId, assembly, sourceTransform, .001),
            "A different definition name was accepted as existing output.");
        Check(!matches(block, "different assembly", null, null, sourceTransform, sourceId, assembly, sourceTransform, .001),
            "Wrong Assembly_ID was accepted.");
        Check(!matches(block, assembly, null, "another tool", sourceTransform, sourceId, assembly, sourceTransform, .001),
            "Foreign untracked output was accepted.");
        Check(!matches(block, assembly, null, null, moved, sourceId, assembly, sourceTransform, .001),
            "Name alone suppressed a source at a different location.");
        Check(!matches(null, assembly, null, null, sourceTransform, sourceId, assembly, sourceTransform, .001),
            "Nameless untracked output was accepted.");
        var scaled = sourceTransform; scaled.M00 = 2;
        Check(!matches(block, assembly, null, null, scaled, sourceId, assembly, sourceTransform, .001),
            "Wrong scale was accepted as an untracked match.");

        string scoped = scope(block, new[] { sourceId });
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { scoped, scoped + "__2", scoped + "__3" };
        Check(unused(scoped, used.Contains) == scoped + "__4", "Colliding definitions were not given a new name.");
        Check(used.Count == 3, "Name selection mutated existing definitions.");
        Check(unused(block, used.Contains) == block, "Unused preferred name was discarded.");
        Console.WriteLine("PASS: Actual linked matching skips tracked/moved outputs, distinguishes source IDs, validates untracked identity/placement, and preserves colliding names.");

        // Contract checks on the actual emitted delivery: do not execute any native document mutation.
        var root = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(kind: Microsoft.CodeAnalysis.SourceCodeKind.Script)).GetRoot();
        var implementation = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.ValueText == "StoneSurfaceBake");
        var methods = implementation.Members.OfType<MethodDeclarationSyntax>().ToDictionary(m => m.Identifier.ValueText);
        var evaluate = methods["Evaluate"];
        var apply = methods["Apply"];
        var traverse = evaluate.DescendantNodes().OfType<InvocationExpressionSyntax>().Single(i => i.Expression.ToString() == "Traverse");
        var sourceLoop = traverse.Ancestors().OfType<ForEachStatementSyntax>().First();
        Check(sourceLoop.Expression.ToString() == "missing", "Existing assemblies still enter source traversal.");
        var earlyReturn = evaluate.DescendantNodes().OfType<IfStatementSyntax>()
            .Single(i => i.Condition.ToString() == "missing.Count == 0");
        Check(earlyReturn.SpanStart < traverse.SpanStart && earlyReturn.Statement.DescendantNodes().OfType<ReturnStatementSyntax>().Any(),
            "All-existing solve must return before geometry, layers or Undo.");
        var enumerator = evaluate.DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
            .Single(i => i.Type.ToString() == "ObjectEnumeratorSettings");
        foreach (string flag in new[] { "HiddenObjects", "LockedObjects", "NormalObjects" })
            Check(enumerator.Initializer.Expressions.OfType<AssignmentExpressionSyntax>()
                .Any(a => a.Left.ToString() == flag && a.Right.ToString() == "true"), "Existing " + flag + " outputs are not indexed.");
        var fallback = evaluate.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Single(i => i.Expression.ToString() == "ExistingAssemblyMatches");
        Check(fallback.Ancestors().OfType<LambdaExpressionSyntax>().First().ToString().Contains("!consumed.Contains(o.Id)"),
            "A single untracked output could suppress multiple coincident source occurrences.");

        string[] forbidden = { "ModifyGeometry", "ModifyAttributes", "Delete", "Purge", "Replace", "Transform", "CommitChanges" };
        foreach (var call in implementation.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (call.Expression is not MemberAccessExpressionSyntax member) continue;
            string receiver = member.Expression.ToString();
            bool documentTable = receiver == "doc.Objects" || receiver == "doc.InstanceDefinitions" || receiver == "doc.Layers";
            Check(!documentTable || !forbidden.Contains(member.Name.Identifier.ValueText),
                "Missing-only script can mutate existing document data: " + call.Expression);
        }
        var writeCalls = apply.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(i => i.Expression.ToString() == "doc.InstanceDefinitions.Add" || i.Expression.ToString() == "doc.Objects.AddInstanceObject").ToArray();
        Check(writeCalls.Length == 2, "Expected only definition and instance creation paths.");
        var definitionLoop = writeCalls.Single(i => i.Expression.ToString() == "doc.InstanceDefinitions.Add")
            .Ancestors().OfType<ForEachStatementSyntax>().First();
        Check(definitionLoop.Statement.DescendantNodes().OfType<IfStatementSyntax>()
            .Any(i => i.Condition.ToString() == "d.Existing != null" && i.Statement is ContinueStatementSyntax),
            "Existing definitions are not protected by the reuse guard.");
        Check(!apply.ParameterList.Parameters.Any(p => p.Identifier.ValueText == "retired"), "Legacy output replacement remains enabled.");
        Check(!apply.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(i => i.Expression.ToString().EndsWith(".Duplicate")),
            "Apply must use fresh attributes for new outputs only.");
        Console.WriteLine("PASS: Delivery indexes hidden/locked outputs, skips before traversal, consumes untracked matches once, and has no existing-object/definition update/delete calls.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
