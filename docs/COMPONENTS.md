# Current component guide

## Geometry and product selection

| Component | Main inputs | Main outputs |
|---|---|---|
| Select PV Panel | Catalog dropdown or Product ID | Grouped Dims/Specs; dimensions; reference STC electrical properties |
| PV Geometry Generator | Dims or manual width/height/thickness in m; reference plane | Closed panel, front face, normal, area m², frame, grouped Template |
| PV Candidate Layout | Template; planar mounting face; tilt; column/row gaps; landscape; offset; edge setback; optional obstacles | Grouped Layout; accepted panel solids/faces; count; area m²; IDs; mounting direction; actual tilt; Report |
| PV Module Specifications | Grouped Specs or manual STC electrical inputs | Validated Specs and individual values |

Product catalog: seven snapshots (three Trina NEG9R.28 variants and four Heliene 60M G1 All Black ratings). This is not live inventory. The Heliene source datasheet is under `references/panel-datasheets/`; Trina's source URL is stored in catalog metadata.

The geometry reference plane is the front center, thickness extending behind it. Tilt in Candidate Layout is relative to the mounting surface; 0° means parallel. Spacing is between projected footprints. Use the menu's Flip control to correct mounting direction. The selected region must be a single planar face with actual holes/trims where openings are excluded.

## Experimental performance components

| Component | Main inputs | Main outputs |
|---|---|---|
| PV Solar Conditions | Location; DNR/DHR/GHR; optional HOYs; north; reflectance; Run | Grouped Conditions; solar elevation/azimuth; Report |
| PV Solar Exposure | Candidate Layout; Conditions; optional additional geometry; grid; sensor offset; Run | Grouped Results; panel POA W/m²; panel irradiation kWh/m²; incident solar kWh; reduction %; IDs; Report |
| PV Exposure Map | Results; optional fixed legend bounds and plane | Cell-colored mesh; legend; cell irradiation; raw mesh; chart values/labels; summary rows |
| PV Electricity | Results; module Specs; AC rating; non-shading losses; thermal settings; optional ambient series | Grouped electricity results; panel DC; array AC; period/monthly energy; yield; IDs; Report |

HOYs/interval timing remain inside grouped Conditions/Results rather than separate exposure-output sockets. Use the map component for visualization. No panel removal, automatic search or household economics is implemented.
