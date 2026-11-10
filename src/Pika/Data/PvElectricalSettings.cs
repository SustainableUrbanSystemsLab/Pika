using System;
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
namespace PlugInSolar {

public sealed class PvInverterSpec {
 public double AcPowerW {get;set;}
 public double EfficiencyPct {get;set;}
 public double MaximumVoltage {get;set;}
 public double MpptMinimum {get;set;}
 public double MpptMaximum {get;set;}
 public double MaximumCurrent {get;set;}
 public void Validate(){
  PvSeries.Check(AcPowerW,"AC rated power",double.Epsilon);
  PvSeries.Check(EfficiencyPct,"Efficiency",double.Epsilon,100);
  PvSeries.Check(MaximumVoltage,"Maximum DC voltage",double.Epsilon);
  PvSeries.Check(MpptMinimum,"MPPT minimum",double.Epsilon);
  PvSeries.Check(MpptMaximum,"MPPT maximum",MpptMinimum,MaximumVoltage);
  PvSeries.Check(MaximumCurrent,"Maximum operating current",double.Epsilon);
 }
}
public sealed class PvLossSettings {
 public double SoilingPct {get;set;}
 public double DcWiringPct {get;set;}
 public double AcWiringPct {get;set;}
 public void Validate(){
  PvSeries.Check(SoilingPct,"Soiling",0,100);
  PvSeries.Check(DcWiringPct,"DC wiring",0,100);
  PvSeries.Check(AcWiringPct,"AC wiring",0,100);
 }
}
public sealed class PvElectricalSettings {
 public PvInverterSpec Inverter {get;set;}
 public int PanelsPerInverter {get;set;}
 public void Validate(){
  if(Inverter==null)throw new ArgumentException("Missing inverter.");
  Inverter.Validate();
  if(PanelsPerInverter!=1)throw new ArgumentException("This version supports one independently tracked module per inverter.");
 }
}
public sealed class PvInverterData:PvJsonData<PvInverterSpec> {
 public PvInverterData(){} public PvInverterData(PvInverterSpec value):base(value){}
 protected override void Validate(PvInverterSpec value)=>value.Validate();
 public override string TypeName=>"Inverter specifications";
 public override string TypeDescription=>"Inverter specifications";
 public override IGH_Goo Duplicate()=>IsValid?new PvInverterData(Data):new PvInverterData();
 public override string ToString()=>IsValid?Value:"Invalid Inverter specifications";
}
public sealed class PvLossData:PvJsonData<PvLossSettings> {
 public PvLossData(){} public PvLossData(PvLossSettings value):base(value){}
 protected override void Validate(PvLossSettings value)=>value.Validate();
 public override string TypeName=>"Staged loss assumptions";
 public override string TypeDescription=>"Staged loss assumptions";
 public override IGH_Goo Duplicate()=>IsValid?new PvLossData(Data):new PvLossData();
 public override string ToString()=>IsValid?Value:"Invalid Staged loss assumptions";
}
public sealed class PvElectricalData:PvJsonData<PvElectricalSettings> {
 public PvElectricalData(){} public PvElectricalData(PvElectricalSettings value):base(value){}
 protected override void Validate(PvElectricalSettings value)=>value.Validate();
 public override string TypeName=>"Module level inverter configuration";
 public override string TypeDescription=>"Module level inverter configuration";
 public override IGH_Goo Duplicate()=>IsValid?new PvElectricalData(Data):new PvElectricalData();
 public override string ToString()=>IsValid?Value:"Invalid Module level inverter configuration";
}
}