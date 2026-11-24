"""Write operations on groups / group_membership.

All functions take an open connection as first argument and never commit or
roll back; callers wrap them in ``db.transaction()``.
"""

from collections.abc import Iterable, Mapping
from typing import Any

from db import tenant_clause
from psycopg2 import sql
from psycopg2.extras import execute_values

UPDATABLE_MEMBERSHIP_FIELDS = frozenset({"title", "link"})


class GroupsNotFound(Exception):
    """Raised when some groups in a sort-order update don't exist for the tenant."""

    def __init__(self, missing_ids: Iterable[int]):
        self.missing_ids = sorted(missing_ids)
        ids = ", ".join(str(i) for i in self.missing_ids)
        super().__init__(f"Group(s) {ids} not found or not updatable")


def add_membership(conn, group_id, person_uid, tenant_id, title=None) -> dict:
    columns = ["group_id", "bcc_person_uid", "tenant_id"]
    values: list[Any] = [group_id, person_uid, tenant_id]
    if title is not None:
        columns.append("title")
        values.append(title)

    query = sql.SQL(
        "INSERT INTO group_membership ({cols}) VALUES ({vals}) RETURNING *"
    ).format(
        cols=sql.SQL(", ").join(sql.Identifier(c) for c in columns),
        vals=sql.SQL(", ").join(sql.Placeholder() * len(values)),
    )
    with conn.cursor() as cur:
        cur.execute(query, values)
        return cur.fetchone()


def remove_membership(conn, group_id, person_uid, tenant_id) -> dict | None:
    tenant_sql, tenant_params = tenant_clause("tenant_id", tenant_id)
    with conn.cursor() as cur:
        cur.execute(
            "DELETE FROM group_membership"
            " WHERE group_id = %s AND bcc_person_uid = %s"
            f" AND {tenant_sql} RETURNING *",
            [group_id, person_uid, *tenant_params],
        )
        return cur.fetchone()


def update_membership(
    conn, group_id, person_uid, tenant_id, fields: Mapping[str, Any]
) -> dict | None:
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
        return cur.fetchone()


def update_sort_order(conn, updates: list[dict], tenant_id) -> None:
    """Set sort_order for several groups atomically.

    Raises GroupsNotFound if any group_id doesn't match a group for the tenant;
    the caller's transaction must then be rolled back so nothing is changed.
    """
    if not updates:
        return
    rows = [(int(u["group_id"]), int(u["sort_order"])) for u in updates]
    requested = {group_id for group_id, _ in rows}

    tenant_sql, tenant_params = tenant_clause("g.tenant_id", tenant_id)
    # execute_values only substitutes the VALUES %s; escape the tenant
    # parameters into the query text via mogrify.
    with conn.cursor() as cur:
        tenant_sql_bound = cur.mogrify(tenant_sql, tenant_params).decode()
        query = (
            "UPDATE groups g SET sort_order = v.sort_order"
            " FROM (VALUES %s) AS v(id, sort_order)"
            f" WHERE g.id = v.id AND {tenant_sql_bound.replace('%', '%%')}"
            " RETURNING g.id"
        )
        result = execute_values(
            cur,
            query,
            rows,
            template="(%s::bigint, %s::int)",
            page_size=max(len(rows), 1),
            fetch=True,
        )

    updated = {r["id"] if isinstance(r, Mapping) else r[0] for r in result}
    missing = requested - updated
    if missing:
        raise GroupsNotFound(missing)
