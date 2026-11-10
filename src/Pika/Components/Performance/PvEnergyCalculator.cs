using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace PlugInSolar {
public sealed partial class PvEnergyCalculatorComponent : GH_Component {
    public PvEnergyCalculatorComponent():base("PV Energy Calculator","PV Energy","Estimates shaded hourly and period DC generation from EPW weather and a panel layout. Required inputs: Candidate Layout, Weather and Module Specifications. Enable Run to calculate. Exact grid calculation starts after inputs settle for 350 ms; new inputs cancel previous work. Results remain empty until the current calculation completes.","Pika","02 Performance") {}
    public override Guid ComponentGuid=>new Guid("b2eaed5c-05c4-4091-8e17-e286f03b35cf");
    protected override Bitmap Icon=>Assets.Icon("pv_power");

    protected override void RegisterInputParams(GH_InputParamManager p) {
        p.AddGenericParameter("Candidate Layout","Layout","PV Candidate Layout output, with rectangular active faces, IDs and existing context.",GH_ParamAccess.item);
        p.AddGenericParameter("Weather","Weather","PV Weather output with hourly data and the selected period.",GH_ParamAccess.item);
        p.AddGenericParameter("Module Specifications","Specs","Select PV Panel output. Apply the same module specification to every panel in this layout.",GH_ParamAccess.item);
        int context=p.AddGeometryParameter("Additional Context","Context","Optional Rhino geometry in document units. Breps, meshes, surfaces, extrusions, SubDs and blocks cast opaque shadows. Curves, points and annotations are ignored with a warning. Vegetation requires surface or mesh geometry.",GH_ParamAccess.list);
        p[context].Optional=true;
        p.AddNumberParameter("North","North","Clockwise degrees from model +Y to true north.",GH_ParamAccess.item,0);
        p.AddNumberParameter("Grid Size","Grid","Maximum rectangular surface sample size, m.",GH_ParamAccess.item,.1);
        p.AddNumberParameter("Albedo","Albedo","Uniform ground reflectance, 0–1.",GH_ParamAccess.item,.2);
        p.AddNumberParameter("Thermal U0","U0","Faiman constant heat-loss coefficient, W/(m² K). Default 25 for the baseline ventilated model.",GH_ParamAccess.item,25);
        p.AddNumberParameter("Thermal U1","U1","Faiman wind-dependent coefficient, W/(m² K)/(m/s). Default 6.84.",GH_ParamAccess.item,6.84);
        p.AddBooleanParameter("Run","Run","True runs the calculation on each Grasshopper solution with changed inputs.",GH_ParamAccess.item,false);
        int losses=p.AddGenericParameter("Losses","Losses","Optional PV Loss Assumptions. Only Soiling is applied here. DC/AC wiring losses are applied in PV AC Calculator.",GH_ParamAccess.item);p[losses].Optional=true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p) {
        p.AddNumberParameter("Total DC Energy","DC kWh","Sum of module DC energy over the selected period, kWh.",GH_ParamAccess.item);
        p.AddTextParameter("Panel DC Energy","Panel DC","Panel ID; period DC energy in kWh. Structured numeric power and IDs remain in DC Results.",GH_ParamAccess.list);
        p.AddTextParameter("Timestamps","Time","Local standard interval starts, MM/DD HH:mm, aligned with hourly branch values.",GH_ParamAccess.list);
        p.AddTextParameter("Hourly Panel DC Power","DC Time","Date-labeled DC power, MM/dd HH:mm | W. Branch {panel index}. Use DC Results to pass structured numeric data to PV AC Calculator.",GH_ParamAccess.tree);
        p.AddGenericParameter("Solar Exposure Results","Exposure","Hourly panel-average irradiance in W/m² and cumulative cell irradiation in kWh/m². Connect to PV Exposure Map for color mesh and legend.",GH_ParamAccess.item);
        p.AddTextParameter("Report","Report","Period, UTC offset, model assumptions and limitations.",GH_ParamAccess.item);
        p.AddGenericParameter("DC Results","DC Results","Structured module DC power, interval starts, panel IDs, module ratings and applied soiling. Connect directly to PV AC Calculator.",GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da) {
        Message=null;
        try {
            bool run=false;da.GetData(9,ref run);
            if(!run){CancelJobs();Message="Set Run = True";return;}
            IGH_Goo layout=null,weather=null,specifications=null;
            if(!da.GetData(0,ref layout)||!da.GetData(1,ref weather)||!da.GetData(2,ref specifications))return;
            var bundle=PvPanelCatalog.Unwrap<PvCandidateLayoutData>(layout).Value;
            var w=PvPanelCatalog.Unwrap<PvWeatherData>(weather).Data;
            var specs=PvPanelCatalog.Unwrap<PvModuleSpecsData>(specifications).Data;
            var extra=new List<IGH_GeometricGoo>();da.GetDataList(3,extra);
            double north=0,grid=.1,albedo=.2,u0=25,u1=6.84;
            da.GetData(4,ref north);da.GetData(5,ref grid);da.GetData(6,ref albedo);da.GetData(7,ref u0);da.GetData(8,ref u1);
            PvSeries.Check(north,"North",-360,360);PvSeries.Check(grid,"Grid Size",.001);PvSeries.Check(albedo,"Albedo",0,1);
            PvSeries.Check(u0,"Thermal U0",double.Epsilon);PvSeries.Check(u1,"Thermal U1",0);
            IGH_Goo lossInput=null;
            var losses=new PvLossSettings();
            if(da.GetData(10,ref lossInput))losses=PvPanelCatalog.Unwrap<PvLossData>(lossInput).Data;
            if(Params.Input[10].SourceCount>0&&lossInput==null)
                throw new ArgumentException("Connected Losses has no valid data. Resolve its upstream errors.");
            var warnings=new List<string>();
            var exposure=RequestExposure(da.Iteration,bundle,w,extra,north,grid,albedo,warnings);
            if(exposure==null)return;
            if(warnings.Count>0)AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                string.Join("\n",warnings.Take(5))+(warnings.Count>5?$"\n{warnings.Count-5} more non-surface items ignored.":""));
            double[][] power=exposure.Poa.Select(p=>p.Select((g,t)=>{
                double temp=w.AirTempC[t]+g/(u0+u1*w.WindMS[t]);
                return Math.Max(0,specs.PowerW*g*(1-losses.SoilingPct/100)/1000*(1+specs.GammaPmaxPctPerC/100*(temp-25)));
            }).ToArray()).ToArray();
            var energy=power.Select(p=>p.Sum()/1000).ToArray();
            var labels=PvSeries.ValidateTime(w.Timestamps,1).Select(t=>t.ToString("MM/dd HH:mm",CultureInfo.InvariantCulture));
            da.SetData(0,energy.Sum());da.SetDataList(1,energy.Select((value,i)=>exposure.Ids[i]+"; "+value.ToString("0.###",CultureInfo.InvariantCulture)+" kWh"));da.SetDataList(2,labels);
            da.SetDataTree(3,PvHourlyDisplay.Power(power,w.Timestamps));da.SetData(4,new PvExposureData(exposure));
            da.SetData(5,$"{w.Timestamps.First()} to {w.Timestamps.Last()} (hourly starts). {exposure.Report} Exposure cache: {(exposureCacheHit?"hit":"miss")}. Faiman U0={u0}, U1={u1}; PVWatts DC uses POA as effective irradiance. Soiling={losses.SoilingPct}%. DC is before wiring and inverter losses. Connect DC Results to PV AC Calculator for AC and electrical losses. IAM and electrical mismatch are outside this prototype.");
            da.SetData(6,new PvDcData(new PvDcResult {
                PanelIds=exposure.Ids,IntervalStarts=w.Timestamps,StepHours=1,
                PanelPowerW=power,Module=specs,SoilingPct=losses.SoilingPct
            }));
            Message=$"{energy.Sum():0.##} kWh DC"+(exposureCacheHit?" (cached)":"");
        }catch(Exception e){CancelJobs();foreach(var output in Params.Output)output.ClearData();AddRuntimeMessage(GH_RuntimeMessageLevel.Error,e.Message);}
    }

    internal static PvExposureResult CalculateExposure(PvExposureSnapshot bundle,PvWeather w,Mesh mesh,double north,double grid,double albedo,List<string> warnings,CancellationToken token,int workers) {
        var clock=Stopwatch.StartNew();
        var layout=bundle.Layout;
        int panels=layout.Ids.Length,hours=w.Timestamps.Length;
        if(panels==0)throw new ArgumentException("Candidate Layout has no panels.");
        double scale=RhinoMath.UnitScale(UnitSystem.Meters,bundle.Units);
        double rad=Math.PI/180;
        var sun=w.Elevation.Select((e,i)=>{
            double az=(w.Azimuth[i]+north)*rad,c=Math.Cos(e*rad);
            return new Vector3d(c*Math.Sin(az),c*Math.Cos(az),Math.Sin(e*rad));
        }).ToArray();
        var r=new PvExposureResult {
            Ids=layout.Ids.ToArray(),Timestamps=w.Timestamps.Select(t=>DateTimeOffset.Parse(t,CultureInfo.InvariantCulture).AddMinutes(30).ToString("o")).ToArray(),StepHours=1,
            AreaM2=new double[panels],TiltDeg=new double[panels],AzimuthDeg=new double[panels],Poa=new double[panels][],ReferencePoa=new double[panels][],
            BeamVisibility=new double[panels][],SkyVisibility=new double[panels],GroundVisibility=new double[panels],Irradiation=new double[panels],
            ShadingReductionPct=new double[panels],CellIrradiation=new double[panels][],CellVertices=new double[panels][][],CellAreas=new double[panels][],
            AirTempC=w.AirTempC,WindMS=w.WindMS
        };
        // Each worker owns a mesh and its native ray-intersection acceleration cache.
        var meshes=new Mesh[workers];
        try {
        for(int k=0;k<workers;k++){token.ThrowIfCancellationRequested();meshes[k]=mesh.DuplicateMesh();}
        long rays=0;
        for(int i=0;i<panels;i++) {
            Plane frame=layout.Frames[i];
            Vector3d normal=frame.ZAxis;
            var box=layout.Bounds[i];
            double width=box.Max.X-box.Min.X,height=box.Max.Y-box.Min.Y;
            int nx=(int)Math.Ceiling(width/(grid*scale)),ny=(int)Math.Ceiling(height/(grid*scale));
            if(nx<1||ny<1||(long)nx*ny>1600)throw new ArgumentException("Increase Grid Size. The limit is 1600 samples per panel.");
            int count=nx*ny;
            var beam=sun.Select((s,t)=>w.Elevation[t]>0?w.Dni[t]*Math.Max(0,normal*s):0).ToArray();
            var daylight=Enumerable.Range(0,hours).Where(t=>beam[t]>0).ToArray();
            rays+=(long)count*daylight.Length;
            token.ThrowIfCancellationRequested();
            double sky=(1+normal.Z)/2,ground=(1-normal.Z)/2;
            var diffuse=w.Dhi.Select((d,t)=>d*sky+albedo*w.Ghi[t]*ground).ToArray();
            var visible=new int[hours];
            r.AreaM2[i]=width*height/(scale*scale);
            r.TiltDeg[i]=Math.Acos(Math.Clamp(normal.Z,-1,1))/rad;
            r.AzimuthDeg[i]=(Math.Atan2(normal.X,normal.Y)/rad-north+720)%360;
            r.SkyVisibility[i]=r.GroundVisibility[i]=1;
            r.CellAreas[i]=Enumerable.Repeat(r.AreaM2[i]/count,count).ToArray();
            r.CellIrradiation[i]=new double[count];r.CellVertices[i]=new double[count][];
            double diffuseSum=diffuse.Sum();
            Parallel.For(0,workers,new ParallelOptions {CancellationToken=token,MaxDegreeOfParallelism=workers},worker=>{
              var localVisible=new int[hours];
              for(int cell=worker;cell<count;cell+=workers) {
                token.ThrowIfCancellationRequested();int x=cell/ny,y=cell%ny;
                double x0=box.Min.X+x*width/nx,x1=x0+width/nx,y0=box.Min.Y+y*height/ny,y1=y0+height/ny;
                r.CellVertices[i][cell]=new[]{frame.PointAt(x0,y0),frame.PointAt(x1,y0),frame.PointAt(x1,y1),frame.PointAt(x0,y1)}.SelectMany(p=>new[]{p.X,p.Y,p.Z}).ToArray();
                var point=frame.PointAt((x0+x1)/2,(y0+y1)/2)+normal*Math.Max(.001*scale,bundle.Tolerance);
                double sum=diffuseSum;
                foreach(int t in daylight) {
                    if((t&63)==0)token.ThrowIfCancellationRequested();
                    if(Intersection.MeshRay(meshes[worker],new Ray3d(point,sun[t]))>=0)continue;
                    localVisible[t]++;
                    sum+=beam[t];
                }
                r.CellIrradiation[i][cell]=sum/1000;
            }
              lock(visible){for(int t=0;t<hours;t++)visible[t]+=localVisible[t];}
            });
            r.BeamVisibility[i]=visible.Select((v,t)=>beam[t]>0?(double)v/count:1).ToArray();
            r.Poa[i]=diffuse.Select((d,t)=>d+beam[t]*r.BeamVisibility[i][t]).ToArray();
            r.ReferencePoa[i]=diffuse.Select((d,t)=>d+beam[t]).ToArray();
            r.Irradiation[i]=r.Poa[i].Sum()/1000;
            double reference=r.ReferencePoa[i].Sum()/1000;
            r.ShadingReductionPct[i]=reference>0?Math.Max(0,100*(1-r.Irradiation[i]/reference)):0;
        }
        r.Report=$"Exact grid {grid:0.###} m; {workers} workers; {clock.Elapsed.TotalSeconds:0.###} s tracing/aggregation. {panels} panels, {hours} hours, {rays:N0} direct-light samples; {mesh.Faces.Count:N0} obstruction mesh faces; {warnings.Count} non-surface items ignored. Rhino geometry shades direct beam. Isotropic sky and uniform ground terms assume unobstructed views.";
        r.Validate();
        return r;
        }finally{foreach(var owned in meshes)owned?.Dispose();}
    }
}
}
