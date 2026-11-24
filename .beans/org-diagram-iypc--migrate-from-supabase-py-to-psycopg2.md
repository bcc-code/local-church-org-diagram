---
# org-diagram-iypc
title: Migrate from supabase-py to psycopg2
status: completed
type: epic
priority: normal
created_at: 2026-11-24T17:23:49Z
updated_at: 2026-11-24T17:27:42Z
---

Replace supabase-py with plain SQL over psycopg2 (Supabase stays as Postgres host). Notebook excluded.

## Summary of Changes
All backend DB access now goes through psycopg2 (db.py, queries.py, mutations.py), and the supabase dependency is removed. Docs and env use DATABASE_URL; plans/ deleted. 47 tests pass against postgres:17, and CI runs a postgres service.

Follow-ups: replace schema.sql with a real pg_dump; migrate the tools notebook.
