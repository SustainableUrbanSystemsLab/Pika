# Releasing

Package: Yak, Rhino 8 for Mac only (the project targets `net8.0`, compiled against RhinoCommon 8.27, and references Rhino's Mac `System.Windows.Forms`). Builds need the Rhino 8 app installed, so GitHub-hosted runners cannot build it; releases are built and pushed from a Mac.

## Branches and channels

| Branch | Purpose | Yak version | Who can install it |
|---|---|---|---|
| `dev` | Day-to-day work; PRs target it. Never pushed to Yak. | none (local builds only) | nobody |
| `pre-release` | Beta candidates, merged from `dev` | `X.Y.Z-beta.N` | `yak install Pika X.Y.Z-beta.N`, or Package Manager with prereleases shown |
| `release` | Stable, merged from `pre-release` | `X.Y.Z` | everyone |

`<Version>` in `src/PlugInSolar/PlugInSolar.csproj` is the only place `X.Y.Z` lives; `scripts/build_yak.sh` passes it to Yak, overriding `yak/manifest.yml`. Semver: new feature → MINOR, fix → PATCH, breaking old `.gh` definitions (component GUID or parameter-order change) → MAJOR.

**A pushed Yak version is burned.** It can be yanked but never re-pushed, so build the exact file you tested.

## Before the first release (one-time)

1. The package name is `Pika` (`name:` in `yak/manifest.yml`), and it is permanent once pushed. `yak search` is a text search of Yak only, so also search food4rhino.com for it before the first push.
2. Choose a license and add `LICENSE` (README says none is selected yet).
3. Confirm `authors:` and `url:` in `yak/manifest.yml`.
4. Minimum Rhino is 8.27: the `RhinoCommon`/`Grasshopper` package versions in the csproj set the Yak tag (`rh8_27-mac`). `0.5.0-beta.1` was load-tested in Rhino 8.32 on .NET 8.0.14 (all 8 components construct); it has not been run in Rhino 8.27 itself.
5. `yak login` (opens a browser; needs a McNeel account).

## Beta (from `pre-release`)

```bash
git switch pre-release && git merge dev
scripts/build_yak.sh beta.1          # build/yak/<name>-X.Y.Z-beta.1-rh8_<n>-mac.yak
```

Test the built file before pushing (a `.yak` is a zip; unzip it into a Rhino 8 packages folder on a clean profile), open `examples/geometry/Start_Here.3dm` + `Geometry_Workflow.gh`, expect **6 panels / 10.005996 m²**. Then:

```bash
"/Applications/Rhino 8.app/Contents/Resources/bin/yak" push build/yak/<file>.yak
git tag v0.5.0-beta.1 && git push origin pre-release v0.5.0-beta.1
gh release create v0.5.0-beta.1 build/yak/<file>.yak --prerelease --verify-tag
```

Increment `beta.N` for each candidate.

## Stable (from `release`)

```bash
git switch release && git merge pre-release
scripts/build_yak.sh                 # X.Y.Z, no suffix
```

Repeat the example check, then `yak push` the file, `git tag vX.Y.Z`, push `release` and the tag, and create a GitHub release from the tag with the `.yak` attached. Merge `release` back to `dev` so the next cycle bumps `<Version>` from there.

## Yank

```bash
yak yank Pika X.Y.Z-beta.1
```

## Solar Conditions in a Yak install

PV Solar Conditions shells out to Python. A Yak install has no repository `.venv`, so users set `SOLAR_PYTHON` to an interpreter with `requirements-solar.txt` installed (Rhino started from the Dock does not see shell variables; use `launchctl setenv SOLAR_PYTHON <path>` and restart Rhino), and still need Ladybug Tools and Radiance `gendaymtx` (see [Solar workflow](SOLAR_WORKFLOW.md)). The geometry components need none of this.
