using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

string scriptPath = Path.GetFullPath(args[0]);
string source = File.ReadAllText(scriptPath);
string rhinoRoot = @"C:\Program Files\Rhino 8";
var ghReferences = new[] {
    rhinoRoot + @"\Plug-ins\Grasshopper\Grasshopper.dll",
    rhinoRoot + @"\Plug-ins\Grasshopper\GH_IO.dll",
    rhinoRoot + @"\Plug-ins\Grasshopper\Components\RhinoCodePluginGH.gha",
    rhinoRoot + @"\System\RhinoCodePlatform.Rhino3D.dll",
    rhinoRoot + @"\System\RhinoCodePlatform.GH.dll",
    rhinoRoot + @"\System\RhinoCodePlatform.GH1.dll",
    rhinoRoot + @"\System\Rhino.Runtime.Code.dll"
}.Select(path => MetadataReference.CreateFromFile(path));
var options = ScriptOptions.Default.AddReferences(typeof(RhinoDoc).Assembly, typeof(Enumerable).Assembly,
    typeof(StoneInputs).Assembly, typeof(System.Drawing.Color).Assembly, typeof(System.Windows.Forms.Form).Assembly)
    .AddReferences(ghReferences);
byte[] linkedAssembly = null;
foreach (Type globals in new[] { typeof(FreshStoneInputs), typeof(StoneInputs), typeof(TypedStoneInputs), typeof(LintelStoneInputs), typeof(LintelTypedStoneInputs) })
{
    var compilation = CSharpScript.Create(source, options, globals).GetCompilation();
    var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
    foreach (var error in errors) Console.Error.WriteLine(error);
    Require(errors.Length == 0, "Full external script failed compilation with " + globals.Name);
    using var output = new MemoryStream();
    var emitted = compilation.Emit(output);
    foreach (var error in emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)) Console.Error.WriteLine(error);
    Require(emitted.Success, "External script failed emit.");
    if (linkedAssembly == null) linkedAssembly = output.ToArray();
}
Console.WriteLine("PASS: Full linked script compiles/emits with fresh x/y, previous untyped/typed and lintel untyped/typed ports.");
LinkedScriptChecks.CheckNullOutputPass();
if (args.Contains("--check-undo")) UndoScopeChecks.Run(linkedAssembly);
if (args.Contains("--check-assembly-surfaces")) AssemblySurfaceChecks.Run(linkedAssembly);
if (args.Contains("--check-missing-only")) MissingOnlyChecks.Run(linkedAssembly, source);
if (args.Contains("--check-lintels")) LintelChecks.Run(linkedAssembly, source);

string bootstrapPath = args.Length > 1 && !args[1].StartsWith("--") ? Path.GetFullPath(args[1])
    : Path.Combine(Path.GetDirectoryName(scriptPath), "SetupStoneSurfaceBake.cs");
var bootstrapReferences = new[] {
    rhinoRoot + @"\Plug-ins\Grasshopper\Grasshopper.dll",
    rhinoRoot + @"\Plug-ins\Grasshopper\GH_IO.dll",
    rhinoRoot + @"\Plug-ins\Grasshopper\Components\RhinoCodePluginGH.gha",
    rhinoRoot + @"\System\RhinoCodePlatform.Rhino3D.dll",
    rhinoRoot + @"\System\RhinoCodePlatform.GH.dll",
    rhinoRoot + @"\System\RhinoCodePlatform.GH1.dll",
    rhinoRoot + @"\System\Rhino.Runtime.Code.dll"
}.Select(path => MetadataReference.CreateFromFile(path));
var bootstrapOptions = options.AddReferences(bootstrapReferences)
    .AddReferences(typeof(System.Windows.Forms.Form).Assembly);
// Rhino 8 binds the SDK-mode GH_ScriptInstance name to its concrete script base.
// A plain Roslyn compilation instead resolves the old abstract Grasshopper type.
// Supply the installed Rhino 8 binding for this offline compile; this is not a GH solve.
string bootstrapSource = File.ReadAllText(bootstrapPath).Replace(
    ": GH_ScriptInstance", ": RhinoCodePlatform.Rhino3D.Languages.GH1.Grasshopper1ScriptInstance");
