# Event Model

This document maps the business events in the Org Diagram system — what actually happens, in business terms. It complements [`domain-model.md`](./domain-model.md), which defines the ubiquitous language and aggregates these events operate on.

## Actors

- **User** — Any authenticated BCC user. Views the org chart and group rosters read-only.
- **Admin** — Manages group membership within a tenant: adds/removes members, assigns titles and links. A per-tenant role, distinct from User.
- **Technical Admin** — Has direct database access. Performs periodic org-structure reorganizations and generates membership reports on an Admin's behalf. Operates outside the running application (database console, import script, CLI) rather than through the web UI.
- **BCC Core API** (upstream system) — Owns Person identity. Queried for names/photos; never initiates events in this system.

## Business Events

Named in past tense — things that have happened. Grouped by process.

### Authentication

| Event | Triggered by |
|---|---|
| **UserLoggedIn** | A User completes the BCC OIDC login flow. |
| **UserLoggedOut** | A User ends their session. |

### Membership Management

The core, recurring workflow — an Admin curating who belongs where:

| Event | Triggered by |
|---|---|
| **MemberAssignedToGroup** | An Admin adds a person to a group. |
| **MemberUnassignedFromGroup** | An Admin removes a person from a group. |
| **MemberAssignedTitleInGroup** | An Admin sets or changes a member's title within a group (e.g. "Leder", "Ansvarlig"). |
| **MemberAssignedMetadataInGroup** | An Admin attaches supplementary metadata to a membership — today, a free-text link/note; the same event covers whatever fields get added later. |

A membership is unique per (group, person, tenant) — a title change mutates the existing membership rather than creating a new one. The business does not distinguish *why* a membership changed (a volunteer joining, a leadership appointment, a term ending, a data correction) — one event per action type covers all cases; there's no separate `LeaderAppointed`-style event.

### Group Structure

| Event | Triggered by |
|---|---|
| **OrgStructureReorganized** | A Technical Admin performs a periodic bulk reorganization of the group hierarchy (rebuilding groups and their memberships from source data), working directly against the database. |

Group structure changes only through this periodic, out-of-band reorganization — there's no in-app, incremental way to create, rename, or move a single group. `domain-model.md`'s statement that Org Diagram "owns" group structure means ownership of the data, not an in-app mutation capability.

### Reporting

| Event | Triggered by |
|---|---|
| **MembershipReportGenerated** | A Technical Admin generates a membership export (CSV) on an Admin's behalf. |

The Admin is the actual consumer, using the export for their own downstream purposes. Today this requires a Technical Admin to run it manually; a self-service export built into the API/website — making this an Admin-initiated event directly — is the natural next step, not built yet.

### Tenant Boundary

Tenant identity comes from the logged-in user's church affiliation. Onboarding a new tenant is, in practice, just the first `OrgStructureReorganized` for that church; there's no separate provisioning event, and granting someone the Admin role for a new tenant happens informally, outside the system.

## Process Narrative

```
[Technical Admin]                     [Admin]                              [User]
      │                                   │                                    │
      ▼                                   │                                    │
OrgStructureReorganized                   │                                    │
(periodic bulk reorg,                     │                                    │
 direct DB, outside the app)              │                                    │
      │                                   ▼                                    │
      │                             UserLoggedIn                        UserLoggedIn
      │                                   │                                    │
      │                                   ▼                                    ▼
      │                          MemberAssignedToGroup              (views tree & rosters —
      │                          MemberUnassignedFromGroup            read-only, no new events)
      │                          MemberAssignedTitleInGroup      ──▶ tree/roster reads reflect
      │                          MemberAssignedMetadataInGroup        changes immediately
      │
      ├── MembershipReportGenerated (on Admin's behalf) ──▶ export handed to Admin
      │
      ▼
  (planned: Admin self-service export, no Technical Admin needed)
```

