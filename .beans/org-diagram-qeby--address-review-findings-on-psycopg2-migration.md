---
# org-diagram-qeby
title: Address review findings on psycopg2 migration
status: completed
type: task
priority: normal
created_at: 2026-11-24T17:35:22Z
updated_at: 2026-11-24T17:37:33Z
parent: org-diagram-iypc
---

- [x] Tenant-check group on add_membership (404 for foreign/unknown group)
- [x] Reads in transactions; end before BCC HTTP calls (api + report)
- [x] Guard conftest against non-test databases
- [x] Simplify SQL building (unnest sort-order, plain INSERT)
- [x] Validate input in admin routes (400 instead of 500)
- [x] Typing: TenantId/Row aliases, typed conn
- [x] Shared test factories
- [x] Tenant-filter member_count in person groups

## Summary of Changes
- add_membership is now INSERT ... SELECT from the caller's group; it returns 404 for another tenant's group or an unknown one, and tenant_id comes from the group.
- All reads run inside db.transaction() and finish before BCC API calls; the report now takes pre-fetched rows.
- conftest refuses non-local or non-*test* databases.
- Sort-order uses unnest(); the plain INSERT replaces psycopg2.sql composition.
- Admin routes validate ints, uids, ranges and duplicates, and return 400 for bad input.
- Added Connection/Row/TenantId aliases and fetch_one/fetch_all helpers.
- Added a shared tests/factories.py.
- person-groups member_count is now tenant-scoped.
- 64 tests pass.
