---
# org-diagram-hwu7
title: 'Docs/env: DATABASE_URL, remove historical plans'
status: completed
type: task
priority: normal
created_at: 2026-11-24T17:23:50Z
updated_at: 2026-11-24T17:25:30Z
parent: org-diagram-iypc
---

- [x] CLAUDE.md, README, .env.example, example.env, domain-model.md
- [x] delete plans/*.md

## Summary of Changes
Docs and env examples now use DATABASE_URL (+ optional DB_POOL_MAX), describe Postgres hosted on Supabase, and point to db.py/queries.py/mutations.py/schema.sql. The tools/.env section still uses SUPABASE_* because the notebook was not migrated. Historical plans/ were deleted.
