# Domain Model

## Ubiquitous Language

- **Group** — A node in the org hierarchy (e.g. a team, department, or committee). Has a `name` and an optional `parent_id`. Groups form a tree per `tenant_id`.
- **Person** — An individual identified by `bcc_person_uid` (aka `person_uid`), a UID from the BCC Core API. The org-diagram DB never stores person names/photos — those are fetched live from BCC.
- **Membership** (`group_membership`) — The link between a Person and a Group. Carries membership-specific attributes: `title` (e.g. "Leder", "Ansvarlig") and `link` (free-text, e.g. a URL or note). A person can have multiple memberships across groups.
- **Title** — A role label on a Membership, not on a Person or Group. "Leder" and "Ansvarlig" are leadership titles and sort first in member lists.
- **Tenant** (`tenant_id`) — Multi-tenant partition. `NULL` tenant_id means single-tenant/default mode. Derived from the logged-in user's `churchId`.
- **Org Chart / Tree** — The rendered hierarchy of Groups (via `parent_id`), shown with d3-org-chart. The `/api/tree` payload is groups only — members are fetched per-group on demand via `/api/persons`.
- **Demo Mode** — Runtime mode (`DEMO_MODE=1`) where `/api/*` responses come from static JSON/in-memory state instead of Supabase/BCC, for dev without credentials.

## Bounded Contexts

### Org Diagram (this app)
Owns the **Group** hierarchy and **Membership** records. Source of truth: Supabase (`groups`, `group_membership` tables). Responsible for:
- Org structure (create/move/query groups)
- Who belongs to which group, and their title/link within it
- Rendering the tree (frontend) and reporting (`backend/reports/`)

Org Diagram does **not** own Person identity — it only stores a foreign UID (`bcc_person_uid`) as a reference.

### BCC Members (upstream context, via BCC Core API)
Owns **Person** identity: display name, profile picture, and other person attributes. Accessed via `bcc_api/` (generated Swagger client), OAuth2 client-credentials flow. Org Diagram treats this context as read-only and calls it on demand (batch lookup by UIDs in `/api/persons`, or search in `/api/persons/search`).

**Integration point**: `bcc_person_uid` is the shared identifier crossing the boundary. Org Diagram joins its local Membership data with BCC's Person data at request time — no person data is persisted locally, so a person with no matching BCC result renders as `name: "?"`.

## Aggregates

### Person
- **Identity**: `bcc_person_uid` (external, owned by BCC)
- **Attributes** (from BCC, not persisted): `display_name`, `profile_picture`
- Org Diagram holds no invariants over Person — it's a value fetched from BCC, keyed by UID.

### Group
- **Identity**: `id`
- **Attributes**: `name`, `parent_id` (nullable — null/root groups have no parent), `tenant_id`
- **Invariant**: hierarchy is a tree — `parent_id` must reference another group (or be null for a root), scoped within the same `tenant_id`.

### Membership
- **Identity**: composite — (`group_id`, `bcc_person_uid`, `tenant_id`)
- **Attributes**: `title` (optional), `link` (optional)
- **Invariant**: links exactly one Person (by UID) to exactly one Group, within one tenant. A person may hold multiple Memberships (different groups, or same group with different title over time — current schema allows only one row per group+person+tenant).
- Membership is the aggregate root for admin mutations (`POST`/`PUT`/`DELETE /api/group-membership`) — Group and Person are referenced, not modified, through it.
