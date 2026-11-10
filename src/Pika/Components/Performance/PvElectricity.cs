using System;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using System.Text.Json;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
namespace PlugInSolar {
public sealed class PvElectricityResult {
    public string[] Ids {get;set;}public string[] Timestamps {get;set;}public double StepHours {get;set;}
    public double[][] PanelDcW {get;set;}public double[][] CellTempC {get;set;}public double[] ArrayDcW {get;set;}public double[] ArrayAcW {get;set;}
    public double[] PanelDcKwh {get;set;}public string[] Months {get;set;}public double[] MonthlyAcKwh {get;set;}public double TotalAcKwh {get;set;}public double TotalDcKwh {get;set;}public double SpecificYield {get;set;}public double CapacityWp {get;set;}public string Report {get;set;}
    public void Validate(){int n=Ids?.Length??0,t=Timestamps?.Length??0;if(n==0||t==0||PanelDcW?.Length!=n||CellTempC?.Length!=n||PanelDcKwh?.Length!=n||Months?.Length!=MonthlyAcKwh?.Length)throw new ArgumentException("Invalid electricity result dimensions.");PvSeries.ValidateTime(Timestamps,StepHours,true);PvSeries.Array(ArrayDcW,t,"DC power",0);PvSeries.Array(ArrayAcW,t,"AC power",0);for(int i=0;i<n;i++){PvSeries.Array(PanelDcW[i],t,"panel DC",0);PvSeries.Array(CellTempC[i],t,"cell temperature");}PvSeries.Check(TotalAcKwh,"AC energy",0);PvSeries.Check(TotalDcKwh,"DC energy",0);PvSeries.Check(CapacityWp,"Capacity",double.Epsilon);}
}
public sealed class PvElectricityData : PvJsonData<PvElectricityResult>{
    public PvElectricityData(){}public PvElectricityData(PvElectricityResult r):base(r){}protected override void Validate(PvElectricityResult d){d.Validate();}public override string TypeName=>"PV Electricity Results";public override string TypeDescription=>"Time-aligned panel DC, array AC and period energy results";public override IGH_Goo Duplicate()=>new PvElectricityData(Data);public override string ToString()=>IsValid?$"PV electricity: {Data.TotalAcKwh:0.###} kWh AC":"Invalid electricity results";
}
public static class PvElectricityEngine {
    public static PvElectricityResult Calculate(PvExposureResult e,PvModuleSpecs specs,double acRatingW=0,double lossesPct=0,double fallbackTempC=20,double fallbackWindMS=1,double u0=25,double u1=6.84){
        e.Validate();specs.Validate();PvSeries.Check(acRatingW,"AC rating",0);PvSeries.Check(lossesPct,"Other DC losses",0,100);PvSeries.Check(fallbackTempC,"Fallback temperature",-100,100);PvSeries.Check(fallbackWindMS,"Fallback wind",0,100);PvSeries.Check(u0,"Faiman U0",double.Epsilon);PvSeries.Check(u1,"Faiman U1",0);
        int n=e.Ids.Length,t=e.Timestamps.Length;double capacity=specs.PowerW*n,ac=acRatingW==0?capacity:acRatingW,pdc0=ac/.96,gamma=specs.GammaPmaxPctPerC/100;
        PvSeries.Check(capacity,"Array capacity",double.Epsilon);PvSeries.Check(pdc0,"Inverter reference rating",double.Epsilon);
        var r=new PvElectricityResult{Ids=e.Ids.ToArray(),Timestamps=e.Timestamps.ToArray(),StepHours=e.StepHours,PanelDcW=new double[n][],CellTempC=new double[n][],ArrayDcW=new double[t],ArrayAcW=new double[t],PanelDcKwh=new double[n],CapacityWp=capacity};
        for(int i=0;i<n;i++){
            r.PanelDcW[i]=new double[t];r.CellTempC[i]=new double[t];for(int j=0;j<t;j++){
                double temp=e.AirTempC==null?fallbackTempC:e.AirTempC[j],wind=e.WindMS==null?fallbackWindMS:e.WindMS[j];double cell=temp+e.Poa[i][j]/(u0+u1*wind);double dc=Math.Max(0,specs.PowerW*e.Poa[i][j]/1000*(1+gamma*(cell-25)))*(1-lossesPct/100);
                r.CellTempC[i][j]=cell;r.PanelDcW[i][j]=dc;r.ArrayDcW[j]+=dc;
            }r.PanelDcKwh[i]=r.PanelDcW[i].Sum()*e.StepHours/1000;
        }
        for(int j=0;j<t;j++){double dc=r.ArrayDcW[j],zeta=dc/pdc0;r.ArrayAcW[j]=dc>0?Math.Max(0,Math.Min(ac,.96/.9637*(-.0162*zeta-.0059/zeta+.9858)*dc)):0;}
        var times=PvSeries.ValidateTime(e.Timestamps,e.StepHours,e.Hoys!=null);var monthly=new SortedDictionary<string,double>();
        // Timestamps represent interval midpoints (EPW convention). Split cross-month intervals exactly.
        for(int j=0;j<t;j++){
            var start=times[j].AddHours(-e.StepHours/2);var end=times[j].AddHours(e.StepHours/2);while(start<end){var next=new DateTimeOffset(start.Year,start.Month,1,0,0,0,start.Offset).AddMonths(1);var stop=next<end?next:end;string key=start.ToString("yyyy-MM");if(!monthly.ContainsKey(key))monthly[key]=0;monthly[key]+=r.ArrayAcW[j]*(stop-start).TotalHours/1000;start=stop;}
        }
        r.Months=monthly.Keys.ToArray();r.MonthlyAcKwh=monthly.Values.ToArray();r.TotalDcKwh=r.ArrayDcW.Sum()*e.StepHours/1000;r.TotalAcKwh=r.ArrayAcW.Sum()*e.StepHours/1000;r.SpecificYield=r.TotalAcKwh/(capacity/1000);
        r.Report=$"{n} identical {specs.PowerW:0.###} Wp modules; {capacity:0.###} Wp DC, {ac:0.###} W AC limit. Per-panel PVWatts DC, Faiman U0={u0}, U1={u1}, common PVWatts inverter (96% nominal). Independent module maximum-power DC approximation; shared-string mismatch, bypass diodes and MPPT tracking are not modeled. Panel DC results are model estimates. Full front POA approximates effective irradiance; no spectral/AOI correction. Other DC losses={lossesPct}% exclude shading already modeled. "+(e.AirTempC==null?$"Fallback air temperature {fallbackTempC}°C. ":"Weather air temperature. ")+(e.WindMS==null?$"Fallback wind {fallbackWindMS} m/s. ":"Weather wind. ")+"Faiman temperature used as cell-temperature approximation; coefficients must suit mounting conditions. Energy covers the supplied period, not automatically a full year. Timestamps treated as interval midpoints.";
        r.Validate();return r;
    }
}
public sealed class PvElectricityComponent : GH_Component {
    // Legacy component: retained for saved definitions, hidden from new workflows.
    public override GH_Exposure Exposure => GH_Exposure.hidden;