var bootstrap = CSharpScript.Create(bootstrapSource, bootstrapOptions).GetCompilation();
using (var emittedBootstrap = new MemoryStream())
{
    var result = bootstrap.Emit(emittedBootstrap);
    foreach (var error in result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)) Console.Error.WriteLine(error);
    Require(result.Success, "Automatic setup script failed compilation against installed Grasshopper/RhinoCode APIs.");
}
Console.WriteLine("PASS: Complete automatic setup script compiles and emits against installed Grasshopper/RhinoCode APIs.");

var defaults = new StoneSurfaceBake.Settings(0, 5, 0, 5);
var different = new StoneSurfaceBake.Settings(-3, 8, 2.5, 4);
Require(different.FinBaseZ == -3 && different.FinHeight == 8 && different.FlatBaseZ == 2.5 && different.FlatHeight == 4,
    "Family settings are independent.");
foreach (var values in new[] { new[] { double.NaN, 5d, 0, 5 }, new[] { 0d, 0, 0, 5 },
    new[] { 0d, 5, 0, -1 }, new[] { 0d, double.PositiveInfinity, 0, 5 }, new[] { 0d, 5, double.NegativeInfinity, 5 } })
{
    bool rejected = false;
    try { new StoneSurfaceBake.Settings(values[0], values[1], values[2], values[3]); }
    catch (ArgumentException) { rejected = true; }
    Require(rejected, "Invalid numeric input was accepted.");
}
Require(StoneSurfaceBake.IsAssembly("STN-MET-GA-2010S-020"), "Current prefix rejected.");
Require(StoneSurfaceBake.IsAssembly("stn-met-ga-custom-suffix"), "Prefix scope was restricted to the old name length.");
foreach (string name in new[] { "STN-MET-GA2010S-020", "STN-MET-GA-2010S-020_EL", "STN-MET-GA-2010S-020_v", "OTHER" })
    Require(!StoneSurfaceBake.IsAssembly(name), "Excluded assembly accepted: " + name);
Require(StoneSurfaceBake.StoneFamily("STN-MET-1010M-020") == 1 && StoneSurfaceBake.StoneFamily("STN-MET-2010S-020") == 2,
    "Stone families crossed.");
Require(StoneSurfaceBake.StoneFamily("STN-MET-2010S-020_EL") == 0, "View suffix entered stone scope.");

var line = new Line(new Point3d(-7.0625, 0, 0), new Point3d(-0.3125, -0.0000676, 0));
var translated = Transform.Identity; translated.M03 = 301; translated.M13 = -55; translated.M23 = 12;
var rotated = translated; rotated.M00 = 0; rotated.M01 = -1; rotated.M10 = 1; rotated.M11 = 0;
var mirrored = translated; mirrored.M00 = -2; mirrored.M11 = 3; mirrored.M22 = -4;
var tilted = translated; tilted.M00 = 0.8; tilted.M20 = 0.6; tilted.M01 = 0.3; tilted.M22 = 7;
foreach (var transform in new[] { Transform.Identity, translated, rotated, mirrored, tilted })
foreach (double offset in new[] { -3d, 0d, 2.5d })
foreach (double height in new[] { 4d, 5d, 8d })
{
    Transform placed = StoneSurfaceBake.PlacementTransform(line, transform, offset, 0.001);
    Point3d start = line.From, end = line.To;
    start.Transform(transform); end.Transform(transform);
    start.Z += offset; end.Z += offset;
    Point3d[] expected = { start, end, new Point3d(end.X, end.Y, end.Z + height), new Point3d(start.X, start.Y, start.Z + height) };
    Point3d[] actual = { Point3d.Origin, new Point3d(line.Length, 0, 0), new Point3d(line.Length, 0, height), new Point3d(0, 0, height) };
    for (int i = 0; i < actual.Length; i++)
    {
        actual[i].Transform(placed);
        Require(actual[i].DistanceTo(expected[i]) < 1e-9, "World edge/offset/height mismatch.");
    }
    Require(StoneSurfaceBake.SameTransform(placed, placed, 0.001), "No-op comparison failed.");
    var changed = placed; changed.M23 += 0.1;
    Require(!StoneSurfaceBake.SameTransform(placed, changed, 0.001), "Changed base elevation was ignored.");
    changed = placed; changed.M00 += 0.01;
    Require(!StoneSurfaceBake.SameTransform(placed, changed, 0.001), "Changed source scale was ignored.");
}
Console.WriteLine("PASS: 45 placement combinations preserve signed offsets and independent world heights.");
bool verticalRejected = false;
try { StoneSurfaceBake.PlacementTransform(new Line(Point3d.Origin, new Point3d(0, 0, 8)), Transform.Identity, 0, .001); }
catch (InvalidOperationException) { verticalRejected = true; }
Require(verticalRejected, "World-Z edge accepted.");

