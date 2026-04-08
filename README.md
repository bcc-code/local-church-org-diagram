# Local Church Org Diagram

This project contains a Vue.js frontend (TypeScript) and a Flask backend (Python).

## Structure

- `frontend/` — Vue.js 3 + TypeScript app (Vite)
- `backend/` — Flask app

## Getting Started

### Frontend

1. Install dependencies:
   ```sh
   cd frontend
   npm install
   ```
2. Run the development server:
   ```sh
   npm run dev
   ```

### Backend

1. Install dependencies:
   ```sh
   pip install -r requirements.txt
   ```
2. Run the Flask server:
   ```sh
   cd backend
   flask run
   ```

## DEMO_MODE

Set the environment variable `DEMO_MODE=1` to enable demo mode in the backend Flask app.
Configure it in your `.env` file or in the terminal session as shown below.
When enabled, all API endpoints (except `/`) will return static JSON responses from files in the `backend/demo_requests/` directory, matching the request path (e.g., `/tree` returns `demo_requests/tree.json`).

**Usage:**

```sh
export DEMO_MODE=1
flask run
```

This is useful for development and testing without requiring live backend or external API access.
