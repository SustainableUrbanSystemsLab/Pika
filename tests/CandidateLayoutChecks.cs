using System;
using System.Linq;
using System.Collections.Generic;
using Rhino;
using Rhino.Geometry;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using PlugInSolar;
public static class CandidateLayoutChecks {
 static int count;static void Check(bool x,string name){if(!x)throw new Exception("FAILED: "+name);count++;}static void Near(double x,double y,string name){Check(Math.Abs(x-y)<1e-7*Math.Max(1,Math.Abs(y)),name);}
 public static string Run(){count=0;
  foreach(var id in PvPanelCatalog.ModelIds){var dims=PvPanelCatalog.Dimensions(id);var spec=PvPanelCatalog.Specifications(id);dims.Validate();spec.Validate();Check(dims.WidthM>0&&spec.PowerW>0,"valid product "+id);}
  foreach(var unit in new[]{UnitSystem.Meters,UnitSystem.Millimeters,UnitSystem.Feet}){
   double s=RhinoMath.UnitScale(UnitSystem.Meters,unit),tol=1e-6*s;
   PvGeometryGeneratorComponent.Generate(1.001,1.666,.04,Plane.WorldXY,unit,tol,out var panel,out var face,out var normal,out var area);
   using(panel)using(face)using(var surface=new PlaneSurface(new Plane(new Point3d(0,0,3*s),Vector3d.ZAxis),new Interval(0,10*s),new Interval(0,8*s)).ToBrep()){
    Check(panel.IsSolid&&panel.IsValid,"valid panel "+unit);Near(area,1.001*1.666,"m² area "+unit);
    var flat=PvLayoutEngine.Generate(panel,Plane.WorldXY,surface,.1,.1,0,false,.05,.2,null,null,false,0,"Test",5000,unit,tol,false);Check(flat.Panels.Count==32,"flat count "+unit);Near(flat.AreaM2,32*area,"total area "+unit);Check(flat.Ids.Distinct().Count()==32&&flat.Faces.Count==32,"aligned outputs "+unit);
    var tilt=PvLayoutEngine.Generate(panel,Plane.WorldXY,surface,.1,.1,30,false,.05,.2,null,null,false,0,"Test",5000,unit,tol,false);Check(tilt.Panels.Count>0&&tilt.Normals[0].Z>0,"tilt "+unit);Check(tilt.Panels.All(b=>b.GetBoundingBox(true).Min.Z>=3.05*s-tol*10),"minimum mounting offset "+unit);
    var landscape=PvLayoutEngine.Generate(panel,Plane.WorldXY,surface,.1,.1,0,true,.05,.2,null,null,false,0,"Test",5000,unit,tol,false);Check(landscape.Panels.Count==30,"landscape "+unit);
    var empty=PvLayoutEngine.Generate(panel,Plane.WorldXY,surface,.1,.1,0,false,.05,5,null,null,false,0,"Test",5000,unit,tol,false);Check(empty.Panels.Count==0,"oversized setback "+unit);
    using(var blocker=new Box(Plane.WorldXY,new Interval(-s,11*s),new Interval(-s,9*s),new Interval(3.01*s,3.3*s)).ToBrep()){var blocked=PvLayoutEngine.Generate(panel,Plane.WorldXY,surface,.1,.1,0,false,.05,.2,null,new[]{blocker},false,0,"Test",5000,unit,tol,false);Check(blocked.Panels.Count==0&&blocked.ContextRejected==32,"obstacle rejection "+unit);}
    var round=new PvCandidateLayoutData(new PvLayoutBundle{Layout=flat,Units=unit,Tolerance=tol});var chunk=new GH_IO.Serialization.GH_LooseChunk("test");Check(round.Write(chunk),"package serialization "+unit);var restored=new PvCandidateLayoutData();Check(restored.Read(chunk)&&restored.Value.Layout.Panels.Count==32,"package deserialization "+unit);
   }
  }
  bool rejected=false;try{PvGeometryGeneratorComponent.Generate(-1,1,.04,Plane.WorldXY,UnitSystem.Meters,1e-6,out var p,out var f,out var n,out var a);}catch(ArgumentException){rejected=true;}Check(rejected,"invalid dimensions rejected");
  double ds=RhinoMath.UnitScale(UnitSystem.Meters,RhinoDoc.ActiveDoc.ModelUnitSystem);
  using(var doc=new GH_Document())using(var surface=new PlaneSurface(new Plane(new Point3d(0,0,3*ds),Vector3d.ZAxis),new Interval(0,10*ds),new Interval(0,8*ds)).ToBrep()){
   var select=new SelectPvPanelComponent();var geometry=new PvGeometryGeneratorComponent();var layout=new PvCandidateLayoutComponent();var mount=new Param_Brep();mount.PersistentData.Append(new GH_Brep(surface));foreach(var o in new IGH_DocumentObject[]{select,geometry,layout,mount})doc.AddObject(o,false);geometry.Params.Input[4].AddSource(select.Params.Output[0]);layout.Params.Input[0].AddSource(geometry.Params.Output[5]);layout.Params.Input[1].AddSource(mount);doc.Enabled=true;GH_Document.EnableSolutions=true;doc.NewSolution(true);
   foreach(var c in new GH_Component[]{select,geometry,layout})Check(!c.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any(),c.Name+" solves");Check(layout.Params.Output[1].VolatileDataCount>0,"connected chain generates panels");Check(layout.Params.Input.Count==9&&layout.Params.Output.Count==9,"candidate interface");
  }
  return count+" candidate-layout checks passed";
 }
}
