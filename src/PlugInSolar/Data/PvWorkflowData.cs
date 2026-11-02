using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace PlugInSolar {
// Typed geometry packages keep IDs, frames and context together across the workflow.
public sealed class PvPanelTemplateData : GH_Goo<Tuple<Brep,Plane>> {
    public PvPanelTemplateData() { }
    public PvPanelTemplateData(Brep panel,Plane frame){Value=Tuple.Create(panel.DuplicateBrep(),frame);}
    public override bool IsValid=>Value!=null&&Value.Item1!=null&&Value.Item1.IsValid&&Value.Item2.IsValid;
    public override string TypeName=>"PV Panel Template";
    public override string TypeDescription=>"Panel solid and its front-center frame";
    public override IGH_Goo Duplicate()=>IsValid?new PvPanelTemplateData(Value.Item1,Value.Item2):new PvPanelTemplateData();
    public override string ToString()=>IsValid?"PV panel template":"Invalid PV template";
    public override bool Write(GH_IO.Serialization.GH_IWriter w){if(!IsValid)return false;new GH_Brep(Value.Item1).Write(w.CreateChunk("panel"));new GH_Plane(Value.Item2).Write(w.CreateChunk("frame"));return true;}
    public override bool Read(GH_IO.Serialization.GH_IReader r){var b=new GH_Brep();var p=new GH_Plane();if(!b.Read(r.FindChunk("panel"))||!p.Read(r.FindChunk("frame")))return false;Value=Tuple.Create(b.Value,p.Value);return IsValid;}
}
public sealed class PvLayoutBundle {
    public PvLayoutResult Layout;
    public List<Brep> Context=new List<Brep>();
    public Rhino.UnitSystem Units;
    public double Tolerance;
}
public sealed class PvCandidateLayoutData : GH_Goo<PvLayoutBundle> {
    public PvCandidateLayoutData() { }
    public PvCandidateLayoutData(PvLayoutBundle b){Value=b;}
    public override bool IsValid=>Value?.Layout!=null&&Value.Layout.Panels.Count==Value.Layout.Faces.Count&&Value.Layout.Panels.Count==Value.Layout.Ids.Count;
    public override string TypeName=>"PV Candidate Layout";
    public override string TypeDescription=>"Candidate geometry, panel IDs, units and collision/shading context";
    public override string ToString()=>IsValid?$"PV candidate layout: {Value.Layout.Panels.Count} panels":"Invalid PV layout";
    public override IGH_Goo Duplicate(){if(!IsValid)return new PvCandidateLayoutData();var s=Value.Layout;var r=new PvLayoutResult{AreaM2=s.AreaM2,MountNormal=s.MountNormal,SurfaceType=s.SurfaceType,Tried=s.Tried,BoundaryRejected=s.BoundaryRejected,ContextRejected=s.ContextRejected,Limited=s.Limited};r.Panels.AddRange(s.Panels.Select(b=>b.DuplicateBrep()));r.Faces.AddRange(s.Faces.Select(b=>b.DuplicateBrep()));r.Frames.AddRange(s.Frames);r.Normals.AddRange(s.Normals);r.Positions.AddRange(s.Positions);r.Ids.AddRange(s.Ids);r.Notes.AddRange(s.Notes);return new PvCandidateLayoutData(new PvLayoutBundle{Layout=r,Units=Value.Units,Tolerance=Value.Tolerance,Context=Value.Context.Select(b=>b.DuplicateBrep()).ToList()});}
    public override bool Write(GH_IO.Serialization.GH_IWriter w){
        if(!IsValid)return false;w.SetInt32("units",(int)Value.Units);w.SetDouble("tolerance",Value.Tolerance);w.SetDouble("area",Value.Layout.AreaM2);w.SetInt32("count",Value.Layout.Panels.Count);w.SetInt32("contextCount",Value.Context.Count);
        for(int i=0;i<Value.Layout.Panels.Count;i++){var c=w.CreateChunk("item",i);c.SetString("id",Value.Layout.Ids[i]);new GH_Brep(Value.Layout.Panels[i]).Write(c.CreateChunk("panel"));new GH_Brep(Value.Layout.Faces[i]).Write(c.CreateChunk("face"));new GH_Plane(Value.Layout.Frames[i]).Write(c.CreateChunk("frame"));}
        for(int i=0;i<Value.Context.Count;i++)new GH_Brep(Value.Context[i]).Write(w.CreateChunk("context",i));return true;
    }
    public override bool Read(GH_IO.Serialization.GH_IReader r){
        var l=new PvLayoutResult{AreaM2=r.GetDouble("area")};Value=new PvLayoutBundle{Layout=l,Units=(Rhino.UnitSystem)r.GetInt32("units"),Tolerance=r.GetDouble("tolerance")};
        for(int i=0;i<r.GetInt32("count");i++){var c=r.FindChunk("item",i);var b=new GH_Brep();var f=new GH_Brep();var p=new GH_Plane();if(!b.Read(c.FindChunk("panel"))||!f.Read(c.FindChunk("face"))||!p.Read(c.FindChunk("frame")))return false;l.Panels.Add(b.Value);l.Faces.Add(f.Value);l.Frames.Add(p.Value);l.Positions.Add(p.Value.Origin);l.Normals.Add(p.Value.ZAxis);l.Ids.Add(c.GetString("id"));}
        for(int i=0;i<r.GetInt32("contextCount");i++){var b=new GH_Brep();if(!b.Read(r.FindChunk("context",i)))return false;Value.Context.Add(b.Value);}return IsValid;
    }
}
public sealed class PvExposureResult {
    public string[] Ids {get;set;}
    public string[] Timestamps {get;set;}
    public double StepHours {get;set;}
    public double[] AreaM2 {get;set;}
    public double[] TiltDeg {get;set;}
    public double[] AzimuthDeg {get;set;}
    public double[][] Poa {get;set;}
    public double[][] ReferencePoa {get;set;}
    public double[][] BeamVisibility {get;set;}
    public double[] SkyVisibility {get;set;}
    public double[] GroundVisibility {get;set;}
    public double[] Irradiation {get;set;}
    public double[] ShadingReductionPct {get;set;}
    public double[] AirTempC {get;set;}
    public double[] WindMS {get;set;}
    public double[] Hoys {get;set;}
    public double[][] CellIrradiation {get;set;}
    public double[][][] CellVertices {get;set;}
    public double[][] CellAreas {get;set;}
    public string Report {get;set;}
    public void Validate(){
        int n=Ids?.Length??0,t=Timestamps?.Length??0;if(n==0||t==0||Ids.Distinct().Count()!=n)throw new ArgumentException("Exposure needs unique panel IDs and nonempty timestamps.");
        PvSeries.ValidateTime(Timestamps,StepHours,Hoys!=null);
        if(AreaM2?.Length!=n||TiltDeg?.Length!=n||AzimuthDeg?.Length!=n||Poa?.Length!=n||ReferencePoa?.Length!=n||BeamVisibility?.Length!=n||SkyVisibility?.Length!=n||GroundVisibility?.Length!=n||Irradiation?.Length!=n||ShadingReductionPct?.Length!=n)throw new ArgumentException("Exposure panel data are misaligned.");
        for(int i=0;i<n;i++){PvSeries.Check(AreaM2[i],"Panel area",double.Epsilon);PvSeries.Array(Poa[i],t,"POA",0);PvSeries.Array(ReferencePoa[i],t,"reference POA",0);PvSeries.Array(BeamVisibility[i],t,"beam visibility",0,1);}
        if(Hoys!=null) {PvSeries.Array(Hoys,t,"HOYs",0,8759);for(int j=1;j<t;j++)if(Hoys[j]<=Hoys[j-1])throw new ArgumentException("Exposure HOYs must be unique and increasing.");}
        if(CellIrradiation!=null||CellVertices!=null||CellAreas!=null){
            if(CellIrradiation?.Length!=n||CellVertices?.Length!=n||CellAreas?.Length!=n)throw new ArgumentException("Cell results are misaligned.");
            for(int i=0;i<n;i++){int m=CellIrradiation[i]?.Length??0;if(m==0||CellVertices[i]?.Length!=m)throw new ArgumentException("Missing cell geometry.");PvSeries.Array(CellIrradiation[i],m,"Cell irradiation",0);PvSeries.Array(CellAreas[i],m,"Cell area",double.Epsilon);foreach(var v in CellVertices[i])PvSeries.Array(v,12,"Cell vertices");}
        }
        if(AirTempC!=null)PvSeries.Array(AirTempC,t,"air temperature",-100,100);if(WindMS!=null)PvSeries.Array(WindMS,t,"wind",0,100);
    }
}
public sealed class PvExposureData : PvJsonData<PvExposureResult> {
    public PvExposureData(){}public PvExposureData(PvExposureResult d):base(d){}
    protected override void Validate(PvExposureResult d){d.Validate();}
    public override string TypeName=>"PV Solar Exposure";public override string TypeDescription=>"Per-panel shaded POA time series with IDs, interval durations and weather";
    public override IGH_Goo Duplicate()=>new PvExposureData(Data);
    public override string ToString()=>IsValid?$"PV exposure: {Data.Ids.Length} panels × {Data.Timestamps.Length} intervals":"Invalid PV exposure";
}
public static class PvSeries {
    public static void Check(double v,string name,double low=double.MinValue,double high=double.MaxValue){if(!PvPanelCatalog.Finite(v)||v<low||v>high)throw new ArgumentException(name+" must be finite and within "+low+" to "+high+".");}
    public static void Array(double[] a,int count,string name,double low=double.MinValue,double high=double.MaxValue){if(a==null||a.Length!=count)throw new ArgumentException(name+" length does not match timestamps.");foreach(double v in a)Check(v,name,low,high);}
    public static DateTimeOffset[] ValidateTime(string[] times,double hours,bool allowGaps=false){Check(hours,"Interval hours",1e-6,24);if(times==null||times.Length==0)throw new ArgumentException("No timestamps.");var parsed=times.Select(t=>{if(!System.Text.RegularExpressions.Regex.IsMatch(t,@"(Z|[+-]\d\d:\d\d)$"))throw new ArgumentException("Timestamps require an explicit UTC offset.");return DateTimeOffset.Parse(t,System.Globalization.CultureInfo.InvariantCulture);}).ToArray();for(int i=1;i<parsed.Length;i++)if((parsed[i]-parsed[i-1]).TotalHours<=0 || (!allowGaps && Math.Abs((parsed[i]-parsed[i-1]).TotalHours-hours)>1e-8) || (allowGaps && Math.Abs((parsed[i]-parsed[i-1]).TotalHours/hours-Math.Round((parsed[i]-parsed[i-1]).TotalHours/hours))>1e-8))throw new ArgumentException("Timestamps must be continuous, unique, and match Interval Hours.");return parsed;}
}
}
