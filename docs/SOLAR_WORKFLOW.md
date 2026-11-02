# Solar exposure v2 — weather preparation, calculation, visualization

This update separates three responsibilities:

**PV Solar Conditions → PV Solar Exposure → PV Exposure Map**

PV Solar Exposure also connects directly to **PV Electricity**. No household-savings, search, tree, foliage, transmission or new material model is added. Additional Shading Geometry retains the existing geometric-blocking behavior.

## Connect the components

| From | To | Meaning |
|---|---|---|
| Select PV Panel → Dims | PV Geometry Generator → Dims | Product dimensions |
| PV Geometry Generator → Template | PV Candidate Layout → Template | Panel solid and front frame |
| PV Candidate Layout → Layout | PV Solar Exposure → Layout | Positioned panels, IDs and stored context |
| LB Import EPW → location | PV Solar Conditions → Location | Latitude, longitude, standard UTC offset and elevation |
| LB Import EPW → direct_normal_rad | PV Solar Conditions → DNR | Hourly direct normal radiation, Wh/m² |
| LB Import EPW → diffuse_horizontal_rad | PV Solar Conditions → DHR | Hourly diffuse horizontal radiation, Wh/m² |
| LB Import EPW → global_horizontal_rad | PV Solar Conditions → GHR | Hourly global horizontal radiation, Wh/m² |
| Eddy3D radiation numeric outputs | Corresponding DNR / DHR / GHR inputs | Alternative to Ladybug collections; enable these outputs in Eddy3D's menu |
| Eddy3D → Hour of Year | PV Solar Conditions → HOY | Required for partial numeric lists; normally zero-based |
| PV Solar Conditions → Conditions | PV Solar Exposure → Conditions | Prepared time-resolved sky data |
| PV Solar Exposure → Results | PV Electricity → Exposure | Panel-average hourly POA and time metadata |
| Select PV Panel → Specs | PV Electricity → Specs | Per-module electrical ratings |
| LB dry_bulb_temperature / wind_speed | PV Electricity → Air Temperature / Wind Speed | Optional real ambient conditions; separate from exposure calculation |
| PV Solar Exposure → Results | PV Exposure Map → Results | Retained grid-cell irradiation and vertices |

Ladybug radiation collections carry values, units and dates; direct connections are supported. HOYs are inferred from their dates. Full-year 8760-number lists may omit HOYs; partial lists must provide them. All radiation series must be the same length and chronological. No independent sun-position series needs connecting.

Location accepts Ladybug Location, supported numeric-property location objects, or text `latitude,longitude,UTC offset,elevation`, e.g. `33.749,-84.388,-5,320`. If an Eddy3D Location wrapper is unsupported, use LB Location for the same EPW or the explicit text form. Do not substitute a daylight-saving offset for the EPW standard offset.

## PV Solar Conditions

| Input | Unit/type | Description |
|---|---|---|
| Location | Location object / explicit text | Source of solar-position and sky location information |
| Direct Normal Radiation | Hourly Wh/m²; or W/m² selected in menu | Ladybug collection or numeric list |
| Diffuse Horizontal Radiation | Same as DNR | Must align with DNR |
| Global Horizontal Radiation | Same as DNR | Validated for completeness; Ladybug-compatible ground uses sky-patch average, not GHR |
| Weather HOYs | Integer list | Zero-based hourly interval indices 0–8759; inferred from Ladybug collections |
| North | degrees clockwise from model +Y | Negate Ladybug's counterclockwise numeric angle when matching a nonzero north |
| Ground Reflectance | 0–1 | Uniform ground-hemisphere approximation; default 0.2 |
| Run | Boolean | False clears results; true prepares conditions. Unchanged requests are cached |

Right-click controls: numeric HOYs are one-based; numeric radiation is W/m² instead of hourly Wh/m²; Detailed sky (577 rather than 145 patches). Modern Ladybug collection unit/date metadata take precedence over numeric-list assumptions. Never infer a list's indexing convention from its first value.

Outputs: **Solar Conditions** first, then Solar Elevation, Solar Azimuth, Report. No separate Analysis HOYs or Analysis Time Step outputs. Time metadata remain in Conditions.

Native **hourly non-leap EPW** data only in this version. Numeric HOYs identify interval start; solar positions are evaluated at midpoint. A fixed 2001 reference calendar and the EPW standard UTC offset are used, without DST. Sparse selected hours remain separate one-hour intervals. A shorter analysis period does not change timestep, and subhourly weather is not invented by interpolation.

Internally the component runs Radiance **gendaymtx** separately for direct and diffuse radiation, retaining one column per weather interval (no cumulative `-A` flag). It uses installed Ladybug sky vectors, solid angles and RGB broadband weights. Ground reflection follows LB Incident Radiation's simple mirrored-hemisphere average. No interreflection is modeled.

