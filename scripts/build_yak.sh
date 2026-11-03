#!/usr/bin/env bash
# Stage and build the Yak package (Rhino 8 Mac). Builds only; never pushes.
# Usage: scripts/build_yak.sh [prerelease-suffix]   e.g. beta.1 -> 0.5.0-beta.1 (omit for a stable build)
set -euo pipefail
SOLAR_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SOLAR_DOTNET="${SOLAR_DOTNET:-dotnet}"
YAK="${YAK:-/Applications/Rhino 8.app/Contents/Resources/bin/yak}"
STAGE="$SOLAR_ROOT/build/yak"
PROJECT="$SOLAR_ROOT/src/PlugInSolar/PlugInSolar.csproj"
# The csproj <Version> is the single source; --version overrides manifest.yml.
VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$PROJECT")"
[[ -n "$VERSION" ]] || { echo 'No <Version> in PlugInSolar.csproj.' >&2; exit 1; }
[[ -z "${1:-}" ]] || VERSION="$VERSION-$1"
rm -rf "$STAGE"
"$SOLAR_DOTNET" build "$PROJECT" --configuration Release --output "$STAGE" -p:UseSharedCompilation=false -p:DebugType=none
cp "$SOLAR_ROOT/yak/manifest.yml" "$SOLAR_ROOT/yak/icon.png" "$STAGE/"
cp -R "$SOLAR_ROOT/examples" "$STAGE/examples"
(cd "$STAGE" && "$YAK" build --platform mac --version "$VERSION")
ls -l "$STAGE"/*.yak
echo 'Built only. Publish with docs/RELEASING.md once the package name is final.'
