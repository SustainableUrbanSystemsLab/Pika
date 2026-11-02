# Installation and examples

## Local research/development plugin

1. Build using [the development instructions](DEVELOPMENT.md).
2. Keep the output at `build/grasshopper/`. That location is also used to find the repository root and optional `.venv` environment.
3. For the Mac setup, `scripts/build_plugin.sh` writes a library link into Grasshopper's components folder. If using `scripts/build_in_rhino.py`, either retain the existing link to this output folder or create a local `.ghlink` pointing to it in your Grasshopper components folder.
4. Quit and restart Rhino after replacing a loaded plugin.
5. Open the appropriate example below.

Do not install two copies of the main `PlugInSolar.gha`. The separate advisor review library has different IDs and its own toolbar, so it can coexist.

## Geometry example

Open `examples/geometry/Start_Here.3dm` first, retaining millimeters; use ZoomExtents if necessary. Then open `examples/geometry/Geometry_Workflow.gh` in Grasshopper. All GH geometry is internalized.

Baseline: Heliene 320W, tilt 0°, portrait, row/column gaps 0.1 m, offset 0.05 m, setback 0.1 m. Expected **6 panels / 10.005996 m²**. The `.ghx` is a source-readable version of the same definition; open either GH or GHX.

Connections: Select PV Panel **Dims** → Geometry Generator **Dims**; Geometry Generator **Template** → Candidate Layout **Template**; selected planar surface → **S**; optional building/obstacles → **Context**.

All distance inputs are meters regardless of Rhino units. Right-click Candidate Layout to Flip direction or rotate the grid. Its regular arrangement checks physical fit and collisions, not solar performance.

## Solar example

Open `examples/solar/Solar_Workflow.gh` in a millimeter Rhino model. The embedded synthetic day is a demonstration, not an annual forecast. Read [Solar workflow](SOLAR_WORKFLOW.md) before interpreting results.

## Troubleshooting

- Missing components: rebuild/restart and check the actual library load error. Use the current examples rather than old legacy definitions.
- Wrong geometry scale: the supplied embedded geometry is millimeters, but distance inputs are meters.
- No panels: inspect Report, direction/Flip, panel dimensions, setback and obstacles.
- Missing Python/sky dependencies: only the experimental solar preparation requires them; geometry components remain independent.
- The removed legacy layout, exposure and original Python component set are not in the new build. Old definitions containing those removed components may need manual migration. The full pre-cleanup backup preserves the previous files/build.
