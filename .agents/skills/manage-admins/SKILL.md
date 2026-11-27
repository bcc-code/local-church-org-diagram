---
name: manage-admins
description: Add, list or remove org-diagram admins (Global Admins and Local Admins of a tenant/BCC church) and look persons up by name, with backend_new's `admins` CLI command, against the local database or production. Use when the user asks who is an admin, or to make someone an admin or remove one.
---

# Manage admins

`backend_new`'s `admins` command manages the Admins table. Full reference: the "Admins" section of
`backend_new/README.md`.

- **Global Admin**: administers every tenant (`--global`).
- **Local Admin**: administers one tenant, a BCC church ID (`--tenant <id>`), whatever the person's own church.
- A person can have both. Rows are keyed by the person's BCC person UID.

## Running it

From `backend_new/`, always with `--json`. Parse stdout as JSON. Ignore stderr except for the `Warning (...)` and
`Error (...)` lines, which repeat what the JSON says.

```bash
dotnet run --project backend --no-launch-profile -- admins <search|list|add|remove> ... --json
```

- **Local** (dev database): `ConnectionStrings:Default` comes from user secrets or `aspire run`. Core API credentials
  come from user secrets, which `search` and `add` need. Drop `--no-launch-profile` if the launch profile provides the
  settings.
- **Production**: only when the user explicitly asks for production. Ask them to export the values in the session (`!
  export ...`) rather than pasting secrets into the chat:
  ```bash
  export ASPNETCORE_ENVIRONMENT=Production
  export ConnectionStrings__Default="Host=aws-0-<region>.pooler.supabase.com;Port=5432;...;SSL Mode=Require"  # session pooler
  export CoreApi__ClientId=... CoreApi__ClientSecret=...
  ```
  Say which environment you're about to change before every write.

Exit code 0 means success, including no-ops (`unchanged`, `not_found`). Exit code 1 means `"status": "error"`: read
`error.code` and `error.message`.

## Workflow

1. **Resolve the person.** Run `admins search "<name>" --json`.
   - Exactly one match: use its `uid`.
   - Several matches: show them (name, UID, current admin rows) and ask the user which person they mean. Never pick one
     yourself: same-name persons look identical here.
   - No match: tell the user. Don't guess a UID.
2. **Get the tenant.** Ask the user for the church ID if they gave a church name. Don't invent one.
3. **Preview.** Run `admins add <uid> --tenant <id> --dry-run --json` (or `--global`).
   - A `tenant_has_no_groups` warning usually means a wrong church ID: check with the user before going on.
4. **Apply.** Run the same command without `--dry-run`.
   - `unchanged` means the person already had that row. Their stored name is kept.
5. **Report.** Show what changed, from the JSON (`status`, `admins`).

### Removing

1. Run `admins list <uid> --json` and show the rows to the user.
2. Run `admins remove <uid> --tenant <id>|--global|--all --json` without `--yes`. It removes nothing and returns
   `confirmation_required` with the rows it would remove. Show those rows.
3. Only after the user confirms that specific removal, rerun the command with `--yes`.

Never pass `--allow-last-global` unless the user explicitly asks to remove the last Global Admin. Removing a Global
Admin is also more serious than removing a Local Admin: say so when you ask the user to confirm it.

### Listing

- `admins list --json`: every admin row.
- `admins list --tenant <id> --global --json`: everyone who can administer that tenant.
- `admins list <uid> --json`: one person's rows.

## Errors

| `error.code`              | What to do                                                                                   |
| ------------------------- | -------------------------------------------------------------------------------------------- |
| `invalid_input`           | Fix the command; the message says what's wrong                                              |
| `person_not_found`        | The UID isn't in the Core API: search again                                                  |
| `core_api_not_configured` | Set the CoreApi credentials, or for `add` pass `--name "<name>"` if the user gives the name  |
| `core_api_error`          | The Core API call failed: retry later or use `--name`                                        |
| `migrations_pending`      | The database lacks the Admins migration: the app must be deployed/started first. Tell the user; don't migrate it yourself |
| `last_global_admin`       | Tell the user; another Global Admin must be added first                                      |
| `confirmation_required`   | Expected without `--yes`: confirm with the user                                              |
