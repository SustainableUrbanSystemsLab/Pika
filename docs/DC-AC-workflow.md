# Pika DC / AC workflow

PV Weather → PV Energy Calculator Weather input.
PV Candidate Layout → PV Energy Calculator Candidate Layout input.
Select PV Panel Module Specifications → both PV Energy Calculator and PV Electrical Config.
Select Inverter → PV Electrical Config → PV AC Calculator Electrical Config input.
PV Energy Calculator DC Results → PV AC Calculator DC Results input.
PV Loss Assumptions → both calculators Losses inputs (soiling applied once upstream; DC/AC wiring downstream).
PV Energy Calculator Solar Exposure Results → PV Exposure Map.

Hourly display outputs use MM/dd HH:mm in EPW local standard time, at interval start. Numeric outputs remain available separately. DC and AC power are W; total/monthly energy are kWh. Monthly labels include year.

AC uses one independent inverter per panel, PVWatts part-load efficiency, rated-AC clipping, DC wiring loss and AC wiring loss. The loss ledger starts with DC Results (after soiling). It does not model batteries, string topology, household demand, export or cell-level mismatch. AC means inverter output after specified AC wiring loss, not household self-consumption.

Additional Context supports meshes, Breps, surfaces, extrusions, SubD and document-resolved blocks. Curves and points are ignored with warnings. Vegetation meshes are opaque. Existing irradiance assumptions remain: direct-beam ray shading, isotropic diffuse sky and ground without obstruction factors.

Installation uses unique build folders to avoid overwriting a loaded assembly. Restart Rhino after changing Pika.ghlink. Replace existing Energy Calculator instances because the input/output schema changed; reconnect using names above.

Validation on 2026-11-04: real Grasshopper components wired in a separate GH document inside Rhino. Synthetic EPW tests cover dated/numeric outputs, month totals, loss conservation, clipping, capped central-grid selection, Brep/mesh equivalence, mixed geometry and blocks, invalid-input output clearing, Run=False. Amsterdam IWEC EPW (pvlib v0.13.0 test fixture) annual check: 8760 intervals, 12 months, 3 test panels, 5% soiling, 2% DC wiring, 3% AC wiring, DC 905.648168 kWh, AC 810.163829 kWh. Small test annual solve approximately 1.13 seconds; not a general performance guarantee or independent scientific validation.


## Exposure comparison update — 2026-11-08

Energy Calculator Panel DC Energy now outputs `Panel ID; value kWh`; the separate Panel IDs port is removed. DC Results retains numeric hourly values and IDs. Replace existing Energy Calculator instances and reconnect by port name (DC Results and Solar Exposure Results shifted).

Exposure Map defaults to individual-cell color, a zero-based irradiation scale, and the original blue/cyan/yellow/orange/red palette. Cell Detail=True shows individual cells. Both modes use the same maximum cell irradiation, so toggling modes does not change the scale. The Results input accepts the full data tree: connect multiple results directly to one map; all branches automatically share limits. Separate map components still need the same explicit limits. Comparisons must have identical timestamps. Multi-result chart labels receive Layout1/, Layout2/ prefixes in input order to distinguish local IDs. No solar/DC calculation formula changed.

Verified inside Rhino: connected DC/AC calculation and labeled results; uniform panel mode; controlled 411/463/231.5 test values; shared limits for two results; identical colors for identical values; explicit limits; cell detail; invalid limits clear output. Test fixtures verify visualization behavior, not the user's original scene.

Automatic scale update: verified two direct sources on distinct branches without Flatten, six output panels, one common legend, both display modes and explicit limits. Replace existing Exposure Map instances after restart to load tree input access.

Cell Detail now defaults to True. Workload limits are checked before context meshing and shading rays. Errors include workload counts and a tested-by-estimation grid-size suggestion. Rhino regression: 10,712,520 requested rays rejected before tracing; suggested grid completed annual calculation. Limits remain 10 million rays and 1600 cells per panel.


Latest behavior supersedes earlier notes: see Async-exposure-validation.md. Ten-million-ray cap removed; exact .1 m grid runs after 350 ms debounce in cancellable parallel background jobs with bounded caches. Exposure map auto-range now uses actual global cell minimum and maximum, not zero.
