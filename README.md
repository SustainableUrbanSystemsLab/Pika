# Plug-In Solar

Rhino 8 / Grasshopper components for generating and evaluating building-mounted PV layouts.

## Start here

- **Geometry workflow:** open [Start_Here.3dm](examples/geometry/Start_Here.3dm), keeping millimeters, then [Geometry_Workflow.gh](examples/geometry/Geometry_Workflow.gh). Baseline: **6 panels, 10.005996 m²**.
- **Solar workflow (experimental):** [Solar_Workflow.gh](examples/solar/Solar_Workflow.gh). Its embedded weather is synthetic; replace it with real EPW inputs for research.
- [Installation](docs/INSTALLATION.md) · [Current components](docs/COMPONENTS.md) · [Development](docs/DEVELOPMENT.md).

The scripts and practical usability of **Select PV Panel**, **PV Geometry Generator**, and **PV Candidate Layout** have been verified in the tested geometry workflow. The newer solar components are retained as experimental work; they are not part of the separate three-component advisor package.

## Current components

| Component | Role | Status |
|---|---|---|
| Select PV Panel | Select a catalog product and supply dimensions/specifications | Script verified; practical usability verified |
| PV Geometry Generator | Generate a panel template from dimensions | Script verified; practical usability verified |
| PV Candidate Layout | Arrange panels on one selected planar mounting face, with geometric checks | Script verified; practical usability verified |
| PV Module Specifications | Group and validate electrical inputs; convert the temperature coefficient to fractional units | Under consideration for removal |
| PV Solar Conditions | Normalize hourly radiation and prepare sun/sky data | Experimental |
| PV Solar Exposure | Calculate grid-cell irradiation and panel-average irradiance | Experimental |
| PV Exposure Map | Display the retained cell grid with a legend | Experimental |
| PV Electricity | Calculate simplified module DC and array AC output | Experimental |

PV Module Specifications does not calculate solar exposure or electricity generation. It currently packages and validates module inputs and converts the temperature coefficient from percent to a fraction. Its removal is being considered to simplify the workflow.

You can find C# scripts here: `src/PlugInSolar/Components`

## Repository map

```text
src/PlugInSolar/
  Components/Inputs/       product selection and module specifications
  Components/Geometry/     geometry generator and candidate layout
  Components/Performance/  current solar conditions, exposure, map and electricity
  Core/                    shared placement engine and Python process helper
  Data/                    typed data, catalog and data-tree helpers
  UI/                      product dropdown interface
  Assets/                  required icons and editable SVG originals
  PluginInfo.cs            library identity and icon loading
  PlugInSolar.csproj       build project
python/                    current Solar Conditions backend only
scripts/                   build helpers
examples/geometry/         current three-component main-plugin example
examples/solar/            current experimental solar example
docs/                      current usage and development instructions
tests/                     current C# and Python tests; portable sky fixture
references/                source panel datasheet
requirements-solar.txt     optional solar-backend Python dependencies
```

`build/` and `.venv/` are local, ignored outputs. They are not source files to select in GitHub Desktop. Old component versions and their examples are no longer mixed with current source; an external backup was made before cleanup. See [cleanup notes](docs/CLEANUP.md).

## Requirements

The geometry workflow runs directly in Rhino/Grasshopper; no external Python environment, Ladybug, Eddy3D or Radiance is needed for those three components.

The experimental Solar Conditions component additionally uses a local Python environment, the packages in `requirements-solar.txt`, installed Ladybug patch definitions and Radiance `gendaymtx`. See [solar workflow](docs/SOLAR_WORKFLOW.md).

The current local build was verified in Rhino 8.36 for macOS, .NET 10.0.8. Use the in-Rhino build helper to compile against the running Rhino runtime, or the .NET 10 SDK project for this configuration. Other runtime/platform combinations are not implied to have been tested.

## Modeling boundaries

Candidate Layout tests geometric placement, not energy performance or installation engineering. Panel analysis area is full rectangular front area. Solar Exposure retains local cell irradiation but uses panel-average hourly irradiance for Electricity. The electrical model assumes independent module maximum-power operation with a common inverter; shared-string mismatch and bypass diodes are not modeled. Additional Shading Geometry keeps its existing geometric-blocking behavior; no new tree/transmission model is included.

Household savings and automatic design search are not implemented. A public repository also needs an explicit license before being presented as a reusable open-source release; a project license has not been selected here.
