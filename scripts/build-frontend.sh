#!/bin/bash
# Oryx POST_BUILD_SCRIPT_PATH: builds the Vue frontend during Kudu deployment.
# Runs after Oryx's Python build step, with the repo root as the working directory.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/../frontend"

# Oryx's build container ships Node 18, but pnpm 11 requires Node >=22.13.
NODE_VERSION="22.23.2"
NODE_DIR="/tmp/node-v${NODE_VERSION}-linux-x64"
if [ ! -x "$NODE_DIR/bin/node" ]; then
  curl -fsSL "https://nodejs.org/dist/v${NODE_VERSION}/node-v${NODE_VERSION}-linux-x64.tar.gz" \
    | tar -xz -C /tmp
fi
export PATH="$NODE_DIR/bin:$PATH"

corepack pnpm install --frozen-lockfile
corepack pnpm run build
