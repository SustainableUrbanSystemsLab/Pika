#!/usr/bin/env bash
# Set up the Python runtime used by PV Weather and the solar backend.
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
python="$root/.venv/bin/python"
uv_command="${PIKA_UV:-uv}"
if [[ ! -x "$python" ]]; then
  "$uv_command" venv --python "${SOLAR_PYTHON:-3.11}" "$root/.venv"
fi
"$uv_command" pip install --python "$python" --requirement "$root/requirements-solar.txt"
