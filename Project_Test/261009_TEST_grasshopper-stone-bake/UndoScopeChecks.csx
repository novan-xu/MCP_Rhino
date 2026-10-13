using System.Reflection;

// Exercise the actual supplied delivery script's emitted class, without running its entry point.
public static class UndoScopeChecks
{
    public static void Run(byte[] compiledScript)
    {
        Type scopeType = Assembly.Load(compiledScript).GetTypes().Single(t => t.Name == "BakeUndoScope");
        foreach (string scenario in new[] { "owned", "borrowed", "disabled", "begin-failed",
            "became-active", "no-writes", "end-failed", "rollback-failed" })
        {
            bool active = scenario == "borrowed";
            int begins = 0, ends = 0, undos = 0;
            uint endedSerial = 0;
            Func<bool> isActive = () => active;
            Func<bool> isEnabled = () => scenario != "disabled";
            Func<uint> begin = () => {
                begins++;
                if (scenario == "became-active") { active = true; return 0; }
                return scenario == "begin-failed" ? 0U : 42U;
            };
            object scope = null;
            Exception failure = null;
            try { scope = Activator.CreateInstance(scopeType, isActive, isEnabled, begin); }
            catch (TargetInvocationException error) { failure = error.InnerException; }
            bool mustReject = scenario == "disabled" || scenario == "begin-failed";
            Check((failure is InvalidOperationException) == mustReject, scenario + ": recording gate");
            Check(begins == (scenario == "borrowed" || scenario == "disabled" ? 0 : 1), scenario + ": begin count");
            if (mustReject) continue;

            Func<uint, bool> end = serial => { ends++; endedSerial = serial; return scenario != "end-failed"; };
            MethodInfo endMethod = scopeType.GetMethod("End");
            endMethod.Invoke(scope, new object[] { end });
            endMethod.Invoke(scope, new object[] { end });
            bool borrowed = scenario == "borrowed" || scenario == "became-active";
            Check(ends == (borrowed ? 0 : 1), scenario + ": close only own record once");
            if (!borrowed) Check(endedSerial == 42, scenario + ": correct record serial");
            Check(undos == 0, scenario + ": success must not undo");

            Func<bool> undo = () => { undos++; return scenario != "rollback-failed"; };
            string message = (string)scopeType.GetMethod("Recover").Invoke(scope,
                new object[] { scenario != "no-writes", undo });
            bool shouldUndo = !borrowed && scenario != "no-writes" && scenario != "end-failed";
            Check(undos == (shouldUndo ? 1 : 0), scenario + ": rollback ownership");
            if (borrowed) Check(message.Contains("Partial bake changes remain"), scenario + ": partial-write disclosure");
            if (scenario == "no-writes") Check(message.Contains("No bake changes"), scenario + ": no-write disclosure");
            if (scenario == "end-failed") Check(message.Contains("did not close"), scenario + ": close failure disclosure");
            if (scenario == "rollback-failed") Check(message.Contains("Rollback failed"), scenario + ": rollback failure disclosure");
        }
        Console.WriteLine("PASS: Actual linked-script Undo ownership, borrowed records, disabled/busy rejection, no-write failure, and rollback guards (8 scenarios).");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
