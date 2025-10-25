# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a monorepo for an organizational chart/diagram visualization tool. It displays hierarchical org structures using d3-org-chart, with data sourced from either:

- **Live mode**: Supabase database + BCC Core API (for person details)
- **Demo mode**: Static JSON files in `backend/demo_requests/`

The Flask backend serves both the API endpoints and the built Vue frontend as a single application.

## Architecture

### Frontend (`frontend/`)

- **Stack**: Vue 3 + TypeScript + Vite + Tailwind CSS 4
- **Visualization**: d3-org-chart (loaded via CDN in `index.html`)
- **Data source**: Fetches `/org-data.json` (static file) which contains the org structure
- **Main component**: `src/App.vue` - renders the org chart visualization

### Backend (`backend/`)

- **Stack**: Flask (Python)
- **Serves**: Built Vue frontend from `public/` directory + API endpoints
- **API Endpoints**:
  - `GET /api/tree` - Returns org structure from Supabase (groups with member UIDs)
  - `GET /api/persons?uids=...` - Returns person details from BCC Core API
- **Frontend Routes**:
  - `GET /` - Serves index.html
  - `GET /<path>` - Catch-all for SPA routing (serves static files or index.html)
- **Demo Mode**: Set `DEMO_MODE=1` in `.env` to serve static responses from `demo_requests/` directory for `/api/*` routes

### BCC API Client (`bcc_api/`)

- Auto-generated Python client from Swagger/OpenAPI spec for BCC Core API
- Used to fetch person details (names, etc.) via OAuth2 client credentials flow
- Install with `pip install -e ./bcc_api` (already in requirements.txt)

### Database

- **Supabase** tables:
  - `groups` - Org hierarchy (id, name, parent_id, tenant_id)
  - `group_membership` - Links persons (bcc_person_uid) to groups
- Multi-tenant support via `TENANT_ID` environment variable (optional)

### Data Import (`tools/`)

- `import_from_excel.ipynb` - Jupyter notebook to import org structure from Excel into Supabase
- Processes hierarchical Excel data (L1/L2/L3 columns) into groups table

## Common Development Commands

### Frontend

```bash
cd frontend
npm install          # Install dependencies
npm run dev          # Start dev server (http://localhost:5173)
npm run build        # Build for production
npm run preview      # Preview production build
```

### Backend

```bash
cd backend
pip install -r ../requirements.txt  # Install dependencies (from repo root)

# Development: Build frontend first, then run Flask
cd ../frontend && npm run build && cd ../backend
python app.py                       # Run Flask server (http://localhost:5000)

# Demo mode (no API/DB required)
DEMO_MODE=1 python app.py
```

### Full Stack (Production-like)

```bash
# Build frontend
cd frontend
npm run build

# Run Flask (serves both frontend + API)
cd ../backend
python app.py
# Visit http://localhost:5000
```

### Root-level dependencies

Install Python dependencies from repo root:

```bash
pip install -r requirements.txt  # Installs Flask, Supabase, BCC API client, etc.
```

## Environment Setup

### Backend `.env` file

Required for live mode (in `backend/.env`):

```bash
SUPABASE_URL=https://...
SUPABASE_KEY=...
BCC_OAUTH_CLIENT_ID=...
BCC_OAUTH_CLIENT_SECRET=...
TENANT_ID=...  # Optional, for multi-tenant filtering
```

For demo mode, only set:

```bash
DEMO_MODE=1
```

### Tools `.env` file

Required for data import notebook (in `tools/.env`):

```bash
SUPABASE_URL=...
SUPABASE_KEY=...
```

## Key Integration Points

1. **Flask ↔ Frontend Build**: Flask serves built frontend from `backend/public/` folder (Vite builds directly to this location)
2. **Frontend ↔ Static Data**: Frontend loads `/org-data.json` from static files (option 1)
3. **Frontend ↔ Backend API**: Frontend can fetch from `/api/tree` and `/api/persons` (option 2)
4. **Backend ↔ Supabase**: Flask app queries `groups` table with joined `group_membership` data
5. **Backend ↔ BCC API**: OAuth2 flow to fetch person details by UIDs
6. **Demo Mode Flow**: All `/api/*` endpoints serve static JSON from `demo_requests/{path}.json` (without `/api` prefix)

## Data Flow (Live Mode)

1. Flask serves built Vue app at `http://localhost:5000/`
2. Frontend fetches backend `/api/tree` endpoint
3. Backend queries Supabase for groups + member UIDs
4. Backend calls BCC Core API `/v2/persons` with UIDs to get names
5. Backend returns combined data to frontend
6. Frontend renders org chart with d3-org-chart

## Important Notes

- The `bcc_api/` directory contains generated code - do not manually edit
- Demo mode is useful for development without credentials or external API access
- **Frontend must be built before running Flask** - Vite builds to `backend/public/`, which Flask serves
- The frontend currently uses a static JSON file (`org-data.json`) by default - see `App.vue` for how to switch to `/api/tree`
- d3-org-chart is loaded from CDN, not npm - see `frontend/index.html`
- OAuth tokens are automatically renewed when expired via `requests_oauth2client`
- All API routes are prefixed with `/api/` to avoid conflicts with frontend routing
- Commit messages should be short and concise