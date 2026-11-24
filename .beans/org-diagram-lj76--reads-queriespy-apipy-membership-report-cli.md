---
# org-diagram-lj76
title: 'Reads: queries.py, api.py, membership report + CLI'
status: completed
type: task
priority: normal
created_at: 2026-11-24T17:23:50Z
updated_at: 2026-11-24T17:26:40Z
parent: org-diagram-iypc
---

- [x] queries.py read functions
- [x] api.py uses queries
- [x] report/cli use conn
- [x] tests/test_queries.py

## Summary of Changes
Added queries.py with six parameterized read functions; /tree uses a LEFT JOIN with the tenant filter in ON, and person groups is now a single query. api.py, MembershipReport and cli.py now read through a psycopg2 connection. Fixed the cli `root` param crash. 11 integration tests pass against postgres:17.
