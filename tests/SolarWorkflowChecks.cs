using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Rhino;
using Rhino.Geometry;
using PlugInSolar;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Parameters;
public static class SolarWorkflowChecks {
 static int count;static void Check(bool x,string s){if(!x)throw new Exception("FAILED: "+s);count++;}static void Near(double x,double y,string s){Check(Math.Abs(x-y)<1e-7*Math.Max(1,Math.Abs(y)),s);}
 static PvLayoutBundle Layout(UnitSystem unit){double s=RhinoMath.UnitScale(UnitSystem.Meters,unit);var l=new PvLayoutResult();var f=new Plane(new Point3d(0,0,s),Vector3d.XAxis,Vector3d.YAxis);PvGeometryGeneratorComponent.Generate(1,2,.04,f,unit,1e-6*s,out var panel,out var face,out var normal,out var area);l.Panels.Add(panel);l.Faces.Add(face);l.Frames.Add(f);l.Normals.Add(normal);l.Positions.Add(f.Origin);l.Ids.Add("P1");l.AreaM2=area;return new PvLayoutBundle{Layout=l,Units=unit,Tolerance=1e-6*s};}
 public static string Run(string repositoryRoot){count=0;var sky=JsonSerializer.Deserialize<PvSolarConditions>(File.ReadAllText(Path.Combine(repositoryRoot,"tests","data","sky_fixture.json")));sky.Validate();Check(sky.Vectors.Length==145,"Radiance sky dimensions");
 var c=JsonSerializer.Deserialize<PvSolarConditions>(JsonSerializer.Serialize(sky));for(int k=0;k<c.Vectors.Length;k++)for(int j=0;j<c.Hoys.Length;j++){c.DirectW[k][j]=0;c.DiffuseW[k][j]=0;}c.DirectW[144]=new[]{1000d,500d,0d};
 PvExposureResult baseline=null;foreach(var unit in new[]{UnitSystem.Meters,UnitSystem.Millimeters,UnitSystem.Feet}){double s=RhinoMath.UnitScale(UnitSystem.Meters,unit);var b=Layout(unit);var r=PvSkyExposureEngine.Calculate(b,c,.25);Near(r.Poa[0][0],1000,"normal direct irradiance "+unit);Near(r.Poa[0][1],500,"second interval "+unit);Near(r.Irradiation[0],1.5,"period integration "+unit);Near(r.AreaM2[0],2,"area units "+unit);Check(r.CellIrradiation[0].Length==32,"grid cells "+unit);Near(r.CellIrradiation[0].Average(),r.Irradiation[0],"cell/panel agreement "+unit);Check(r.Hoys.SequenceEqual(c.Hoys),"HOY metadata "+unit);
 b.Context.Add(new Box(Plane.WorldXY,new Interval(-100*s,0),new Interval(-100*s,100*s),new Interval(3*s,3.1*s)).ToBrep());var shade=PvSkyExposureEngine.Calculate(b,c,.25);Near(shade.Poa[0][0],500,"half-panel shadow "+unit);Near(shade.ShadingReductionPct[0],50,"reduction "+unit);Check(shade.CellIrradiation[0].Min()==0&&shade.CellIrradiation[0].Max()==1.5,"local shadow retained "+unit);using(var m=PvExposureMapComponent.PanelMesh(shade,0,true,0,1.5)){Check(m.Faces.Count==32&&m.VertexColors.Distinct().Count()>1,"grid map colors "+unit);}baseline=r;}
 var partial=JsonSerializer.Deserialize<PvSolarConditions>(JsonSerializer.Serialize(c));partial.Hoys=new[]{8d,12d,16d};partial.Timestamps=new[]{"2001-01-01T08:30:00-05:00","2001-01-01T12:30:00-05:00","2001-01-01T16:30:00-05:00"};var filtered=PvSkyExposureEngine.Calculate(Layout(UnitSystem.Meters),partial,.25);Near(filtered.Irradiation[0],1.5,"selected HOY duration stays one hour");var elec=PvElectricityEngine.Calculate(filtered,PvPanelCatalog.Specifications(PvPanelCatalog.DefaultModel));Check(elec.TotalAcKwh>0,"sparse HOY electricity");Near(elec.TotalAcKwh,elec.MonthlyAcKwh.Sum(),"sparse monthly aggregation");
 var chunk=new GH_IO.Serialization.GH_LooseChunk("test");new PvExposureData(baseline).Write(chunk);var round=new PvExposureData();Check(round.Read(chunk)&&round.Data.CellIrradiation[0].Length==32,"cell results persistence");
 var numeric=new List<IGH_Goo>{new GH_Number(1),new GH_Number(2)};var numbers=PvWeatherAdapter.Numbers(numeric,out var auto,out var units);Check(numbers.SequenceEqual(new[]{1d,2d})&&auto==null,"numeric weather adapter");Check(PvWeatherAdapter.Site(new GH_String("33.749,-84.388,-5,320"))[2]==-5,"text location adapter");
 using(var d=new GH_Document()){var layout=new PvCandidateLayoutData(Layout(RhinoDoc.ActiveDoc.ModelUnitSystem));var lp=new Param_GenericObject();lp.PersistentData.Append(layout);var cp=new Param_GenericObject();cp.PersistentData.Append(new PvSolarConditionsData(c));var run=new Param_Boolean();run.PersistentData.Append(new GH_Boolean(true));var exposure=new PvSolarExposureV2Component();var map=new PvExposureMapComponent();var power=new PvElectricityComponent();var specs=new Param_GenericObject();specs.PersistentData.Append(new PvModuleSpecsData(PvPanelCatalog.Specifications(PvPanelCatalog.DefaultModel)));
 foreach(var o in new IGH_DocumentObject[]{lp,cp,run,exposure,map,power,specs})d.AddObject(o,false);exposure.Params.Input[0].AddSource(lp);exposure.Params.Input[1].AddSource(cp);exposure.Params.Input[5].AddSource(run);map.Params.Input[0].AddSource(exposure.Params.Output[0]);power.Params.Input[0].AddSource(exposure.Params.Output[0]);power.Params.Input[1].AddSource(specs);d.Enabled=true;GH_Document.EnableSolutions=true;d.NewSolution(true);
 foreach(var x in new GH_Component[]{exposure,map,power})Check(!x.RuntimeMessages(GH_RuntimeMessageLevel.Error).Any(),x.Name+": "+string.Join(";",x.RuntimeMessages(GH_RuntimeMessageLevel.Error)));Check(exposure.Params.Input.Count==6&&exposure.Params.Output.Count==8,"compact exposure interface");Check(!exposure.Params.Output.Any(x=>x.Name.Contains("HOY")||x.Name.Contains("Time")||x.Name.Contains("Map")||x.Name.Contains("Legend")),"time and visualization sockets removed");Check(map.Params.Output[0].VolatileDataCount==1,"map output");Check(new PvSolarConditionsComponent().Params.Input.Count==8,"conditions interface");
 }
 return count+" solar workflow checks passed";
 }
}
