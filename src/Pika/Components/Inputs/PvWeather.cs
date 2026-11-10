using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace PlugInSolar {
public sealed class PvWeather {
    public string[] Timestamps { get; set; }
    public double[] Dni { get; set; }
    public double[] Dhi { get; set; }
    public double[] Ghi { get; set; }
    public double[] AirTempC { get; set; }
    public double[] WindMS { get; set; }
    public double[] Elevation { get; set; }
    public double[] Azimuth { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Altitude { get; set; }
    public double UtcOffset { get; set; }

    public void Validate() {
        PvSeries.ValidateTime(Timestamps,1);
        int count=Timestamps.Length;
        PvSeries.Array(Dni,count,"DNI",0);
        PvSeries.Array(Dhi,count,"DHI",0);
        PvSeries.Array(Ghi,count,"GHI",0);
        PvSeries.Array(AirTempC,count,"Air temperature",-100,100);
        PvSeries.Array(WindMS,count,"Wind speed",0,100);
        PvSeries.Array(Elevation,count,"Solar elevation",-90,90);
        PvSeries.Array(Azimuth,count,"Solar azimuth",0,360);
    }
}

public sealed class PvWeatherData : PvJsonData<PvWeather> {
    public PvWeatherData() {}
    public PvWeatherData(PvWeather weather):base(weather) {}
    protected override void Validate(PvWeather weather)=>weather.Validate();
    public override string TypeName=>"PV Weather";
    public override string TypeDescription=>"Hourly EPW irradiance, temperature, wind, midpoint sun position and site metadata";
    public override IGH_Goo Duplicate()=>IsValid?new PvWeatherData(Data):new PvWeatherData();
    public override string ToString()=>IsValid?$"PV Weather · {Data.Timestamps.Length} hourly intervals":"Invalid PV Weather";
}

public sealed class PvWeatherComponent : GH_Component {
    string cacheKey;
    PvWeatherData cached;

    public PvWeatherComponent():base("PV Weather","Weather","Extracts and organizes hourly EPW weather data for PV analysis over the selected period. Required input: EPW.","Pika","01 Inputs") {}
    public override Guid ComponentGuid=>new Guid("a835f8e0-8612-42e7-bcea-ff62bac60716");
    protected override Bitmap Icon=>Assets.Icon("sun_position");

    protected override void RegisterInputParams(GH_InputParamManager p) {
        p.AddTextParameter("EPW","EPW","Local EPW file path or direct HTTP(S) download URL.",GH_ParamAccess.item);
        p.AddTextParameter("Period Start","Start","Inclusive local standard time, MM/DD HH:mm. Blank starts at January 1 00:00.",GH_ParamAccess.item,"");
        p.AddTextParameter("Period End","End","Exclusive local standard time, MM/DD HH:mm. Blank includes the final EPW hour.",GH_ParamAccess.item,"");
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p) {
        p.AddGenericParameter("Weather","Weather","Hourly interval starts and midpoint solar angles (degrees), DNI/DHI/GHI (W/m²), air temperature (°C), wind speed (m/s), latitude/longitude (degrees), elevation (m) and UTC offset (hours). Connect to PV Energy Calculator.",GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da) {
        Message=null;
        try {
            string source="",start="",end="";
            if(!da.GetData(0,ref source))return;
            da.GetData(1,ref start);da.GetData(2,ref end);
            source=source.Trim();
            bool remote=Uri.TryCreate(source,UriKind.Absolute,out var uri)&&(uri.Scheme=="https"||uri.Scheme=="http");
            if(!remote&&!File.Exists(source))throw new ArgumentException("Supply an existing EPW path or direct HTTP(S) EPW URL.");
            string request=JsonSerializer.Serialize(new{operation="weather",inputs=new{source,start=start.Trim(),end=end.Trim()}});
            string key=remote?request:request+File.GetLastWriteTimeUtc(source).Ticks;
            if(key!=cacheKey) {
                using var response=JsonDocument.Parse(PythonBridge.RunPython(request));
                if(response.RootElement.TryGetProperty("error",out var error))throw new ArgumentException(error.GetString());
                var weather=JsonSerializer.Deserialize<PvWeather>(response.RootElement.GetProperty("outputs")[0].GetRawText());
                cached=new PvWeatherData(weather);
                cacheKey=key;
            }
            da.SetData(0,cached);
            Message=$"{cached.Data.Timestamps.Length} hours";
        }catch(Exception e){AddRuntimeMessage(GH_RuntimeMessageLevel.Error,e.Message);}
    }
}
}
