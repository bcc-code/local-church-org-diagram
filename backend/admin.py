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
    if current_app.config["DEMO_MODE"]:
        return {"success": True}, 200

    data = request.get_json()
    if not data:
        return {"error": "No JSON data provided"}, 400

    group_id = data.get("group_id")
    person_uid = data.get("person_uid")
    title = data.get("title")

    if not group_id or not person_uid:
        return {"error": "Both group_id and person_uid are required"}, 400

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
    if current_app.config["DEMO_MODE"]:
        return {"success": True}, 200

    data = request.get_json()
    if not data:
        return {"error": "No JSON data provided"}, 400

    group_id = data.get("group_id")
    person_uid = data.get("person_uid")

    if not group_id or not person_uid:
        return {"error": "Both group_id and person_uid are required"}, 400

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
