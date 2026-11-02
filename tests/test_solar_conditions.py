"""Portable validation checks for the current weather-preparation backend."""
import importlib.util
from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('solar_conditions',ROOT/'python/solar_conditions.py')
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)

class WeatherValidationChecks(unittest.TestCase):
    def args(self):
        return dict(latitude=33.749,longitude=-84.388,timezone=-5,altitude=320,
                    dnr=[800,700,0],dhr=[100,100,0],ghr=[900,800,0],hoys=[4092,4093,4094])
    def reject(self,**changes):
        a=self.args();a.update(changes)
        with self.assertRaises(ValueError):module.prepare(**a)
    def test_duplicate_hours(self):self.reject(hoys=[1,1,2])
    def test_mismatched_hours(self):self.reject(hoys=[1,2])
    def test_missing_radiation(self):self.reject(dnr=[9999,0,0])
    def test_fractional_hour(self):self.reject(hoys=[0,1.5,2])
    def test_missing_partial_index(self):self.reject(hoys=None)
    def test_negative_radiation(self):self.reject(dhr=[100,-1,0])
    def test_invalid_units(self):self.reject(units='kWh/m2')
    def test_unknown_resolution(self):self.reject(density=3)
    def test_invalid_location(self):self.reject(latitude=100)
    def test_bridge_rejects_legacy_operation(self):
        import sys
        sys.path.insert(0,str(ROOT/'python'))
        bridge_spec=importlib.util.spec_from_file_location('current_bridge',ROOT/'python/bridge.py')
        bridge=importlib.util.module_from_spec(bridge_spec);bridge_spec.loader.exec_module(bridge)
        with self.assertRaises(ValueError):bridge.dispatch('clear_sky',{})

if __name__=='__main__':unittest.main()
