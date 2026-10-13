using System.Reflection;
using System.Runtime.Loader;
using Grasshopper.Kernel;
using Rhino.Runtime.Code.Execution;
using RhinoCodePlatform.Rhino3D.Languages.GH1;

public static class LinkedScriptChecks
{
    public static void CheckNullOutputPass()
    {
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            string file = Path.Combine(@"C:\Program Files\Rhino 8\System", name.Name + ".dll");
            return File.Exists(file) ? Assembly.LoadFrom(file) : null;
        };
        RunCheck();
    }

    private static void RunCheck()
    {
        var data = DispatchProxy.Create<IGH_DataAccess, OutputRecorder>();
        var recorder = (OutputRecorder)(object)data;
        var context = new RunContext();
        data.SetData(3, "direct report");
        data.SetDataList(0, new[] { "direct preview" });
        int before = recorder.Writes;
        Grasshopper1Script.SetOutput(context, data, 0, "Fins");
        context.Outputs.Set("Fins", null);
        context.Outputs.Set("Report", null);
        Grasshopper1Script.SetOutput(context, data, 0, "Fins");
        Grasshopper1Script.SetOutput(context, data, 3, "Report");
        if (recorder.Writes != before) throw new Exception("Rhino's null/unset script output pass overwrote direct data-access results.");
        context.Outputs.Set("Report", "positive control");
        Grasshopper1Script.SetOutput(context, data, 3, "Report");
        if (recorder.Writes != before + 1) throw new Exception("Output recording positive control failed.");
        Console.WriteLine("PASS: Installed Rhino Script-Mode output pass preserves direct results for null/unset globals; positive control writes.");
    }
}

public class OutputRecorder : DispatchProxy
{
    public int Writes;
    protected override object Invoke(MethodInfo method, object[] args)
    {
        if (method.Name == "SetData" || method.Name == "SetDataList") { Writes++; return true; }
        throw new NotSupportedException(method.Name);
    }
}
