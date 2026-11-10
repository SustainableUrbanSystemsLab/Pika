using System;
using System.Globalization;
using System.Linq;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace PlugInSolar {
    public sealed class PvDcResult {
        public string[] PanelIds { get; set; }
        public string[] IntervalStarts { get; set; }
        public double StepHours { get; set; }
        public double[][] PanelPowerW { get; set; }
        public PvModuleSpecs Module { get; set; }
        public double SoilingPct { get; set; }

        public void Validate() {
            if (Module == null) throw new ArgumentException("DC Results is missing module specifications.");
            Module.Validate();
            PvSeries.ValidateTime(IntervalStarts, StepHours);
            PvSeries.Check(SoilingPct, "DC soiling", 0, 100);
            if (PanelIds == null || PanelIds.Length == 0 || PanelIds.Distinct().Count() != PanelIds.Length ||
                PanelPowerW == null || PanelPowerW.Length != PanelIds.Length)
                throw new ArgumentException("DC Results needs unique panel IDs and aligned power branches.");
            foreach (var row in PanelPowerW)
                PvSeries.Array(row, IntervalStarts.Length, "Panel DC power", 0);
        }
    }

    public sealed class PvDcData : PvJsonData<PvDcResult> {
        public PvDcData() {}
        public PvDcData(PvDcResult result) : base(result) {}
        protected override void Validate(PvDcResult value) => value.Validate();
        public override string TypeName => "PV DC Results";
        public override string TypeDescription => "Module DC power, panel IDs, interval starts, duration and applied soiling";
        public override IGH_Goo Duplicate() => IsValid ? new PvDcData(Data) : new PvDcData();
        public override string ToString() => IsValid
            ? $"PV DC Results · {Data.PanelIds.Length} panels · {Data.IntervalStarts.Length} intervals"
            : "Invalid PV DC Results";
    }

    public sealed class PvAcResult {
        public string[] PanelIds { get; set; }
        public string[] IntervalStarts { get; set; }
        public double StepHours { get; set; }
        public double[][] PanelPowerW { get; set; }
        public double[] ArrayPowerW { get; set; }
        public string[] Months { get; set; }
        public double[] MonthlyEnergyKwh { get; set; }
        public double InputDcKwh { get; set; }
        public double DcWiringLossKwh { get; set; }
        public double ConversionLossKwh { get; set; }
        public double ClippingLossKwh { get; set; }
        public double AcWiringLossKwh { get; set; }
        public double TotalAcKwh { get; set; }

        public void Validate() {
            PvSeries.ValidateTime(IntervalStarts, StepHours);
            if (PanelIds == null || PanelIds.Length == 0 || PanelIds.Distinct().Count() != PanelIds.Length ||
                PanelPowerW == null || PanelPowerW.Length != PanelIds.Length)
                throw new ArgumentException("AC panel branches do not match panel IDs.");
            foreach (var row in PanelPowerW)
                PvSeries.Array(row, IntervalStarts.Length, "Panel AC power", 0);
            PvSeries.Array(ArrayPowerW, IntervalStarts.Length, "Array AC power", 0);
            if (Months == null) throw new ArgumentException("Missing AC month labels.");
            PvSeries.Array(MonthlyEnergyKwh, Months.Length, "Monthly AC energy", 0);
            foreach (double value in new[] { InputDcKwh, DcWiringLossKwh, ConversionLossKwh,
                ClippingLossKwh, AcWiringLossKwh, TotalAcKwh })
                PvSeries.Check(value, "AC energy ledger", 0);
            double balance = InputDcKwh - DcWiringLossKwh - ConversionLossKwh -
                ClippingLossKwh - AcWiringLossKwh - TotalAcKwh;
            double tolerance = Math.Max(1e-8, InputDcKwh * 1e-9);
            if (Math.Abs(balance) > tolerance || Math.Abs(MonthlyEnergyKwh.Sum() - TotalAcKwh) > tolerance)
                throw new ArgumentException("AC energy ledger does not balance.");
        }
    }

    public sealed class PvAcData : PvJsonData<PvAcResult> {
        public PvAcData() {}
        public PvAcData(PvAcResult result) : base(result) {}
        protected override void Validate(PvAcResult value) => value.Validate();
        public override string TypeName => "PV AC Results";
        public override string TypeDescription => "Hourly AC power, monthly energy and staged electrical losses";
        public override IGH_Goo Duplicate() => IsValid ? new PvAcData(Data) : new PvAcData();
        public override string ToString() => IsValid
            ? $"PV AC Results · {Data.TotalAcKwh:0.###} kWh"
            : "Invalid PV AC Results";
    }

    public static class PvHourlyDisplay {
        public static GH_Structure<GH_String> Power(double[][] values, string[] starts) {
            var labels = starts.Select(t => DateTimeOffset.Parse(t, CultureInfo.InvariantCulture)
                .ToString("MM/dd HH:mm", CultureInfo.InvariantCulture)).ToArray();
            var tree = new GH_Structure<GH_String>();
            for (int panel = 0; panel < values.Length; panel++) {
                tree.AppendRange(values[panel].Select((value, hour) => new GH_String(
                    labels[hour] + " | " + value.ToString("0.###", CultureInfo.InvariantCulture) + " W")),
                    new GH_Path(panel));
            }
            return tree;
        }
    }
}
