#!/bin/bash
# Azure App Service startup script for Flask app with Gunicorn

# Number of workers (recommended: 2-4 x CPU cores)
# Azure App Service Basic tier: 1 core, Standard: 1-4 cores
WORKERS=${GUNICORN_WORKERS:-4}

# Timeout for worker processes (seconds)
TIMEOUT=${GUNICORN_TIMEOUT:-120}

# Bind to port 8000 (Azure App Service forwards to this)
PORT=${PORT:-8000}

echo "Starting Gunicorn with $WORKERS workers on port $PORT..."

cd $(dirname $0)

gunicorn app:app \
    --workers $WORKERS \
    --timeout $TIMEOUT \
    --bind 0.0.0.0:$PORT \
    --access-logfile - \
    --error-logfile - \
    --log-level info
