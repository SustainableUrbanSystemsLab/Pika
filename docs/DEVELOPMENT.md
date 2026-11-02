# Development

## Current source layout

The single C# project is `src/PlugInSolar/PlugInSolar.csproj`. Components are grouped under Inputs, Geometry and Performance. Shared calculation/data/UI code is outside the component classes. `python/` contains only the current solar-preparation backend.

Main component GUIDs are unchanged for the retained components. The obsolete original component set and older layout/exposure implementations are intentionally no longer compiled. Do not restore an old full `Components.cs` into this project; it would reintroduce obsolete types and helpers.

## Build inside Rhino (no standalone SDK)

1. Start Rhino 8 and Grasshopper.
2. In Rhino's ScriptEditor open the saved `scripts/build_in_rhino.py` file as Python 3 and run it.
3. It compiles the current C# tree using Rhino's bundled compiler and copies the two Python helper files to `build/grasshopper/`.
4. Restart Rhino before testing a replacement of an already loaded library.

This method needs no external Python packages for compilation. The experimental Solar Conditions **runtime** still needs the dependencies below.

## Alternative SDK build/install on Mac

`scripts/build_plugin.sh` uses a .NET 10 SDK, installs the pinned solar-backend requirements into the repository's ignored `.venv`, builds the project, and writes the Mac Grasshopper library link. Configure `SOLAR_DOTNET` or `SOLAR_PYTHON` if necessary. The current project defaults to the standard Rhino 8 Mac application path; `RhinoDir` can be overridden deliberately.

The in-Rhino compiler follows the running Rhino runtime. The SDK project targets the tested .NET 10 configuration; do not assume a resulting .NET 10 binary loads in an older runtime.

## Optional solar runtime

Create `.venv` in the repository root and install `requirements-solar.txt`. Ladybug Python definitions and Radiance `gendaymtx` are also needed by Solar Conditions. Its process helper locates the repository by the fixed `build/grasshopper` output layout; keep that layout intact.

The old editable `plug_in_solar` Python package is no longer used by the current components. An existing local environment may still contain its old editable registration, but no current code imports it. Do not upload `.venv` to GitHub.

## Checks

- In Rhino ScriptEditor, run the saved `scripts/run_checks_in_rhino.py` file. It builds a separate test assembly and executes current C# checks without replacing the active canvas.
- With the Python runtime requirements installed: `python -m unittest discover -s tests -p 'test_*.py'`.
- The C# exposure tests use `tests/data/sky_fixture.json`; they do not depend on temporary developer paths or regenerate weather in order to run.
- The small Radiance sky fixture is test data, not a bundled annual weather forecast.

## GitHub

Review all additions, modifications and deletions in GitHub Desktop, then commit and push when ready. This cleanup changes the local working folder only; it does not create commits or push anything automatically.

Source, current examples/docs/tests and references belong in Git. Local builds, environments, cache files, absolute-path `.ghlink` files and private backups are ignored. The advisor ZIP is a separate delivery artifact outside this repository.