Dependencies: existing project Python environment with numpy/pandas/pvlib, installed Ladybug Python libraries (the normal `~/ladybug_tools/python/lib/python3.10/site-packages` or python3.11 path), and Radiance gendaymtx (`SOLAR_GENDAYMTX`, PATH, or `/usr/local/radiance/bin/gendaymtx`). Missing dependencies produce an error; no silent simplified fallback. Two sky subprocesses have 180-second limits; the enclosing bridge has a 240-second limit.

## PV Solar Exposure

| Input | Unit/type | Description |
|---|---|---|
| Candidate Layout | Grouped | Panel geometry, frames, IDs and stored context |
| Solar Conditions | Grouped | Prepared sky patches and aligned hourly metadata |
| Additional Shading Geometry | Brep list, optional | Extra geometric blockers; layout context and neighboring panel solids already included |
| Analysis Grid Size | meters | Maximum cell size; controls the calculation grid and map cells together; default 0.1 |
| Sensor Offset | meters | Evaluation point displacement in front of panel fronts; default 0.001; at least 10× document tolerance internally |
| Run | Boolean | False clears results. True evaluates the supplied geometry |

| Output | Units/type | Description |
|---|---|---|
| Solar Exposure Results | Grouped | Panel hourly POA, cell irradiation/vertices/areas, panel IDs, reference/reduction results, internal HOYs/calendar/interval metadata and settings |
| Panel Irradiance | W/m² tree | Branch per panel, values in Solar Conditions record order; area-average across analysis cells |
| Panel Irradiation | kWh/m² list | Per-panel accumulated irradiation averaged spatially |
| Panel Incident Solar Energy | kWh list | Irradiation × panel front analysis area; sunlight, not electricity |
| Total Incident Solar Energy | kWh | Sum of per-panel incident sunlight |
| Irradiation Reduction | % list | Reduction from unobstructed same orientation/weather; not electrical shading mismatch loss |
| Panel IDs | text list | Identity matching result branches |
| Report | text | Settings, limitations and interpretation |

No color map, legend, timestamps, Analysis HOYs or time-step sockets on this component. Its geometry is not automatically previewed. Connect Results to the separate map component.

The engine evaluates each rectangular front cell against the prepared sky and mirrored ground vectors, using cosine-weighted visibility. All cells are equal area within a panel. It keeps cell period irradiation while computing panel-average hourly POA without storing a huge cell×hour matrix. Therefore this release supports period cell maps, but **does not output hourly cell maps**. Maximum 1600 cells per panel and 80 million estimated visibility rays. Refine grid and sky density for convergence. No panel removal or optimization is performed.

## PV Exposure Map

Inputs: **Results**, optional fixed **Legend Minimum** and **Legend Maximum** in kWh/m², optional **Legend Plane**.

Outputs: grid-cell colored Exposure Map; Legend geometry with values/units; Cell Irradiation (branch per panel, mesh-face order); uncolored Analysis Mesh; panel Chart Values; matching Chart Labels; CSV-ready Summary Data; Report.

Each mesh face corresponds to one calculation cell and receives its own color. Use fixed legend bounds when comparing layouts. Automatic bounds span **all cells**, not panel averages. Equal-valued cases expand the legend range slightly. Colors are relative to the selected scale; red does not establish installation suitability.

Cell values plus Analysis Mesh can be passed to other spatial visualizers; Chart Values and Labels can feed panel-comparison bar charts. Time-series charts can use the numeric Panel Irradiance output; the grouped Results preserve its time index without additional HOY sockets.

## Electricity and compatibility

PV Electricity accepts both legacy exposure packages and the new Results package. Added optional Air Temperature and Wind Speed sockets at the end preserve existing port positions. Ladybug collections are matched by HOY; full-year lists are selected by HOY; partial numeric lists must already match result order. Without supplied ambient data, the explicit fallback temperature/wind inputs are used and a warning is shown.

The compact current exposure component retains its existing development GUID. The obsolete exposure component has been removed from the current source/build; old definitions using that version require migration. A complete external backup was made before cleanup.

The new example `examples/solar/Solar_Workflow.gh` uses explicitly labeled synthetic hourly data and is not an annual production forecast. Only the current geometry and solar examples remain in the repository; obsolete definitions are in the external backup. Restart Rhino after installation to load new toolbar types.

## Verification after repository cleanup

260 C# checks and 10 Python validation checks passed against the reorganized source. The C# suite covers geometry, products/dropdown, candidate layouts, cell exposure/map behavior, sparse-hour integration and Electricity. The current Solar Workflow definition is preserved with its component IDs unchanged. This is implementation verification, not measurement-based validation of PV yield.
