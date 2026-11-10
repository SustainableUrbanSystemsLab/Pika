using System;

namespace PlugInSolar {
    public static class PvInverterValidation {
        public static void Check(PvModuleSpecs module, PvInverterSpec inverter) {
            module.Validate();
            inverter.Validate();
            if (module.Voc > inverter.MaximumVoltage)
                throw new ArgumentException($"Module Voc {module.Voc} V exceeds inverter maximum DC voltage {inverter.MaximumVoltage} V.");
            if (module.Vmp < inverter.MpptMinimum || module.Vmp > inverter.MpptMaximum)
                throw new ArgumentException($"Module Vmp {module.Vmp} V is outside the inverter MPPT range {inverter.MpptMinimum}–{inverter.MpptMaximum} V.");
            if (module.Imp > inverter.MaximumCurrent)
                throw new ArgumentException($"Module Imp {module.Imp} A exceeds inverter maximum operating current {inverter.MaximumCurrent} A.");
        }
    }
}
