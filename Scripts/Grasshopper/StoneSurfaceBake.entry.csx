// #! csharp
// LINK THIS FILE through Rhino 8 C# Script's special script input (Input is Path).
// A fresh component's x/y ports configure themselves on the first solve.
// The file connection stays in place. No code needs to be pasted into the editor.
// Inputs (Item): FinBaseZ=0, FinHeight=5, FlatBaseZ=0, FlatHeight=5, Bake=false.
// Outputs: Fins, Flats, BakedIds, Report. All numeric values are inches.
#r "C:/Program Files/Rhino 8/Plug-ins/Grasshopper/Components/RhinoCodePluginGH.gha"
#r "C:/Program Files/Rhino 8/System/RhinoCodePlatform.Rhino3D.dll"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Rhino;
using Rhino.Geometry;
using Rhino.DocObjects;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using RhinoCodePluginGH.Components;
using RhinoCodePluginGH.Parameters;

// ghenv is supplied by Rhino's Script-Mode, even before our named ports exist.
// DataAccessManager avoids referencing missing FinBaseZ/Fins/etc. globals during setup.
LiveStoneSurfaceComponent.Run(ghenv.Component as CSharpComponent, ghenv.DataAccessManager);
