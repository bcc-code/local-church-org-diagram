import logging

from flask import Blueprint, current_app, request, session
from roles import is_global_admin

logger = logging.getLogger("admin")


def authorize():
    user = session.get("user")
    if not user:
        return {"error": "Authentication required"}, 401


admin_bp = Blueprint("admin", __name__, url_prefix="/api")
admin_bp.before_request(authorize)


@admin_bp.route("/group-membership", methods=["POST"])
def add_group_member():
    """Add a member to a group"""
    data = request.get_json()
    if not data:
        return {"error": "No JSON data provided"}, 400

    if not is_global_admin(session["user"]["email"]):
        return {"error": "global_admin role required"}, 403

    group_id = data.get("group_id")
    person_uid = data.get("person_uid")
    title = data.get("title")
    if not group_id or not person_uid:
        return {"error": "Both group_id and person_uid are required"}, 400

    if current_app.config["DEMO_MODE"]:
        # Convert IDs to appropriate types for demo mode
        try:
            group_id = int(group_id)
            person_uid = int(person_uid)
        except ValueError, TypeError:
            return {"error": "Invalid group_id or person_uid"}, 400

        # Update in-memory demo data
        memberships = current_app.config["DEMO_MEMBERSHIPS"]
        if group_id not in memberships:
            memberships[group_id] = []

        # Check if already a member
        if person_uid not in memberships[group_id]:
            memberships[group_id].append(person_uid)

            # Update member count in tree
            tree = current_app.config["DEMO_TREE"]
            for group in tree:
                if group["group_id"] == group_id:
                    group["member_count"] = len(memberships[group_id])
                    break

        return {"success": True}, 201

    tenant_id = session["user"].get("churchId")
    membership_data = {
        "group_id": group_id,
        "bcc_person_uid": person_uid,
        "tenant_id": tenant_id,
    }

    if title is not None:
        membership_data["title"] = title

    supabase = current_app.config["SUPABASE"]
    result = supabase.table("group_membership").insert(membership_data).execute()

    return {
        "success": True,
        "data": result.data[0] if result.data else None,
    }, 201


@admin_bp.route("/group-membership", methods=["DELETE"])
def remove_group_member():
    """Remove a member from a group"""
    data = request.get_json()
    if not data:
        return {"error": "No JSON data provided"}, 400

    if not is_global_admin(session["user"]["email"]):
        return {"error": "global_admin role required"}, 403

    group_id = data.get("group_id")
    person_uid = data.get("person_uid")

    if not group_id or not person_uid:
        return {"error": "Both group_id and person_uid are required"}, 400

    if current_app.config["DEMO_MODE"]:
        # Convert IDs to appropriate types for demo mode
        try:
            group_id = int(group_id)
            person_uid = int(person_uid)
        except ValueError, TypeError:
            return {"error": "Invalid group_id or person_uid"}, 400

        # Update in-memory demo data
        memberships = current_app.config["DEMO_MEMBERSHIPS"]

        if group_id in memberships and person_uid in memberships[group_id]:
            memberships[group_id].remove(person_uid)

            # Update member count in tree
            tree = current_app.config["DEMO_TREE"]
            for group in tree:
                if group["group_id"] == group_id:
                    group["member_count"] = len(memberships[group_id])
                    break

            return {"success": True}, 200
        else:
            return {"error": "Member not found in group"}, 404

    supabase = current_app.config["SUPABASE"]
    tenant_id = session["user"].get("churchId")

    result = (
        supabase.table("group_membership")
        .delete()
        .eq("group_id", group_id)
        .eq("bcc_person_uid", person_uid)
        .eq("tenant_id", tenant_id)
        .execute()
    )
    if not result.data:
        return {"error": "Member not found in group"}, 404

    return {
        "success": True,
        "data": result.data[0] if result.data else None,
    }, 200


@admin_bp.route("/group-membership", methods=["PUT"])
def update_group_member():
    """Update a group member's properties (title and/or link)"""
    data = request.get_json()
    if not data:
        return {"error": "No JSON data provided"}, 400

    if not is_global_admin(session["user"]["email"]):
        return {"error": "global_admin role required"}, 403

    group_id = data.get("group_id")
    person_uid = data.get("person_uid")
    title = data.get("title")
    link = data.get("link")

    if not group_id or not person_uid:
        return {"error": "Both group_id and person_uid are required"}, 400

    if current_app.config["DEMO_MODE"]:
        # Update title and/or link in DEMO_MEMBERS
        members = current_app.config["DEMO_MEMBERS"]
        for member in members:
            if str(member["person_uid"]) == str(person_uid):
                if title is not None:
                    member["title"] = title
                if link is not None:
                    member["link"] = link
                return {"success": True}, 200

        return {"error": "Member not found"}, 404

    # Build update data with only provided fields
    update_data = {}
    if title is not None:
        update_data["title"] = title
    if link is not None:
        update_data["link"] = link

    if not update_data:
        return {"success": True}, 304  # Not modified

    supabase = current_app.config["SUPABASE"]
    tenant_id = session["user"].get("churchId")

    result = (
        supabase.table("group_membership")
        .update(update_data)
        .eq("group_id", group_id)
        .eq("bcc_person_uid", person_uid)
        .eq("tenant_id", tenant_id)
    ).execute()
    if not result.data:
        return {"error": "Member not found in group"}, 404

    return {
        "success": True,
        "data": result.data[0] if result.data else None,
    }, 200


@admin_bp.route("/groups/sort-order", methods=["PUT"])
def update_group_sort_order():
    """Update sort_order for a set of sibling groups (drag-to-reorder)."""
    data = request.get_json()
    if not data or not isinstance(data.get("updates"), list) or not data["updates"]:
        return {"error": "No updates provided"}, 400

    updates = data["updates"]
    for update in updates:
        if "group_id" not in update or "sort_order" not in update:
            return {"error": "Each update requires group_id and sort_order"}, 400

    if current_app.config["DEMO_MODE"]:
        tree = current_app.config["DEMO_TREE"]
        by_id = {group["group_id"]: group for group in tree}
        for update in updates:
            group = by_id.get(update["group_id"])
            if group:
                group["sort_order"] = update["sort_order"]
        return {"success": True}, 200

    supabase = current_app.config["SUPABASE"]
    tenant_id = session["user"].get("churchId")

    for update in updates:
        q = supabase.table("groups").update(
            {"sort_order": update["sort_order"]}
        ).eq("id", update["group_id"])
        if tenant_id:
            q = q.eq("tenant_id", tenant_id)
        q.execute()

    return {"success": True}, 200