Guid parent = Guid.NewGuid(), otherParent = Guid.NewGuid(), member = Guid.NewGuid(), otherMember = Guid.NewGuid();
string key = StoneSurfaceBake.OccurrenceKey(parent, new[] { member });
Require(key == StoneSurfaceBake.OccurrenceKey(parent, new[] { member }), "Occurrence key is unstable.");
Require(key != StoneSurfaceBake.OccurrenceKey(otherParent, new[] { member }), "Parent instance IDs collided.");
Require(key != StoneSurfaceBake.OccurrenceKey(parent, new[] { otherMember, member }), "Nested paths collided.");
var ids = new HashSet<Guid> { parent, member };
Require(StoneSurfaceBake.CanChangeSharedDefinition(0, new[] { parent, member }, ids), "Owned shared definition rejected.");
Require(!StoneSurfaceBake.CanChangeSharedDefinition(1, new[] { parent, member }, ids), "Nested reference could be modified.");
Require(!StoneSurfaceBake.CanChangeSharedDefinition(0, new[] { parent, otherParent }, ids), "Unmatched reference could be modified.");
const string block = "WF_STN-MET-2010S-020", assembly = "STN-MET-GA-2010S-020";
var original = Transform.Identity;
var desired = Transform.Identity; desired.M23 = -3;
Require(StoneSurfaceBake.LegacyMatches(block, assembly, null, null, original, block, assembly, original, desired, .001),
    "Previous zero-offset bake could not be adopted when the requested offset changed.");
Require(StoneSurfaceBake.LegacyMatches(block, assembly, null, null, desired, block, assembly, original, desired, .001),
    "Equivalent legacy placement could not be adopted.");
Require(!StoneSurfaceBake.LegacyMatches(block, assembly + "_EL", null, null, original, block, assembly, original, desired, .001),
    "Wrong Assembly_ID was adopted.");
Require(!StoneSurfaceBake.LegacyMatches(block, assembly, key, null, original, block, assembly, original, desired, .001),
    "Tracked output was treated as untracked legacy output.");
Require(!StoneSurfaceBake.LegacyMatches(block, assembly, null, "another tool", original, block, assembly, original, desired, .001),
    "Another tool's output was adopted.");
Require(StoneSurfaceBake.FinLayer == "01_MET-IEF-Curtain Wall::Surfaces - STN detailed::Fin"
    && StoneSurfaceBake.FlatLayer == "01_MET-IEF-Curtain Wall::Surfaces - STN detailed::Back", "Layer paths changed.");
Console.WriteLine("PASS: Validation, prefix/exclusions, occurrence IDs, legacy adoption, shared-definition ownership, layers, and no-op comparisons.");
Console.WriteLine("NOT RUN: Rhino native document mutation or an actual Grasshopper solve.");

static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

public class StoneInputs
{
    public RhinoCodePlatform.Rhino3D.Languages.GH1.Legacy.ProxyScriptEnv ghenv;
    public object FinBaseZ, FinHeight, FlatBaseZ, FlatHeight, Bake;
    public object Fins, Flats, BakedIds, Report;
}
public class TypedStoneInputs
{
    public RhinoCodePlatform.Rhino3D.Languages.GH1.Legacy.ProxyScriptEnv ghenv;
    public double FinBaseZ, FinHeight, FlatBaseZ, FlatHeight;
    public bool Bake;
    public object Fins, Flats, BakedIds, Report;
}
public class FreshStoneInputs
{
    public RhinoCodePlatform.Rhino3D.Languages.GH1.Legacy.ProxyScriptEnv ghenv;
    public object x, y, a;
}
public class LintelStoneInputs : StoneInputs
{
    public object LintelBaseZ, LintelHeight, Lintels;
}
public class LintelTypedStoneInputs : TypedStoneInputs
{
    public double LintelBaseZ, LintelHeight;
    public object Lintels;
}
