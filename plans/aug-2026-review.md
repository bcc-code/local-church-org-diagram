# Code Review — org-diagram (2026-08-11)

Scope: full repo (`backend/`, `frontend/`, `tools/`, `bcc_api/`, deployment/config files). ~900 LOC backend, ~2600 LOC frontend, plus a fully vendored generated API client.

## Highest priority

### 1. Admin write endpoints have no server-side authorization check
`backend/admin.py` (`add_group_member`, `remove_group_member`, `update_group_member`) and `backend/api.py` are both gated only by `authorize()`, which checks *"is there a logged-in session"* — not *"is this user allowed to edit org data"*. The `/admin` UI (`frontend/src/views/AdminView.vue`) and the `adminMode` prop threaded through `OrgChart.vue` → `OrgNode.vue` → `GroupMembersDialog.vue` → `MemberCard.vue` are purely cosmetic: they control which buttons render, nothing more. Any authenticated member of a tenant can call `POST/PUT/DELETE /api/group-membership` directly (curl, devtools) and mutate that tenant's org structure — no role/permission claim from the OIDC token is ever checked.
- There is no `role`, `is_admin`, or `permission` concept anywhere in the codebase (verified by grep across `backend/` and `frontend/src/`).
- `app.py`'s `/<int:tenant_id>/admin` route only checks the tenant ID matches the user's `churchId`, not that the user has an admin/leader role within that tenant.
- Fix: check a role/claim from the BCC OIDC `userinfo` (or a separate authorization table) inside `admin_bp.before_request`, not just in the frontend.

### 2. No automated tests, no CI
No test files exist for `backend/` or `frontend/src/` (only the auto-generated `bcc_api/test/*` scaffolding, which just tests the generated models). There's no GitHub Actions workflow. Given `admin.py`/`api.py` contain non-trivial branching logic (demo vs. live mode duplicated in nearly every handler, tenant scoping, sort/score algorithms), regressions here are currently only caught by manual testing.

### 3. Excel-notebook is the only data-onboarding path, and it's not reproducible
`tools/import_from_excel.ipynb` is the sole way new tenants get seeded into Supabase. It:
- Hardcodes a local file path (`~/Documents/Arkitektur Alle i Tjeneste 2025.xlsx`) and a hardcoded `tenant_id: 51`.
- Uses `rapidfuzz` for name-matching against the BCC API, which is not declared in `requirements.txt` or the new `Pipfile` — works only because it happens to be installed locally.
- Is a manually-run, cell-by-cell, non-idempotent script against production Supabase — no dry-run mode, no re-run safety (re-running would likely duplicate `group_membership` rows or `insert` conflicts on `groups.id`).
- This is fine as a one-off migration tool, but as the *only* onboarding mechanism for new BCC tenants it's an operational bus-factor risk. Worth turning the core logic into a `backend/cli.py` command (there's already a CLI pattern established via `generate_report`) so onboarding is scriptable and repeatable.

## Architecture

### Frontend/D3 integration is fragile
`OrgChart.vue` mounts full Vue app instances into DOM nodes created by d3-org-chart (`createApp(OrgNode, ...).mount(host)` per node), and monkey-patches `chart.restyleForeignObjectElements` to re-run the mount step after every d3-driven re-render:
```js
const origRestyle = chart.restyleForeignObjectElements.bind(chart);
chart.restyleForeignObjectElements = (...args) => { ... mountNodes(); ... };
```
This works today but is coupled to an undocumented internal method name of a CDN-loaded library (see below) — any minor version bump of d3-org-chart could silently break node rendering with no compile-time signal, since `d3` is typed `any` throughout. There's also manual DOM querying (`nodeElement.querySelector('.text-caption')`) to patch text in-place as an optimization (`handleMemberCountChanged`) alongside Vue's own reactivity — two rendering strategies coexisting for the same data.

Recommend: if d3-org-chart's chart-update hooks are too limited, consider isolating the d3 integration behind a small wrapper module with an explicit, tested contract, rather than patching library internals inline in the component.

### d3-org-chart loaded via CDN, not as an npm dependency
Per `CLAUDE.md` this is intentional, but it means: no version pinning in `package.json`/lockfile, no type definitions, a runtime polling loop (`waitForD3`) to work around load-order, and a third dependency-tracking mechanism (CDN `<script>` tag in `index.html`) alongside npm and pip. Worth at least pinning the CDN URL to an exact version (check `index.html`) and adding a local `@types` shim instead of `any`.

### Demo-mode branching duplicated in nearly every handler
Every route in `api.py` and `admin.py` has an `if current_app.config["DEMO_MODE"]: ... else: ...` branch reimplementing the same operation against two different backends (in-memory dict vs. Supabase). This more than doubles the size of each handler and is a maintenance tax — any new field or business rule (e.g. the title-sorting logic, tenant scoping) has to be added twice and kept in sync by hand. A repository/adapter interface (`OrgDataStore` with a `DemoStore` and `SupabaseStore` implementation) would let the route handlers be written once.