    public PvElectricityComponent():base("PV Electricity","PV Electricity","Convert per-panel shaded solar exposure to module DC and array AC using PVWatts and a Faiman temperature approximation. No shared-string/bypass-diode model. Required inputs: Solar Exposure and Module Specifications.","Pika","02 Performance"){}
    public override Guid ComponentGuid=>new Guid("bfef72c5-358b-4cc4-a49f-52de21512405");protected override Bitmap Icon=>Assets.Icon("pv_power");
    protected override void RegisterInputParams(GH_InputParamManager p){
        p.AddGenericParameter("Solar Exposure","Exposure","PV Solar Exposure → Exposure.",GH_ParamAccess.item);p.AddGenericParameter("Module Specifications","Specs","Select PV Panel → Specs. One identical product per layout.",GH_ParamAccess.item);
        p.AddNumberParameter("Array AC Rating","AC W","Total common-inverter AC limit, W. Zero sets it equal to total module DC nameplate power. Independent module MPP approximation; no shared strings.",GH_ParamAccess.item,0);
        p.AddNumberParameter("Other DC Losses","Loss%","Percent reduction after modeled shading, 0–100. Default zero; add explicitly justified non-shading losses.",GH_ParamAccess.item,0);
        p.AddNumberParameter("Fallback Air Temperature","Temp","Used only if Weather has no temp_air column, °C.",GH_ParamAccess.item,20);
        p.AddNumberParameter("Fallback Wind Speed","Wind","Used only if Weather has no wind_speed column, m/s.",GH_ParamAccess.item,1);
        p.AddNumberParameter("Thermal U0","U0","Faiman constant heat-loss coefficient, W/(m² K). Default 25; adjust for mounting conditions.",GH_ParamAccess.item,25);
        p.AddNumberParameter("Thermal U1","U1","Faiman wind-dependent heat-loss coefficient. Default 6.84; adjust for mounting conditions.",GH_ParamAccess.item,6.84);
        int air=p.AddGenericParameter("Air Temperature","Air °C","Optional LB dry_bulb_temperature collection or Eddy3D temperature list. Aligned by collection HOYs, or full-year indices, or supplied result order. Overrides temperature stored in Exposure.",GH_ParamAccess.list);p[air].Optional=true;
        int wind=p.AddGenericParameter("Wind Speed","Wind m/s","Optional LB wind_speed collection or Eddy3D list, aligned with Air Temperature. Overrides wind stored in Exposure.",GH_ParamAccess.list);p[wind].Optional=true;
    }
    protected override void RegisterOutputParams(GH_OutputParamManager p){
        p.AddGenericParameter("Electricity Results","Results","Grouped panel IDs, power, temperatures and energy results.",GH_ParamAccess.item);
        p.AddNumberParameter("AC Energy","AC kWh","Total AC electricity over supplied period, kWh.",GH_ParamAccess.item);p.AddNumberParameter("DC Energy","DC kWh","Total module DC electricity after Other DC Losses, kWh.",GH_ParamAccess.item);
        p.AddNumberParameter("Array AC Power","AC W","Array AC power in timestamp order, W.",GH_ParamAccess.list);p.AddNumberParameter("Panel DC Power","DC W","Branch {panel index}; time-ordered module DC power, W.",GH_ParamAccess.tree);
        p.AddNumberParameter("Panel DC Energy","Panel kWh","Per-panel estimated DC energy over period, kWh.",GH_ParamAccess.list);p.AddTextParameter("Months","Months","Local year-month keys; partial months remain partial.",GH_ParamAccess.list);p.AddNumberParameter("Monthly AC Energy","Month kWh","AC kWh aligned with Months.",GH_ParamAccess.list);
        p.AddNumberParameter("Specific Yield","Yield","Period AC kWh per installed DC kWp.",GH_ParamAccess.item);p.AddTextParameter("Panel IDs","IDs","Identifiers matching panel DC branches.",GH_ParamAccess.list);p.AddTextParameter("Timestamps","Time","Timezone-aware interval midpoints matching power outputs.",GH_ParamAccess.list);p.AddTextParameter("Report","Report","Electrical/thermal assumptions and limitations.",GH_ParamAccess.item);
    }
    protected override void SolveInstance(IGH_DataAccess da){try{
        IGH_Goo exposure=null,specs=null;var v=new[]{0d,0d,20d,1d,25d,6.84};if(!da.GetData(0,ref exposure)||!da.GetData(1,ref specs))return;for(int i=0;i<v.Length;i++)if(!da.GetData(i+2,ref v[i]))return;
        var e=PvPanelCatalog.Unwrap<PvExposureData>(exposure).Data;var s=PvPanelCatalog.Unwrap<PvModuleSpecsData>(specs).Data;
        for(int k=0;k<2;k++){var items=new System.Collections.Generic.List<IGH_Goo>();da.GetDataList(8+k,items);if(items.Count==0)continue;var a=PvWeatherAdapter.Numbers(items,out var hoy,out var unit);double[] selected;
            if(hoy!=null&&e.Hoys!=null){var lookup=hoy.Select((h,i)=>new{h,i}).ToDictionary(x=>x.h,x=>x.i);selected=e.Hoys.Select(h=>lookup.ContainsKey(h)?a[lookup[h]]:throw new ArgumentException("Ambient weather does not cover exposure HOYs.")).ToArray();}
            else if(a.Length==8760&&e.Hoys!=null)selected=e.Hoys.Select(h=>a[(int)h]).ToArray();
            else if(a.Length==e.Timestamps.Length)selected=a;else throw new ArgumentException("Ambient weather must align with Exposure or contain all 8760 hours.");
            if(unit!=null&&unit!=(k==0?"C":"m/s"))throw new ArgumentException("Unexpected ambient-weather unit: "+unit);if(k==0)e.AirTempC=selected;else e.WindMS=selected;
        }
        var r=PvElectricityEngine.Calculate(e,s,v[0],v[1],v[2],v[3],v[4],v[5]);
        da.SetData(0,new PvElectricityData(r));da.SetData(1,r.TotalAcKwh);da.SetData(2,r.TotalDcKwh);da.SetDataList(3,r.ArrayAcW);da.SetDataTree(4,PvDataTrees.Numbers(r.PanelDcW));da.SetDataList(5,r.PanelDcKwh);da.SetDataList(6,r.Months);da.SetDataList(7,r.MonthlyAcKwh);da.SetData(8,r.SpecificYield);da.SetDataList(9,r.Ids);da.SetDataList(10,r.Timestamps);da.SetData(11,r.Report);Message=$"{r.TotalAcKwh:0.##} kWh AC";
        if(e.AirTempC==null||e.WindMS==null)AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,"Weather lacks temperature and/or wind; corresponding fallback inputs are used.");
        double referenceArea=s.PowerW/(1000*s.EfficiencyPct/100);if(e.AreaM2.Any(a=>Math.Abs(a-referenceArea)/referenceArea>.1))AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,"Panel geometry area differs substantially from area implied by rated power/efficiency. Electricity uses rated Wp per module and does not scale it by geometry area.");
    }catch(Exception e){foreach(var p in Params.Output)p.ClearData();Message="";AddRuntimeMessage(GH_RuntimeMessageLevel.Error,e.Message);}}
}
}
