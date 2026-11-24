import logging
from typing import Any

import db
import mutations
from flask import Blueprint, current_app, request, session
from psycopg2.errors import UniqueViolation

logger = logging.getLogger("admin")

_BIGINT_MAX = 2**63 - 1
_INT_MAX = 2**31 - 1


class BadRequest(Exception):
    """Invalid request payload; rendered as a 400 by the blueprint handler."""


def _as_int(value: Any, name: str, *, max_value: int = _BIGINT_MAX) -> int:
    """Parse a JSON int (or numeric string) within range; bools/floats rejected."""
    if isinstance(value, bool):
        raise BadRequest(f"Invalid {name}")
    if isinstance(value, str) and value.strip().lstrip("-").isdigit():
        value = int(value)
    if not isinstance(value, int) or not -max_value - 1 <= value <= max_value:
        raise BadRequest(f"Invalid {name}")
    return value


def _as_uid(value: Any) -> str:
    """Person uids are stored as text; accept JSON strings or ints."""
    if isinstance(value, bool) or not isinstance(value, (str, int)):
        raise BadRequest("Invalid person_uid")
    uid = str(value).strip()
    if not uid:
        raise BadRequest("Invalid person_uid")
    return uid


def _as_optional_str(value: Any, name: str) -> str | None:
    if value is not None and not isinstance(value, str):
        raise BadRequest(f"Invalid {name}")
    return value


def _membership_key(data: dict[str, Any]) -> tuple[int, str]:
    if not data.get("group_id") or not data.get("person_uid"):
        raise BadRequest("Both group_id and person_uid are required")
    return _as_int(data["group_id"], "group_id"), _as_uid(data["person_uid"])


def authorize():
    user = session.get("user")
    if not user:
        return {"error": "Authentication required"}, 401


admin_bp = Blueprint("admin", __name__, url_prefix="/api")
admin_bp.before_request(authorize)


@admin_bp.errorhandler(BadRequest)
def _bad_request(e: BadRequest):
    return {"error": str(e)}, 400


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

    group_id, person_uid = _membership_key(data)
    title = _as_optional_str(title, "title")
    tenant_id = session["user"].get("churchId")
    try:
        with db.transaction() as conn:
            row = mutations.add_membership(
                conn, group_id, person_uid, tenant_id, title=title
            )
    except UniqueViolation:
        return {"error": "Member already in group"}, 409
    if row is None:
        return {"error": "Group not found"}, 404

    return {"success": True, "data": db.to_json(row)}, 201


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

    group_id, person_uid = _membership_key(data)
    tenant_id = session["user"].get("churchId")
    with db.transaction() as conn:
        row = mutations.remove_membership(conn, group_id, person_uid, tenant_id)
    if row is None:
        return {"error": "Member not found in group"}, 404

    return {"success": True, "data": db.to_json(row)}, 200


@admin_bp.route("/group-membership", methods=["PUT"])
def update_group_member():
    """Update a group member's properties (title and/or link)"""
    data = request.get_json()
    if not data:
        return {"error": "No JSON data provided"}, 400

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

    group_id, person_uid = _membership_key(data)
    # Build update data with only provided fields
    update_data = {}
    if title is not None:
        update_data["title"] = _as_optional_str(title, "title")
    if link is not None:
        update_data["link"] = _as_optional_str(link, "link")

    if not update_data:
        return {"success": True}, 304  # Not modified

    tenant_id = session["user"].get("churchId")
    with db.transaction() as conn:
        row = mutations.update_membership(
            conn, group_id, person_uid, tenant_id, update_data
        )
    if row is None:
        return {"error": "Member not found in group"}, 404

    return {"success": True, "data": db.to_json(row)}, 200


@admin_bp.route("/groups/sort-order", methods=["PUT"])
def update_group_sort_order():
    """Update sort_order for a set of sibling groups (drag-to-reorder)."""
    data = request.get_json()
    if not data or not isinstance(data.get("updates"), list) or not data["updates"]:
        return {"error": "No updates provided"}, 400

    updates = data["updates"]
    for update in updates:
        if (
            not isinstance(update, dict)
            or "group_id" not in update
            or "sort_order" not in update
        ):
            return {"error": "Each update requires group_id and sort_order"}, 400

    if current_app.config["DEMO_MODE"]:
        tree = current_app.config["DEMO_TREE"]
        by_id = {group["group_id"]: group for group in tree}
        for update in updates:
            group = by_id.get(update["group_id"])
            if group:
                group["sort_order"] = update["sort_order"]
        return {"success": True}, 200

    parsed = [
        (
            _as_int(u["group_id"], "group_id"),
            _as_int(u["sort_order"], "sort_order", max_value=_INT_MAX),
        )
        for u in updates
    ]
    if len({group_id for group_id, _ in parsed}) != len(parsed):
        return {"error": "Duplicate group_id in updates"}, 400

    tenant_id = session["user"].get("churchId")
    try:
        with db.transaction() as conn:
            mutations.update_sort_order(conn, parsed, tenant_id)
    except mutations.GroupsNotFound as e:
        logger.warning(
            "sort_order update matched no rows: group_ids=%s tenant_id=%s",
            e.missing_ids,
            tenant_id,
        )
        ids = ", ".join(str(i) for i in e.missing_ids)
        return {
            "error": f"Group(s) {ids} not found or not updatable",
            "missing_group_ids": e.missing_ids,
        }, 404

    return {"success": True}, 200
