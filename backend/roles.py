from flask import current_app


def get_user_roles(email: str) -> list[dict]:
    """Return this user's role assignments (demo mode only for now)."""
    if current_app.config["DEMO_MODE"]:
        assignments = current_app.config["DEMO_ROLE_ASSIGNMENTS"]
        return [a for a in assignments if a["email"].lower() == email.lower()]

    # TODO: query the `admin_roles` table in Supabase once it exists
    return []


def is_global_admin(email: str) -> bool:
    return any(a["role"] == "global_admin" for a in get_user_roles(email))
