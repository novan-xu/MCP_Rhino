using Jint;
using Jint.Runtime.Interop;
using MCP_Rhino.Offline.Contracts;

namespace MCP_Rhino.Offline.Infrastructure;

internal static class OfflineScriptRunner
{
    public static void Run(string script, OfflineModelBridge model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(script) || script.Length > 65536)
            throw new OfflineEditException("INVALID_SCRIPT", "Supply 1–65536 characters of JavaScript modeling script.");
        using var engine = new Engine(options =>
        {
            options.LimitMemory(32_000_000).MaxStatements(100_000)
                .TimeoutInterval(TimeSpan.FromSeconds(5)).LimitRecursion(64)
                .CancellationToken(cancellationToken).DisableStringCompilation();
            options.Interop.AllowGetType = false;
            options.Interop.AllowSystemReflection = false;
            options.Interop.AllowWrite = false;
            options.Interop.TypeResolver = new TypeResolver { MemberFilter = _ => false };
            options.Constraints.MaxArraySize = 100_000;
            options.Constraints.RegexTimeout = TimeSpan.FromMilliseconds(100);
        });
        // The only CLR projection is a string-in/string-out function. Never expose File3dm,
        // delegates' targets, exception objects, types, streams or arbitrary CLR instances.
        engine.SetValue("__modelCall", new Func<string, string, string>(model.Call));
        engine.Execute("""
            "use strict";
            const model = (() => {
                const nativeCall = __modelCall;
                const parse = JSON.parse, stringify = JSON.stringify;
                delete globalThis.__modelCall;
                const call = (name, args) => {
                    const r = parse(nativeCall(name, stringify(args)));
                    if (!r.ok) throw new Error(r.error);
                    return r.value;
                };
                return Object.freeze({
                    objects: (offset = 0, limit = 200) => call("objects", {offset, limit}),
                    layers: () => call("layers", {}),
                    documentUserText: () => call("documentUserText", {}),
                    setDocumentUserText: (key, value) => call("setDocumentUserText", {key, value}),
                    setAttributes: (ids, attributes) => call("setAttributes", {ids, attributes}),
                    transform: (ids, matrix) => call("transform", {ids, matrix}),
                    deleteObjects: ids => call("deleteObjects", {ids}),
                    addLayer: (name, color = "#000000", parentId = null) => call("addLayer", {name, color, parentId}),
                    editLayer: (id, attributes) => call("editLayer", {id, attributes}),
                    addPoint: (point, attributes = {}) => call("addPoint", {point, attributes}),
                    addLine: (start, end, attributes = {}) => call("addLine", {start, end, attributes}),
                    addPolyline: (points, attributes = {}) => call("addPolyline", {points, attributes}),
                    addSphere: (center, radius, attributes = {}) => call("addSphere", {center, radius, attributes}),
                    addBox: (min, max, attributes = {}) => call("addBox", {min, max, attributes}),
                    log: message => call("log", {message: String(message)})
                });
            })();
            """);
        engine.Execute("\"use strict\";\n" + script, "offline-edit.js");
        if (model.Failed)
            throw new OfflineEditException("SCRIPT_EDIT_FAILED", "A model operation failed, even if the script caught its error. No preview was saved.");
    }
}
