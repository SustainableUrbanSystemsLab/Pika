using System;
using System.Linq;
using System.Text.Json;
using Grasshopper.Kernel.Types;

namespace PlugInSolar {
public class PvPanelDimensions {
    public string Model { get; set; }
    public double WidthM { get; set; }
    public double HeightM { get; set; }
    public double ThicknessM { get; set; }
    public void Validate() {
        foreach (double v in new[]{WidthM,HeightM,ThicknessM})
            if (!PvPanelCatalog.Finite(v) || v<=0) throw new ArgumentException("Panel dimensions must be finite, positive meters.");
    }
}
public class PvModuleSpecs {
    public string Manufacturer { get; set; }
    public string Model { get; set; }
    public string SourceUrl { get; set; }
    public string Revision { get; set; }
    public double PowerW { get; set; }
    public double EfficiencyPct { get; set; }
    public double GammaPmaxPctPerC { get; set; }
    public double Vmp { get; set; }
    public double Imp { get; set; }
    public double Voc { get; set; }
    public double Isc { get; set; }
    public void Validate() {
        foreach(double v in new[]{PowerW,EfficiencyPct,Vmp,Imp,Voc,Isc})
            if(!PvPanelCatalog.Finite(v)||v<=0)throw new ArgumentException("Module power, efficiency, voltages, and currents must be finite and positive.");
        if(EfficiencyPct>100)throw new ArgumentException("Module efficiency must be at most 100 percent.");
        if(!PvPanelCatalog.Finite(GammaPmaxPctPerC)||GammaPmaxPctPerC>0||GammaPmaxPctPerC < -2)
            throw new ArgumentException("Pmax temperature coefficient must be between -2 and 0 percent per degree C.");
        if(Voc<Vmp||Isc<Imp)throw new ArgumentException("Voc must be at least Vmp, and Isc must be at least Imp.");
        if(Math.Abs(Vmp*Imp-PowerW)>PowerW*.03)throw new ArgumentException("Rated power is inconsistent with Vmp multiplied by Imp (3 percent tolerance).");
    }
}
public abstract class PvJsonData<T> : GH_Goo<string> where T:class {
    protected PvJsonData() { Value=""; }
    protected PvJsonData(T data) { Validate(data); Value=JsonSerializer.Serialize(data); }
    protected abstract void Validate(T data);
    public T Data { get { var d=JsonSerializer.Deserialize<T>(Value); if(d==null)throw new ArgumentException("Missing PV data.");Validate(d);return d; } }
    public override bool IsValid { get { try {var d=Data;return true;}catch{return false;} } }
    public override bool Write(GH_IO.Serialization.GH_IWriter w) {w.SetString("payload",Value??"");return true;}
    public override bool Read(GH_IO.Serialization.GH_IReader r) {Value=r.GetString("payload");return IsValid;}
}
public class PvPanelDimensionsData : PvJsonData<PvPanelDimensions> {
    public PvPanelDimensionsData() { }
    public PvPanelDimensionsData(PvPanelDimensions d):base(d) { }
    protected override void Validate(PvPanelDimensions d) {d.Validate();}
    public override string TypeName=>"PV Panel Dimensions";
    public override string TypeDescription=>"Named panel dimensions in meters";
    public override IGH_Goo Duplicate()=>new PvPanelDimensionsData(Data);
    public override string ToString() {if(!IsValid)return "Invalid PV dimensions";var d=Data;return $"{d.Model}: {d.WidthM} × {d.HeightM} × {d.ThicknessM} m";}
}
public class PvModuleSpecsData : PvJsonData<PvModuleSpecs> {
    public PvModuleSpecsData() { }
    public PvModuleSpecsData(PvModuleSpecs d):base(d) { }
    protected override void Validate(PvModuleSpecs d) {d.Validate();}
    public override string TypeName=>"PV Module Specifications";
    public override string TypeDescription=>"STC electrical specifications; efficiency and temperature coefficient in percent units";
    public override IGH_Goo Duplicate()=>new PvModuleSpecsData(Data);
    public override string ToString() {if(!IsValid)return "Invalid PV specifications";var d=Data;return $"{d.Model}: {d.PowerW} Wp, {d.EfficiencyPct}%, Pmax coefficient {d.GammaPmaxPctPerC}%/°C; Vmp {d.Vmp} V, Imp {d.Imp} A, Voc {d.Voc} V, Isc {d.Isc} A";}
}
public static class PvPanelCatalog {
    public const string Source="https://static.trinasolar.com/sites/default/files/Datasheet_Vertex%20S%2B_NEG9R.28_EN_2024_C_web.pdf";
    // STC values from manufacturer revision TSM_EN_2024_C. Catalog snapshots, not live inventory.
    public static readonly string[] ModelIds={"TSM-430NEG9R.28","TSM-450NEG9R.28","TSM-460NEG9R.28","Heliene 60M G1 All Black 305W","Heliene 60M G1 All Black 310W","Heliene 60M G1 All Black 315W","Heliene 60M G1 All Black 320W"};
    public const string DefaultModel="Heliene 60M G1 All Black 320W";
    public static bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);
    public static PvModuleSpecs Specifications(string model) {
        int i=Array.FindIndex(ModelIds,id=>string.Equals(id,model?.Trim(),StringComparison.OrdinalIgnoreCase));
        if(i<0)throw new ArgumentException("Unknown panel product. Choose a product from the right-click menu or use an exact catalog ID: "+string.Join(", ",ModelIds));
        if(i>=3) {
            int h=i-3;
            var heliene=new PvModuleSpecs{Manufacturer="Heliene",Model=ModelIds[i],SourceUrl="references/panel-datasheets/Heliene60M_Black_Home-PV_NA-Rev.02.pdf",Revision="NA-Rev.02 (user-supplied datasheet)",PowerW=new[]{305d,310d,315d,320d}[h],EfficiencyPct=new[]{18.28,18.78,18.92,19.21}[h],GammaPmaxPctPerC=-.36,Vmp=new[]{33.60,33.54,33.70,34.10}[h],Imp=new[]{9.07,9.27,9.33,9.37}[h],Voc=new[]{39.98,40.13,40.32,40.50}[h],Isc=new[]{9.67,10.22,10.16,10.20}[h]};
            heliene.Validate();return heliene;
        }
        var d=new PvModuleSpecs {Manufacturer="Trina Solar",Model=ModelIds[i],SourceUrl=Source,Revision="TSM_EN_2024_C",PowerW=new[]{430d,450d,460d}[i],EfficiencyPct=new[]{21.5,22.5,23.0}[i],GammaPmaxPctPerC=-.29,Vmp=new[]{43.2,44.6,45.4}[i],Imp=new[]{9.96,10.09,10.14}[i],Voc=new[]{51.4,52.9,53.8}[i],Isc=new[]{10.59,10.74,10.81}[i]};
        d.Validate();return d;
    }
    public static PvPanelDimensions Dimensions(string model) {
        var s=Specifications(model);
        return s.Manufacturer=="Heliene" ? new PvPanelDimensions{Model=s.Model,WidthM=1.001,HeightM=1.666,ThicknessM=.040} : new PvPanelDimensions{Model=s.Model,WidthM=1.134,HeightM=1.762,ThicknessM=.030};
    }
    public static T Unwrap<T>(IGH_Goo value) where T:class {
        if(value is T data)return data;
        if(value is GH_ObjectWrapper wrapper && wrapper.Value is T wrapped)return wrapped;
        throw new ArgumentException("Incorrect grouped PV data type. Connect the matching grouped output directly; do not pass it through a text Panel.");
    }
}
}
