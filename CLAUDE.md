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

- **Stack**: Flask (Python) + Authlib (for OIDC)
- **Serves**: Built Vue frontend from `public/` directory + API endpoints
- **API Endpoints**:
  - `GET /api/tree` - Returns org structure from Supabase (groups with member UIDs)
  - `GET /api/persons?uids=...` - Returns person details from BCC Core API
  - `GET /api/user` - Returns current authenticated user info
- **Authentication Routes**:
  - `GET /login` - Initiates OIDC login flow with BCC
  - `GET /authorize` - OIDC callback endpoint
  - `GET /logout` - Clears user session
- **Frontend Routes**:
  - `GET /` - Serves index.html
  - `GET /<path>` - Catch-all for SPA routing (serves static files or index.html)
- **Authentication**: OpenID Connect via BCC login (https://login.bcc.no)
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

## Local Development Workflow (Recommended)

For the best development experience with hot module replacement (HMR):

**Terminal 1 - Run Flask backend:**
```bash
cd backend
python app.py                    # Runs on http://localhost:5000
# OR for demo mode:
DEMO_MODE=1 python app.py
```

**Terminal 2 - Run Vite dev server:**
```bash
cd frontend
npm run dev                      # Runs on http://localhost:5173
```

**Develop on:** `http://localhost:5173`

The Vite dev server proxies `/api/*` requests to Flask backend, giving you:
- Instant HMR updates for frontend changes
- Real backend API integration
- Best debugging experience

## Common Development Commands

### Frontend

```bash
cd frontend
npm install          # Install dependencies
npm run dev          # Start dev server with HMR (http://localhost:5173)
npm run build        # Build for production
npm run preview      # Preview production build
```

### Backend

```bash
cd backend
pip install -r ../requirements.txt  # Install dependencies (from repo root)
python app.py                       # Run Flask server (http://localhost:5000)

# Demo mode (no API/DB required)
DEMO_MODE=1 python app.py
```

### Production Build

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
# Supabase Configuration
SUPABASE_URL=https://...
SUPABASE_KEY=...

# BCC OAuth2 Client Credentials (for API access)
BCC_OAUTH_CLIENT_ID=...
BCC_OAUTH_CLIENT_SECRET=...

# BCC OpenID Connect (for user authentication)
BCC_OIDC_CLIENT_ID=...
BCC_OIDC_CLIENT_SECRET=...

# Flask Session Secret (generate with: python -c "import os; print(os.urandom(24).hex())")
FLASK_SECRET_KEY=...

# Optional: Multi-tenant filtering
TENANT_ID=...
```

For demo mode, only set:

```bash
DEMO_MODE=1
```

A `.env.example` file is provided in the `backend/` directory with all available configuration options.

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
5. **Backend ↔ BCC API**: OAuth2 client credentials flow to fetch person details by UIDs
6. **User Authentication**: OpenID Connect flow with BCC for user login (session-based)
7. **Demo Mode Flow**: All `/api/*` endpoints serve static JSON from `demo_requests/{path}.json` (without `/api` prefix)

## Data Flow (Live Mode)

1. Flask serves built Vue app at `http://localhost:5000/`
2. Frontend fetches backend `/api/tree` endpoint
3. Backend queries Supabase for groups + member UIDs
4. Backend calls BCC Core API `/v2/persons` with UIDs to get names
5. Backend returns combined data to frontend
6. Frontend renders org chart with d3-org-chart

## Authentication Flow (OIDC)

1. User visits `/login` endpoint
2. Backend redirects to BCC login page (https://login.bcc.no)
3. User authenticates with BCC credentials
4. BCC redirects back to `/authorize` callback with authorization code
5. Backend exchanges code for tokens (access_token, id_token)
6. Backend extracts user info from ID token (email, name, sub)
7. User info stored in Flask session
8. User redirected to homepage
9. Frontend can check `/api/user` to get current user info
10. User can logout via `/logout` endpoint (clears session)

## Important Notes

- The `bcc_api/` directory contains generated code - do not manually edit
- Demo mode is useful for development without credentials or external API access
- **For local development**, use Vite dev server (`npm run dev`) with Flask backend running in parallel
- **For production**, build frontend first (`npm run build`) - Vite builds to `backend/public/`, which Flask serves
- Vite dev server proxies `/api/*` requests to Flask backend at `http://localhost:5000`
- The frontend currently uses a static JSON file (`org-data.json`) by default - see `App.vue` for how to switch to `/api/tree`
- d3-org-chart is loaded from CDN, not npm - see `frontend/index.html`
- **Two OAuth flows**:
  - OAuth2 client credentials: Used by backend to access BCC Core API (tokens auto-renewed via `requests_oauth2client`)
  - OpenID Connect: Used for user authentication (session-based, managed by Authlib)
- User sessions are stored server-side with Flask's session management
- OIDC discovery endpoint: `https://login.bcc.no/.well-known/openid-configuration`
- All API routes are prefixed with `/api/` to avoid conflicts with frontend routing
- Commit messages should be short and concise