"""Read-only SQL queries against the org hierarchy tables.

All functions take a psycopg2 connection (RealDictCursor) as the first argument
and never commit or roll back.
"""

from typing import Any

from db import tenant_clause


def get_tree(conn, tenant_id) -> list[dict[str, Any]]:
    """All groups for the tenant with their (tenant-scoped) member counts.

    The membership tenant filter lives in the JOIN condition so groups without
    members are kept with member_count 0.
    """
    m_clause, m_params = tenant_clause("m.tenant_id", tenant_id)
    g_clause, g_params = tenant_clause("g.tenant_id", tenant_id)
    sql = f"""
        SELECT g.id, g.name, g.parent_id, g.sort_order, COUNT(m.id) AS member_count
        FROM groups g
        LEFT JOIN group_membership m ON m.group_id = g.id AND {m_clause}
        WHERE {g_clause}
        GROUP BY g.id
        ORDER BY g.sort_order, g.id
    """
    with conn.cursor() as cur:
        cur.execute(sql, m_params + g_params)
        return list(cur.fetchall())


def get_group_memberships(conn, group_id: int, tenant_id) -> list[dict[str, Any]]:
    clause, params = tenant_clause("tenant_id", tenant_id)
    sql = f"""
        SELECT bcc_person_uid, title, link
        FROM group_membership
        WHERE group_id = %s AND {clause}
        ORDER BY id
    """
    with conn.cursor() as cur:
        cur.execute(sql, [group_id, *params])
        return list(cur.fetchall())


def get_titles(conn, tenant_id) -> list[str]:
    clause, params = tenant_clause("tenant_id", tenant_id)
    sql = f"""
        SELECT DISTINCT title
        FROM group_membership
        WHERE {clause} AND title IS NOT NULL
    """
    with conn.cursor() as cur:
        cur.execute(sql, params)
        return [row["title"] for row in cur.fetchall()]


def get_person_uids_by_title(conn, title: str, tenant_id) -> list[str]:
    clause, params = tenant_clause("tenant_id", tenant_id)
    sql = f"""
        SELECT DISTINCT bcc_person_uid
        FROM group_membership
        WHERE title = %s AND {clause}
    """
    with conn.cursor() as cur:
        cur.execute(sql, [title, *params])
        return [row["bcc_person_uid"] for row in cur.fetchall()]


def get_person_groups(conn, person_uid: str, tenant_id) -> list[dict[str, Any]]:
    """Groups (in the tenant) where the person is a member.

    member_count counts all memberships of each group, not tenant-filtered.
    """
    g_clause, g_params = tenant_clause("g.tenant_id", tenant_id)
    m_clause, m_params = tenant_clause("m.tenant_id", tenant_id)
    sql = f"""
        SELECT g.id, g.name,
               (SELECT COUNT(*) FROM group_membership a WHERE a.group_id = g.id)
                   AS member_count
        FROM groups g
        WHERE {g_clause}
          AND EXISTS (
              SELECT 1 FROM group_membership m
              WHERE m.group_id = g.id AND m.bcc_person_uid = %s AND {m_clause}
          )
        ORDER BY g.id
    """
    with conn.cursor() as cur:
        cur.execute(sql, [*g_params, person_uid, *m_params])
        return list(cur.fetchall())


def get_membership_report_rows(conn) -> list[dict[str, Any]]:
    """All memberships (all tenants) with their group name."""
    sql = """
        SELECT m.bcc_person_uid, m.title, g.name AS group_name
        FROM group_membership m
        JOIN groups g ON g.id = m.group_id
        ORDER BY m.id
    """
    with conn.cursor() as cur:
        cur.execute(sql)
        return list(cur.fetchall())
