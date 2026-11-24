"""Write operations on groups / group_membership.

All functions take an open connection as first argument and never commit or
roll back; callers wrap them in ``db.transaction()``. Inputs are expected to be
validated and typed by the caller (see ``admin.py``).
"""

from collections.abc import Iterable, Mapping, Sequence
from typing import Any

from db import Connection, Row, TenantId, fetch_all, fetch_one, tenant_clause
from psycopg2 import sql

UPDATABLE_MEMBERSHIP_FIELDS = frozenset({"title", "link"})


class GroupsNotFound(Exception):
    """Raised when some groups in a sort-order update don't exist for the tenant."""

    def __init__(self, missing_ids: Iterable[int]):
        self.missing_ids = sorted(missing_ids)
        super().__init__(f"Groups not found or not updatable: {self.missing_ids}")


def add_membership(
    conn: Connection,
    group_id: int,
    person_uid: str,
    tenant_id: TenantId,
    title: str | None = None,
) -> Row | None:
    """Insert a membership if the group belongs to the tenant.

    Returns the new row, or None if the group doesn't exist for the tenant.
    The membership's tenant_id is taken from the group itself.
    """
    tenant_sql, tenant_params = tenant_clause("g.tenant_id", tenant_id)
    with conn.cursor() as cur:
        cur.execute(
            "INSERT INTO group_membership (group_id, bcc_person_uid, tenant_id, title)"
            " SELECT g.id, %s, g.tenant_id, %s FROM groups g"
            f" WHERE g.id = %s AND {tenant_sql}"
            " RETURNING *",
            [person_uid, title, group_id, *tenant_params],
        )
        return fetch_one(cur)


def remove_membership(
    conn: Connection, group_id: int, person_uid: str, tenant_id: TenantId
) -> Row | None:
    tenant_sql, tenant_params = tenant_clause("tenant_id", tenant_id)
    with conn.cursor() as cur:
        cur.execute(
            "DELETE FROM group_membership"
            " WHERE group_id = %s AND bcc_person_uid = %s"
            f" AND {tenant_sql} RETURNING *",
            [group_id, person_uid, *tenant_params],
        )
        return fetch_one(cur)


def update_membership(
    conn: Connection,
    group_id: int,
    person_uid: str,
    tenant_id: TenantId,
    fields: Mapping[str, Any],
) -> Row | None:
    """Update title/link. Column names are dynamic, so this uses psycopg2.sql."""
    if not fields:
        raise ValueError("No fields to update")
    unknown = set(fields) - UPDATABLE_MEMBERSHIP_FIELDS
    if unknown:
        raise ValueError(f"Fields not updatable: {', '.join(sorted(unknown))}")

    tenant_sql, tenant_params = tenant_clause("tenant_id", tenant_id)
    names = list(fields)
    query = sql.SQL(
        "UPDATE group_membership SET {assignments}"
        " WHERE group_id = %s AND bcc_person_uid = %s AND {tenant} RETURNING *"
    ).format(
        assignments=sql.SQL(", ").join(
            sql.SQL("{} = %s").format(sql.Identifier(n)) for n in names
        ),
        tenant=sql.SQL(tenant_sql),  # trusted literal from tenant_clause
    )
    with conn.cursor() as cur:
        cur.execute(
            query,
            [*(fields[n] for n in names), group_id, person_uid, *tenant_params],
        )
        return fetch_one(cur)


def update_sort_order(
    conn: Connection, updates: Sequence[tuple[int, int]], tenant_id: TenantId
) -> None:
    """Set sort_order for several groups in one statement.

    ``updates`` is a sequence of (group_id, sort_order) with unique group ids.
    Raises GroupsNotFound if any group_id doesn't match a group for the tenant;
    the caller's transaction must then be rolled back so nothing is changed.
    """
    if not updates:
        return
    ids = [group_id for group_id, _ in updates]
    orders = [sort_order for _, sort_order in updates]

    tenant_sql, tenant_params = tenant_clause("g.tenant_id", tenant_id)
    with conn.cursor() as cur:
        cur.execute(
            "UPDATE groups g SET sort_order = v.sort_order"
            " FROM unnest(%s::bigint[], %s::int[]) AS v(id, sort_order)"
            f" WHERE g.id = v.id AND {tenant_sql}"
            " RETURNING g.id",
            [ids, orders, *tenant_params],
        )
        updated = {row["id"] for row in fetch_all(cur)}

    missing = set(ids) - updated
    if missing:
        raise GroupsNotFound(missing)
