# Repository cleanup — November 2, 2026

The user selected: **keep the newer solar work; remove obsolete versions**.

## Retained

- Select PV Panel, PV Geometry Generator and PV Candidate Layout.
- Supporting custom module-specification input.
- Current Solar Conditions, compact Solar Exposure, Exposure Map and Electricity prototypes.
- Shared data/catalog/UI/placement/process helpers required by those components.
- One main geometry workflow example and one current solar example.
- Relevant tests, source icons and panel reference datasheet.

## Removed from the active tree

- Original standalone Python Script Mode component set and its JSON component catalogs.
- Original catalog-driven C# component set: Location, Clear Sky, Sun Position, Panel Orientation, Panel Irradiance, PV Power, Energy Yield, EPW Weather and PV Power Weather.
- Legacy PV Layout Generator and the first PV Solar Exposure component implementation.
- Old tutorial/versioned examples and outdated setup/component documents.
- Duplicate icon exports, unused dark/48px variants and unrelated old icons.
- Old Python calculation package, packaging metadata and tests specific to the removed workflow.
- Tracked absolute-path Grasshopper link; future local `.ghlink` files are ignored.

Shared code was extracted before removing old component scripts: the current candidate layout uses Core/PvLayoutEngine; performance components share Data/PvDataTrees; Solar Conditions uses Core/PythonBridge. Removing a visible legacy component did not mean deleting a required algorithm.

## Backup and Git status

A complete snapshot of the pre-cleanup source, examples, documentation, reference files and generated builds was created **outside the repository**, before edits. Git history was not rewritten, committed, reset, or pushed. The local `.venv` and `.git` directories were left in place. Backup ZIP excludes `.git`, `.venv` and Python cache folders; it contains a relative-path SHA256 manifest for the preserved files.

The local generated library remains at build/grasshopper to preserve the existing Mac library-link location. An atomic replacement updates it without overwriting the memory-mapped file already loaded in a running Rhino session. Restart Rhino to load the cleaned component list.

Older GH definitions that contain removed component types require migration to the current workflows. The external backup includes the prior build and examples for recovery.
