# Domain Model

This document describes the org-diagram domain in Domain-Driven Design (DDD) terms: the ubiquitous language, bounded contexts, entities, and aggregates that the code implements.

## Ubiquitous Language

| Term | Meaning |
|---|---|
| **Group** | A node in an organization's hierarchy (e.g. "Bestyrelse", a 5-person board). Has a name, an optional parent group, and a set of members. |
| **Staff group** (`staber`) | A `Group` flagged `type = "staff-group"`. Rendered as a badge/counter on its parent node rather than as its own box in the tree — visually subordinate to regular groups. |
| **Group Membership** | The relationship between a `Person` and a `Group`: the fact that a person belongs to a group, optionally carrying a `title`. This is the aggregate's real unit of change — people and groups rarely change, memberships change constantly. |
| **Person** | A human being, identified by `person_uid`. Org-diagram does not own person identity or profile data — it is a system of record for *membership*, not for *people*. Name/photo are fetched live from the BCC Core API (or demo fixtures). |
| **Title** | A free-text role label on a membership (e.g. "Leder", "Ansvarlig"), not a controlled vocabulary — except that "Leder" and "Ansvarlig" are treated specially for sort order (leadership-first). |
| **Tenant** | An organizational boundary (a "church", identified by `churchId` from the OIDC claim / `tenant_id` column). All groups and memberships are partitioned by tenant. A `null` tenant is the legacy/single-tenant dataset. |
| **Org Tree** | The materialized hierarchy of groups for a tenant. |
| **User** (session) | The authenticated operator (staff member logging in via BCC OIDC), distinct from `Person`/member records. Carries `churchId`, which scopes every query to a tenant. |

## Bounded Contexts

The system spans two bounded contexts that only meet at the `person_uid` reference:

### 1. Org Structure (this application, owns the model)

Owns `Group`, `Group Membership`, and `Org Tree`. This is the core domain — everything under `backend/{api,admin,reports}.py` and the `groups` / `group_membership` Supabase tables.

### 2. Members (Identity & People - BCC Core API, upstream, external)

Owns `Person` (name, display name, profile picture) and `User` authentication (OIDC via `login.bcc.no`). Org-diagram treats this as a **separate bounded context** consumed through an **Anti-Corruption Layer**: `bcc_api/` (generated Swagger client) plus the thin mapping in `api.py`/`reports.py` (`map_person`, `persons_by_uid.get(uid)` with a `"?"` fallback when a person can't be resolved). Org Structure never stores a person's name — only their `bcc_person_uid`, a foreign identity reference.

## Entities

### `Group`

- **Identity**: `id` (`group_id`).
- **Attributes**: `name`, `parent_id` (nullable — root groups have no parent), `tenant_id`, and an optional `type` (`"staff-group"` vs. regular).
- **Behavior**: structural edits (create/rename/reparent) happen via the import tooling, not the runtime API — there's no `PUT /api/groups` today. `member_count` is not stored on the entity; it's derived by counting associated `Group Membership` rows.
- Groups form the hierarchy by referencing their parent's `id`. There's no cycle-prevention or depth constraint in code — the hierarchy is trusted input.

### `Person`

- **Identity**: `person_uid`, minted and owned entirely by the Identity & People context.
- Within Org Structure, `Person` has no local attributes at all — it exists purely as a reference (`bcc_person_uid` on a `Group Membership` row). Name and profile picture are resolved on read by calling the BCC Core API (or demo fixtures) and are never persisted locally.
- This makes `Person` an entity whose lifecycle Org Structure does not control: it can reference a `person_uid` that the upstream context has renamed, deactivated, or doesn't recognize — handled by falling back to `"?"` as a display name (`map_person` in `api.py`, `MembershipReport.generate_report` in `reports.py`).

### `Org Tree`

- **Identity**: scoped by `tenant_id` — one Org Tree per tenant (including the `null`/legacy tenant).
- Not a stored entity with its own identity column; it's assembled on every read (`GET /api/tree`) by loading all `Group` rows for a tenant and following `parent_id` links, with each group annotated by its live `member_count`.
- Because it's recomputed from `Group` rows on each request, there's nothing to keep in sync — editing a `Group` or a `Group Membership` is immediately reflected the next time the Org Tree is read.

## Aggregates

### `Group` aggregate

- **Aggregate root**: `Group` (`groups` table: `id`, `name`, `parent_id`, `tenant_id`).
- **Invariant boundary**: a group's identity, name, and place in the hierarchy (`parent_id`).
- Membership is deliberately **not** inside this aggregate — `Group Membership` rows are their own aggregate. This keeps adding/removing a member a single-row transaction instead of requiring the whole group (and its member list) to be loaded and re-saved.

### `Group Membership` aggregate

- **Aggregate root**: `Group Membership` itself (`group_membership` table: `group_id`, `bcc_person_uid`, `title`, `tenant_id`).
- **Identity**: the composite `(group_id, bcc_person_uid, tenant_id)` — see `admin.py`'s `remove_group_member`/`update_group_member`, which always key on that triple.
- **Invariants enforced at this boundary**:
  - A membership belongs to exactly one tenant, and it must match the group's tenant (`add_group_member` copies `tenant_id` from the session onto the new row).
  - `title` is a membership-scoped attribute, not a person attribute — editing it (`PUT /api/group-membership`) never touches identity data.
- `Person` is referenced by `bcc_person_uid` only — never loaded/validated as a local entity.
