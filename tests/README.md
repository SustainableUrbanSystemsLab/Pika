# Current verification

Run `scripts/run_checks_in_rhino.py` in Rhino's Python 3 ScriptEditor after opening Grasshopper, with a document window open. The wired-solve checks need `RhinoDoc.ActiveDoc`; without it they fail with "component default solve has no errors".

| Check file | Coverage |
|---|---|
| GeometryChecks.cs | Dimensions, reference planes, normal/area, units and invalid inputs |
| ProductChecks.cs | Seven products, grouped data, overrides and custom module specifications |
| DropdownChecks.cs | Product-menu interaction, persistence and external override |
| CandidateLayoutChecks.cs | Current layout engine, collisions, unit conversions, persistence and connected workflow |
| SolarWorkflowChecks.cs | Current sky exposure, cell maps, sparse-hour integration, Electricity and output interfaces |
| test_solar_conditions.py | Backend validation and rejection of removed bridge operations |
| data/sky_fixture.json | Portable small sky fixture for C# tests |

Latest cleanup verification: **260 C# checks + 10 Python tests passed**.

Obsolete tests tied to removed component interfaces have been removed from the working repository; they remain in the pre-cleanup backup. No test depends on the developer's temporary staging directory.
