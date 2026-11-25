---
# org-diagram-1gcz
title: Switch backend from psycopg2 to psycopg 3
status: completed
type: task
created_at: 2026-11-25T07:11:39Z
updated_at: 2026-11-25T07:11:39Z
parent: org-diagram-iypc
---

- [x] psycopg[binary] + psycopg-pool deps
- [x] db.py on psycopg_pool with checkout health check
- [x] callers, tests, docs

## Summary of Changes
- Swapped deps to psycopg 3.3.9 and psycopg-pool 3.3.3.
- db.py uses psycopg_pool.ConnectionPool:
  - dict_row rows
  - prepare_threshold=None, as Supavisor requires
  - DB_POOL_MAX defaults to 1
  - connections are checked on checkout, so a dead pooled connection is replaced instead of returning a 500
- tenant_clause returns LiteralString, which removes the fetch casts.
- Added a pg_terminate_backend recovery test.
- 65 tests pass.
