#!/bin/bash
# Oryx POST_BUILD_SCRIPT_PATH: builds the Vue frontend during Kudu deployment.
# Runs after Oryx's Python build step, with the repo root as the working directory.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/../frontend"

corepack pnpm install --frozen-lockfile
corepack pnpm run build
rm -rf node_modules