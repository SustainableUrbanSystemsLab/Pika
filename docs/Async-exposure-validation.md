# Asynchronous exposure update — 2026-11-08

## Current behavior
Grid Size defaults to 0.1 m. The 10-million-ray rejection has been removed. The existing 1,600-cell-per-panel guard remains. No spatial or temporal downsampling is introduced.

Each input change invalidates and cancels pending work. Exact calculation begins after inputs remain unchanged for 350 ms. Rhino geometry resolution/meshing occurs on the UI thread after this delay; ray tracing runs on a background task using immutable panel snapshots and private worker mesh copies. Up to eight workers are used, additionally bounded by processor and context-face counts. A process-wide gate prevents separate calculator instances from oversubscribing the CPU. Cancellation is checked between cells and periodically within hourly ray loops; an individual native intersection call cannot be interrupted.

No approximate preview is emitted. Outputs stay empty while pending, preventing downstream AC/map components from consuming stale results. Scheduled solutions publish the latest completed result. Run=False, upstream expiration, component removal and document close/unload cancel jobs. Geometry edits, including block-definition edits, enter the cache fingerprint. A single cached context mesh and up to two size-limited exact exposure results are retained per component. Thermal/module/loss changes can reuse completed exposure; their electrical calculations still update.

Exposure Map uses the global cell minimum and maximum across its entire Results tree, in both modes. Manual limits override the defaults. Constant-valued data receives a small nonnegative display-range padding to avoid division by zero. Original blue/cyan/yellow/orange/red palette and Cell Detail=True remain. No Merge or Flatten is required.

## Rhino validation
Tests ran in separate temporary GH documents, not the user's original scene. Temporary documents were removed afterward. Synthetic 8760-hour weather, 18 panels, 0.1 m grid, 3366 cells, 11,006,820 direct-light queries.

- Connected asynchronous DC → AC → Exposure Map completed beyond the former limit.
- Independent pre-refactor serial algorithm (only ray cap removed) versus new parallel algorithm: maximum difference 0 for cell irradiation, panel hourly POA, beam visibility and reference POA in the small fixture.
- Small-context serial 1.282 s vs parallel 1.349 s: no speedup; worker setup overhead dominates.
- Dense fixture: 375 surrounding box obstacles, combined context/panels 24,331 mesh faces. Serial 29.326 s vs parallel 7.779 s (3.77x); maximum cell irradiation difference 0. Legacy timing includes meshing; parallel timer starts with the prepared mesh, so this is not a complete end-to-end UI comparison.
- Changing soiling to 5% reused exposure, updated DC by exactly 0.95, and solved the connected test graph in 0.237 s.
- Tilt changed during a running job: cancellation token signaled, prior task reached Canceled, pending outputs empty, eventual output tilt matched latest layout.
- Run=False cleared both DC and AC; re-enabling reused the exact cached result.
- Automatic legend used measured nonzero global min 650.0490939534847 and max 2062.5260871832643 in the latest tilt test.

These are local fixture timings, not an assurance of two-second completion for arbitrary buildings. UI snapshot preparation, result serialization/output formatting, scene complexity and competing calculators affect total response time. The solar model remains direct-beam geometric shading plus unobstructed isotropic sky and ground terms; cell-center and hourly sampling limitations remain.
