#!/bin/bash
# Oryx POST_BUILD_SCRIPT_PATH: builds the Vue frontend during Kudu deployment.
# Runs after Oryx's Python build step, with the repo root as the working directory.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/../frontend"

corepack pnpm@11.22.0 install --frozen-lockfile
corepack pnpm@11.22.0 run build
