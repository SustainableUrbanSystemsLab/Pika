using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace PlugInSolar {
    public static class PvAcEngine {
        public static PvAcResult Calculate(PvDcResult dc, PvElectricalSettings config, PvLossSettings losses) {
            dc.Validate();
            config.Validate();
            losses.Validate();
            var inv = config.Inverter;
            var module = dc.Module;
            PvInverterValidation.Check(module,inv);

            int hours = dc.IntervalStarts.Length;
            var result = new PvAcResult {
                PanelIds = dc.PanelIds.ToArray(), IntervalStarts = dc.IntervalStarts.ToArray(),
                StepHours = dc.StepHours, PanelPowerW = new double[dc.PanelIds.Length][],
                ArrayPowerW = new double[hours]
            };
            double etaNominal = inv.EfficiencyPct / 100;
            double referenceDcW = inv.AcPowerW / etaNominal;
            double energyFactor = dc.StepHours / 1000;
            for (int panel = 0; panel < dc.PanelIds.Length; panel++) {
                var row = new double[hours];
                result.PanelPowerW[panel] = row;
                for (int hour = 0; hour < hours; hour++) {
                    double source = dc.PanelPowerW[panel][hour];
                    double inverterInput = source * (1 - losses.DcWiringPct / 100);
                    double unclipped = 0;
                    if (inverterInput > 0) {
                        double load = inverterInput / referenceDcW;
                        double efficiency = etaNominal / 0.9637 * (-0.0162 * load - 0.0059 / load + 0.9858);
                        unclipped = inverterInput * Math.Max(0, Math.Min(1, efficiency));
                    }
                    double clipped = Math.Min(inv.AcPowerW, unclipped);
                    double delivered = clipped * (1 - losses.AcWiringPct / 100);
                    row[hour] = delivered;
                    result.ArrayPowerW[hour] += delivered;
                    result.InputDcKwh += source * energyFactor;
                    result.DcWiringLossKwh += (source - inverterInput) * energyFactor;
                    result.ConversionLossKwh += (inverterInput - unclipped) * energyFactor;
                    result.ClippingLossKwh += (unclipped - clipped) * energyFactor;
                    result.AcWiringLossKwh += (clipped - delivered) * energyFactor;
                }
            }
            var monthly = new SortedDictionary<string, double>();
            var starts = PvSeries.ValidateTime(dc.IntervalStarts, dc.StepHours);
            for (int hour = 0; hour < hours; hour++) {
                var start = starts[hour];
                var end = start.AddHours(dc.StepHours);
                while (start < end) {
                    var monthEnd = new DateTimeOffset(start.Year, start.Month, 1, 0, 0, 0, start.Offset).AddMonths(1);
                    var stop = monthEnd < end ? monthEnd : end;
                    string key = start.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                    if (!monthly.ContainsKey(key)) monthly[key] = 0;
                    monthly[key] += result.ArrayPowerW[hour] * (stop - start).TotalHours / 1000;
                    start = stop;
                }
            }
            result.Months = monthly.Keys.ToArray();
            result.MonthlyEnergyKwh = monthly.Values.ToArray();
            result.TotalAcKwh = result.ArrayPowerW.Sum() * energyFactor;
            result.Validate();
            return result;
        }
    }

    public sealed class PvAcCalculatorComponent : GH_Component {
        public PvAcCalculatorComponent() : base("PV AC Calculator", "PV AC",
            "Converts hourly module DC to AC and reports monthly energy and staged losses. Required inputs: DC Results and Electrical Config. One independent inverter per module.",
            "Pika", "02 Performance") {}
        public override Guid ComponentGuid => new Guid("991921af-a7fb-4456-a784-660308cb439a");
        protected override Bitmap Icon => Assets.Icon("pv_power");

        protected override void RegisterInputParams(GH_InputParamManager p) {
            p.AddGenericParameter("DC Results", "DC", "PV Energy Calculator → DC Results. Includes timestamped module DC in W and applied soiling.", GH_ParamAccess.item);
            p.AddGenericParameter("Electrical Config", "Config", "PV Electrical Config output. One inverter per panel with manufacturer ratings.", GH_ParamAccess.item);
            int losses = p.AddGenericParameter("Losses", "Losses", "Optional PV Loss Assumptions. Applies DC wiring and AC wiring once. Soiling is already applied upstream in the DC calculator.", GH_ParamAccess.item);
            p[losses].Optional = true;
        }
        protected override void RegisterOutputParams(GH_OutputParamManager p) {
            p.AddNumberParameter("Total AC Energy", "AC kWh", "Delivered AC energy over the supplied period, kWh. Includes inverter conversion, clipping and wiring losses.", GH_ParamAccess.item);
            p.AddTextParameter("Hourly AC Power", "AC Time", "Array AC power labeled MM/dd HH:mm | W in EPW local standard time.", GH_ParamAccess.list);
            p.AddTextParameter("Months", "Months", "Local year-month labels matching Monthly AC Energy. Partial periods yield partial months.", GH_ParamAccess.list);
            p.AddNumberParameter("Monthly AC Energy", "Monthly kWh", "AC kWh for each Months entry.", GH_ParamAccess.list);
            p.AddTextParameter("Loss Breakdown", "Losses", "Staged kWh and percent of input DC. Input DC = DC wiring + conversion + clipping + AC wiring + delivered AC. Soiling is accounted upstream.", GH_ParamAccess.list);
            p.AddGenericParameter("AC Results", "Results", "Structured hourly panel/array AC, timestamps, monthly energy and the loss ledger. Suitable for downstream storage analysis.", GH_ParamAccess.item);
            p.AddNumberParameter("Hourly AC Values", "AC W", "Numeric array AC power in W, aligned with Hourly AC Power.", GH_ParamAccess.list);
            p.AddTextParameter("Report", "Report", "Inverter assignment, staged-loss assumptions, units and period.", GH_ParamAccess.item);
        }
        protected override void SolveInstance(IGH_DataAccess da) {
            Message = null;
            foreach (var output in Params.Output) output.ClearData();
            try {
                IGH_Goo dcInput = null, configInput = null, lossInput = null;
                if (!da.GetData(0, ref dcInput) || !da.GetData(1, ref configInput)) return;
                var dc = PvPanelCatalog.Unwrap<PvDcData>(dcInput).Data;
                var config = PvPanelCatalog.Unwrap<PvElectricalData>(configInput).Data;
                var losses = new PvLossSettings();
                bool hasLosses = da.GetData(2, ref lossInput);
                if (hasLosses) losses = PvPanelCatalog.Unwrap<PvLossData>(lossInput).Data;
                else if (Params.Input[2].SourceCount > 0)
                    throw new ArgumentException("Connected Losses contains no valid data. Resolve its upstream errors.");
                if (hasLosses && Math.Abs(losses.SoilingPct - dc.SoilingPct) > 1e-9)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,
                        $"Soiling is {dc.SoilingPct}% in DC Results. Connect this Losses object to the DC calculator to apply its {losses.SoilingPct}% soiling setting.");
                var result = PvAcEngine.Calculate(dc, config, losses);
                var dates = PvSeries.ValidateTime(result.IntervalStarts, result.StepHours);
                da.SetData(0, result.TotalAcKwh);
                da.SetDataList(1, result.ArrayPowerW.Select((v, i) =>
                    dates[i].ToString("MM/dd HH:mm", CultureInfo.InvariantCulture) + " | " +
                    v.ToString("0.###", CultureInfo.InvariantCulture) + " W"));
                da.SetDataList(2, result.Months);
                da.SetDataList(3, result.MonthlyEnergyKwh);
                string Row(string label, double energy) => label + " | " +
                    energy.ToString("0.######", CultureInfo.InvariantCulture) + " kWh | " +
                    (result.InputDcKwh > 0 ? energy / result.InputDcKwh * 100 : 0).ToString("0.###", CultureInfo.InvariantCulture) + "% of input DC";
                da.SetDataList(4, new[] {
                    Row("Input DC after upstream soiling", result.InputDcKwh),
                    Row("DC wiring loss", result.DcWiringLossKwh),
                    Row("Inverter conversion loss", result.ConversionLossKwh),
                    Row("Inverter clipping loss", result.ClippingLossKwh),
                    Row("AC wiring loss", result.AcWiringLossKwh),
                    Row("Delivered AC", result.TotalAcKwh)
                });
                da.SetData(5, new PvAcData(result));
                da.SetDataList(6, result.ArrayPowerW);
                da.SetData(7, $"{dc.PanelIds.Length} independent module inverters, each {config.Inverter.AcPowerW} W AC. " +
                    $"PVWatts inverter curve at {config.Inverter.EfficiencyPct}% nominal efficiency. " +
                    $"DC wiring {losses.DcWiringPct}%, AC wiring {losses.AcWiringPct}%. " +
                    $"Upstream soiling {dc.SoilingPct}% is retained without reapplication. " +
                    $"Period {dc.IntervalStarts.First()} to {dc.IntervalStarts.Last()} (interval starts). " +
                    "Loss order is DC wiring → conversion → clipping → AC wiring. Household consumption and storage are separate downstream calculations. " +
                    "Electrical checks use STC ratings; design-temperature and short-circuit checks require additional specifications.");
                Message = $"{result.TotalAcKwh:0.##} kWh AC";
            } catch (Exception error) {
                foreach (var output in Params.Output) output.ClearData();
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, error.Message);
            }
        }
    }
}
