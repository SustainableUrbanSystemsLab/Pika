#!/usr/bin/env bash
# Build/install the current Rhino 8 Mac library; no legacy component wrappers.
set -euo pipefail
SOLAR_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SOLAR_DOTNET="${SOLAR_DOTNET:-dotnet}"
SOLAR_PYTHON="${SOLAR_PYTHON:-python3}"
mkdir -p "$SOLAR_ROOT/build"
SOLAR_OUTPUT="$(mktemp -d "$SOLAR_ROOT/build/pika-XXXXXXXX")"
SOLAR_LIBRARY="$HOME/Library/Application Support/McNeel/Rhinoceros/8.0/Plug-ins/Grasshopper (b45a29b1-4343-4035-989e-044e8580d9cf)/Libraries"
if ! command -v "$SOLAR_DOTNET" >/dev/null 2>&1; then
  echo 'Use scripts/build_in_rhino.py, or install the .NET 8+ SDK and set SOLAR_DOTNET.' >&2
  exit 1
fi
if command -v "${PIKA_UV:-uv}" >/dev/null 2>&1; then
  "$SOLAR_ROOT/scripts/setup_python.sh"
else
  if [[ ! -x "$SOLAR_ROOT/.venv/bin/python" ]]; then
    "$SOLAR_PYTHON" -m venv "$SOLAR_ROOT/.venv"
  fi
  "$SOLAR_ROOT/.venv/bin/python" -m pip install -r "$SOLAR_ROOT/requirements-solar.txt"
fi
"$SOLAR_DOTNET" build "$SOLAR_ROOT/src/Pika/Pika.csproj" --configuration Release --output "$SOLAR_OUTPUT" -p:UseSharedCompilation=false
mkdir -p "$SOLAR_LIBRARY"
printf '%s\n' "$SOLAR_OUTPUT" > "$SOLAR_LIBRARY/Pika.ghlink.new"
mv "$SOLAR_LIBRARY/Pika.ghlink.new" "$SOLAR_LIBRARY/Pika.ghlink"
echo "Built: $SOLAR_OUTPUT/Pika.gha"
echo 'Restart Rhino after rebuilding a loaded plugin.'
