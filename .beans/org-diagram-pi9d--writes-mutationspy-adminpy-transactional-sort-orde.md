---
# org-diagram-pi9d
title: 'Writes: mutations.py, admin.py, transactional sort-order'
status: completed
type: task
priority: normal
created_at: 2026-11-24T17:23:50Z
updated_at: 2026-11-24T17:27:42Z
parent: org-diagram-iypc
---

- [x] mutations.py insert/delete/update/sort-order
- [x] admin.py uses mutations (409 duplicate, atomic sort-order)
- [x] tests/test_mutations.py

## Summary of Changes
Added mutations.py. Admin writes now run inside db.transaction(). Sort-order is a single UPDATE ... FROM (VALUES) and raises GroupsNotFound so the whole transaction rolls back. A duplicate add returns 409. With no tenant, delete/update/sort-order are scoped to tenant_id IS NULL. Added 22 tests, including route-level ones.
