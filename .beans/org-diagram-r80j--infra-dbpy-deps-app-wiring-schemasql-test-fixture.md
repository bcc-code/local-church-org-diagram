---
# org-diagram-r80j
title: 'Infra: db.py, deps, app wiring, schema.sql, test fixture, CI postgres'
status: completed
type: task
priority: normal
created_at: 2026-11-24T17:23:50Z
updated_at: 2026-11-24T17:27:11Z
parent: org-diagram-iypc
---

- [x] db.py pool/get_conn/transaction/tenant_clause/to_json
- [x] app.py wiring; swap supabase for psycopg2-binary
- [x] schema.sql, db_conn fixture, CI postgres service

## Summary of Changes
Added db.py: a lazy per-process ThreadedConnectionPool, one connection per app context, a transaction() context manager, and broken connections discarded at teardown. app.py now calls db.init_app with DATABASE_URL. Swapped the supabase dependency for psycopg2-binary (plus types-psycopg2 for dev). Added a reconstructed schema.sql, the db_conn fixture, test_db.py, and a postgres:17 service in CI.