### Global mutable module state in `OrgChart.vue`
`let chart`, `let allNodes`, `let selectedPerson`, `let personSearchTimeout`, `let skipNextRender` are all plain module-level `let`s living alongside Vue `ref`s in the same component. `skipNextRender` in particular is a hand-rolled flag to suppress a `watch` callback after a same-component write — a sign the data flow (state.data mutated locally *and* driven by a fetch-triggered watcher) has become hard to reason about. `selectedPerson` is even set but never read anywhere.

## Maintainability

### Two package managers for the frontend
Both `package-lock.json` and `pnpm-lock.yaml` are committed. Whichever one CI/new-clone `npm install`/`pnpm install` picks will silently diverge from the other over time. Pick one (the org doesn't mandate either) and delete the other lockfile.

### Docs vs. code drift
- `CLAUDE.md` says "Tailwind CSS 4"; `frontend/package.json` has `"tailwindcss": "^3.4.18"`. Either the upgrade never landed or the docs are stale.
- Two environment-variable templates exist: root `example.env` (missing `BCC_OIDC_CLIENT_ID/SECRET`, `FLASK_SECRET_KEY`) and `backend/.env.example` (complete). New devs following the root one will hit confusing runtime `KeyError`s on OIDC vars. Delete the root copy or make it point at the backend one.
- Two Python dependency manifests present now: `requirements.txt` (pip, tracked) and an **untracked** `Pipfile` (pipenv). If the intent is migrating to pipenv, `requirements.txt` should be removed once that's done; if not, delete the `Pipfile` — having both invites them to drift out of sync (pip install and `pipenv install` will happily produce different environments).
- Uncommitted `pyrightconfig.json` (`.venv` based) suggests local tooling setup that isn't shared with the team yet — fine if intentional, but worth committing if the team has standardized on Pyright.

### Vendored, generated API client checked into the monorepo
`bcc_api/` is a full swagger-codegen output (~180 files, its own `tox.ini`/`.travis.yml`/`test-requirements.txt`/`git_push.sh` for auto-publishing to a separate repo) committed directly into this project and installed via `pip install -e ./bcc_api`. This is a lot of generated, non-hand-maintained surface area living in a project repo that's not really about the BCC API client. It also carries genuine tech-drift risk: the old `swagger-codegen` (not `openapi-generator`) style client uses an old urllib3-based `rest.py`, and nothing here pins the source OpenAPI spec version it was generated from — if the BCC Core API changes, there's no documented regeneration process. Consider moving this to its own repo/package version and depending on it normally (the `.travis.yml`/`git_push.sh` inside it suggest it *was* originally a standalone repo that got vendored in).

### `.github/copilot-instructions.md` is boilerplate scaffolding, never filled in
It's the default VS Code "Copilot workspace setup" template with checklist items still unchecked and generic instructions ("Ask for project type, language and frameworks if not specified") — none of it is specific to this project, and it duplicates/could conflict with the real `CLAUDE.md`. Either fill it in with project-specific guidance or delete it so future Copilot Chat sessions in this repo aren't following a stale generic template.

### `backend/cli.py` diverges from its own plan doc
`plans/report-generation.md` documents `membership_report()` taking no arguments and `MembershipReport(supabase)` with a 2-column CSV. The actual implementation (`backend/cli.py`, `backend/reports.py`) takes a `--root` option, threads through `persons_api`/`bcc_auth` for name lookups, and emits 4 columns including `team_no`. The plan doc is now misleading if anyone reads it as current documentation — worth updating or removing it now that the feature has shipped and evolved past the plan.

## Smaller findings

- `backend/app.py:96` — docstring typo: `"""Serves the Vue rontend i"""`.
- `_score_and_rank_persons` (`api.py`) re-reads `demo_requests/members.json` from disk on every search request in demo mode (`api.py:142`) instead of using `current_app.config["DEMO_MEMBERS"]`, which is already loaded once at startup for this exact purpose elsewhere in the file.
- `health()` in `app.py` reaches into `app.config["BCC_AUTH"]`/`PERSONS_API"]` unconditionally — this route will `KeyError` in demo mode, since those config keys are only set in the `else` (production) branch of the mode switch. If demo deployments ever get a health-checked load balancer in front of them, this 500s.
- `update_group_member` (`admin.py`) returns HTTP 304 with a JSON body (`{"success": True}, 304`) when no fields were provided — 304 is defined by HTTP to have no body; most HTTP clients/proxies will strip it, making the body silently unreachable. A 200 or 400 would be more conventional here.
- `backend/membership.csv` (untracked, in the repo root of `backend/`) looks like a generated report artifact (matches the exact columns `cli.py` emits) left over from a manual run — worth confirming it isn't meant to be committed, and adding it to `.gitignore` if this is a repeatable byproduct of running the report command locally.
