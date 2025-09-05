#!/usr/bin/env bash
# Thin wrapper to call the PowerShell script on macOS/Linux
set -euo pipefail
SCRIPT_DIR=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &> /dev/null && pwd)
ROOT_DIR=$(cd -- "$SCRIPT_DIR/../.." &> /dev/null && pwd)

if ! command -v pwsh >/dev/null 2>&1; then
  echo "PowerShell 7 (pwsh) is required. Install from https://learn.microsoft.com/powershell/"
  exit 1
fi

pwsh -File "$SCRIPT_DIR/setup-solution.ps1" "$@"

