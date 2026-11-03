using System;
using System.Drawing;
using System.Reflection;
using Grasshopper.Kernel;
namespace PlugInSolar {
static class Assets {
 public static Bitmap Icon(string id){using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("PlugInSolar.Assets."+id+"-24.png"))using(var b=new Bitmap(s))return new Bitmap(b);}
}
public sealed class Info:GH_AssemblyInfo {
 public override string Name=>"Plug-In Solar";
 public override string Description=>"PV product selection, geometry, candidate layouts and experimental solar-performance workflow for Rhino 8.";
 public override Guid Id=>new Guid("ba3e852e-04f2-46a3-933b-fef28736aeee");
 public override Bitmap Icon=>Assets.Icon("plug_in_solar");
 public override string AuthorName=>"Gayeon Lee, Patrick Kastner";
 public override string AuthorContact=>"";
}
}
