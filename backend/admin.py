from flask import Blueprint, current_app, request, session


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
        except (ValueError, TypeError):
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

    membership_data = {
        "group_id": group_id,
        "bcc_person_uid": person_uid,
    }

    if title is not None:
        membership_data["title"] = title

    tenant_id = current_app.config["TENANT_ID"]
    if tenant_id:
        membership_data["tenant_id"] = tenant_id

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

    group_id = data.get("group_id")
    person_uid = data.get("person_uid")

    if not group_id or not person_uid:
        return {"error": "Both group_id and person_uid are required"}, 400

    if current_app.config["DEMO_MODE"]:
        # Convert IDs to appropriate types for demo mode
        try:
            group_id = int(group_id)
            person_uid = int(person_uid)
        except (ValueError, TypeError):
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
    tenant_id = current_app.config["TENANT_ID"]

    q = (
        supabase.table("group_membership")
        .delete()
        .eq("group_id", group_id)
        .eq("bcc_person_uid", person_uid)
    )

    if tenant_id:
        q = q.eq("tenant_id", tenant_id)
    else:
        q = q.is_("tenant_id", None)

    result = q.execute()

    if not result.data:
        return {"error": "Member not found in group"}, 404

    return {
        "success": True,
        "data": result.data[0] if result.data else None,
    }, 200


@admin_bp.route("/group-membership", methods=["PUT"])
def update_group_member():
    """Update a group member's properties (currently only title)"""
    if current_app.config["DEMO_MODE"]:
        return {"success": True}, 304  # Not modified

    data = request.get_json()
    if not data:
        return {"error": "No JSON data provided"}, 400

    group_id = data.get("group_id")
    person_uid = data.get("person_uid")
    title = data.get("title")

    if not group_id or not person_uid:
        return {"error": "Both group_id and person_uid are required"}, 400

    if title is None:
        return {"success": True}, 304  # Not modified

    supabase = current_app.config["SUPABASE"]
    tenant_id = current_app.config["TENANT_ID"]

    q = (
        supabase.table("group_membership")
        .update({"title": title})
        .eq("group_id", group_id)
        .eq("bcc_person_uid", person_uid)
    )

    if tenant_id:
        q = q.eq("tenant_id", tenant_id)
    else:
        q = q.is_("tenant_id", None)

    result = q.execute()
    if not result.data:
        return {"error": "Member not found in group"}, 404

    return {
        "success": True,
        "data": result.data[0] if result.data else None,
    }, 200
