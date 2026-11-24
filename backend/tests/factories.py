"""Shared row factories and tenant constants for DB-backed tests."""

from db import Connection, Row, TenantId, fetch_one

TENANT = 1
OTHER_TENANT = 2


def make_group(
    conn: Connection,
    name: str,
    tenant_id: TenantId = TENANT,
    parent_id: int | None = None,
    sort_order: int | None = None,
) -> int:
    with conn.cursor() as cur:
        cur.execute(
            "INSERT INTO groups (name, parent_id, tenant_id, sort_order)"
            " VALUES (%s, %s, %s, %s) RETURNING id",
            [name, parent_id, tenant_id, sort_order],
        )
        row = fetch_one(cur)
        assert row is not None
        return row["id"]


def make_member(
    conn: Connection,
    group_id: int,
    uid: str,
    tenant_id: TenantId = TENANT,
    title: str | None = None,
    link: str | None = None,
) -> None:
    with conn.cursor() as cur:
        cur.execute(
            "INSERT INTO group_membership (group_id, bcc_person_uid, tenant_id,"
            " title, link) VALUES (%s, %s, %s, %s, %s)",
            [group_id, uid, tenant_id, title, link],
        )


def get_member(conn: Connection, group_id: int, uid: str) -> Row | None:
    with conn.cursor() as cur:
        cur.execute(
            "SELECT * FROM group_membership"
            " WHERE group_id = %s AND bcc_person_uid = %s",
            [group_id, uid],
        )
        return fetch_one(cur)
