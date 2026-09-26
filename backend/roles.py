from flask import current_app
from postgrest.exceptions import APIError

# IDs from the `role` table (see demo_requests/roles.json for the demo-mode catalog)
GLOBAL_ADMIN_ROLE_ID = 1


def get_user_roles(email: str) -> list[dict]:
    """Return this user's role assignments.

    `role` is the role's id (FK to `role.id`).
    """
    if current_app.config["DEMO_MODE"]:
        assignments = current_app.config["DEMO_ROLE_ASSIGNMENTS"]
        return [a for a in assignments if a["email"].lower() == email.lower()]

    supabase = current_app.config["SUPABASE"]
    try:
        result = (
            supabase.table("role_assignments")
            .select("role_id, tenant_id, group_id")
            .ilike("email", email)
            .execute()
        )
    except APIError:
        current_app.logger.exception(f"Error fetching roles for {email}")
        return []

    return [
        {
            "role": item["role_id"],
            "tenant_id": item["tenant_id"],
            "group_id": item["group_id"],
        }
        for item in result.data
    ]


def is_global_admin(email: str) -> bool:
    return any(a["role"] == GLOBAL_ADMIN_ROLE_ID for a in get_user_roles(email))
